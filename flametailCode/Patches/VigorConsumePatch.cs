using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Helpers;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：支援类攻击（伤害 props 带 <see cref="FlametailValueProps.GetIgnoreAttackerDamageModifiers"/> 标志）
/// 不消耗活力（VigorPower），但保留活力的加伤。
///
/// 反编译结论（sts2 VigorPower）：
/// - VigorPower.BeforeAttack(AttackCommand)：当攻击者是 Owner、伤害是 PoweredAttack 且来源为卡牌（或无来源）时，
///   把该次 AttackCommand 记录进内部数据（commandToModify / amountWhenAttackStarted = 当前 Amount）。
/// - VigorPower.ModifyDamageAdditive(...)：为被记录的攻击返回 Amount（加伤）；commandToModify == null 时也会返回 Amount。
/// - VigorPower.AfterAttack(...)：command == commandToModify 时，一次性消耗 amountWhenAttackStarted 层。
/// 即“消耗”发生在 AfterAttack，且以 BeforeAttack 的记录为前提。
///
/// 因此本补丁在 BeforeAttack 阶段拦截支援伤害：不记录本次攻击 →
/// ModifyDamageAdditive 照常返回 Amount（活力加伤保留，符合“不能阻止活力加伤”的要求），
/// AfterAttack 因 command != commandToModify 而不消耗（活力不被支援牌消耗）。
/// 原版多层活力是“下一次攻击全额消耗”，本补丁只影响带支援标志的攻击命令，其它攻击行为不变。
/// </summary>
public static class VigorConsumePatch
{
    /// <summary>
/// 华舞等多段攻击效果在整段攻击期间置为 true：暂停活力的原版消耗流程，
/// 由调用方在整段攻击结束后一次性消耗活力（每段伤害仍享受活力加伤——
/// ModifyDamageAdditive 返回加伤不依赖 AfterAttack 的消耗）。
/// </summary>
public static bool SuppressVigorConsumption;

[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.AfterAttack))]
private static class VigorPowerAfterAttackPatch
{
    private static bool Prefix(PlayerChoiceContext choiceContext, AttackCommand command, VigorPower __instance, ref Task __result)
    {
        // 仅抑制本能力拥有者的攻击消耗；抑制期间活力层数保持不变，
        // 由华舞在攻击循环结束后通过 ModifyAmount 一次性扣除。
        if (!SuppressVigorConsumption || command.Attacker != __instance.Owner)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.BeforeAttack))]
    private static class VigorPowerBeforeAttackPatch
    {
        private static bool Prefix(AttackCommand command, VigorPower __instance, ref Task __result)
        {
            // 只处理本能力拥有者发起的攻击。
            if (command.Attacker == null || command.Attacker != __instance.Owner)
            {
                return true;
            }

            // 仅拦截支援（固定伤害）标志的攻击命令，普通攻击照常记录并消耗活力。
            if (!command.DamageProps.HasFlag(FlametailValueProps.GetIgnoreAttackerDamageModifiers()))
            {
                return true;
            }

            // 跳过记录：本次支援攻击不进入活力消耗流程。
            __result = Task.CompletedTask;
            return false;
        }
    }
}
