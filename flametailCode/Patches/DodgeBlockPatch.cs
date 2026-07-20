using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：让闪避在格挡真正被消耗之前吸收攻击。
///
/// 敌方意图预览走的是 <see cref="MegaCrit.Sts2.Core.Hooks.Hook.ModifyDamage"/>，不会调用
/// <see cref="Creature.DamageBlockInternal"/>，所以敌方头上的攻击数字不会变。
/// </summary>
public static class DodgeBlockPatch
{
    // 记录哪些生物在当前这次受击已经被闪避抵消。
    //  combat 是单线程异步流程，普通 HashSet 足够。
    private static readonly HashSet<Creature> _dodgedTargets = [];

    /// <summary>
    /// 标记目标在本段伤害中应被闪避抵消。
    /// 由 flametailDodgePower.BeforeDamageReceived 调用。
    /// </summary>
    public static void RegisterDodge(Creature target)
    {
        // 先清除可能残留的标记，避免目标转移等边界情况导致下一次受击被错误闪避。
        _dodgedTargets.Remove(target);
        _dodgedTargets.Add(target);
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
    private static class DamageBlockInternalPatch
    {
        private static bool Prefix(Creature __instance, decimal amount, ref decimal __result)
        {
            if (!_dodgedTargets.Remove(__instance))
            {
                return true;
            }

            // 跳过原始格挡消耗逻辑，把 amount 全部视为已被“缓冲”抵消，
            // 这样后续未格挡伤害 = 0，HP 不会减少，真实格挡也不会被扣。
            __result = amount;
            return false;
        }
    }
}
