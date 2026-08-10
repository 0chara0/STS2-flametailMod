using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using flametail.Powers;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：让闪避在格挡真正被消耗之前吸收攻击。
///
/// 敌方意图预览走的是 <see cref="MegaCrit.Sts2.Core.Hooks.Hook.ModifyDamage"/>，不会调用
/// <see cref="Creature.DamageBlockInternal"/>，所以敌方头上的攻击数字不会变。
///
/// 维护两组按战斗隔离的标记：
/// - <see cref="_pendingDodgesByCombat"/>：闪避已消耗、待由格挡阶段消费的“待缓冲”标记。
/// - <see cref="_dodgedThisDamageByCombat"/>：当前这段伤害实际被闪避抵消的记录，供
///   <see cref="AfterDamageReceived"/> 之类的后置阶段查询。
/// </summary>
public static class DodgeBlockPatch
{
    // 待消费的闪避标记：RegisterDodge 加入，DamageBlockInternal 前缀取出并跳过格挡。
    private static readonly ConditionalWeakTable<ICombatState, HashSet<Creature>> _pendingDodgesByCombat = new();

    // 本段伤害已被闪避抵消的记录：前缀消费待缓冲标记时写入，每次伤害结算开始前清空。
    private static readonly ConditionalWeakTable<ICombatState, HashSet<Creature>> _dodgedThisDamageByCombat = new();

    static DodgeBlockPatch()
    {
        CombatManager.Instance.CombatEnded += room =>
        {
            if (room?.CombatState != null)
            {
                _pendingDodgesByCombat.Remove(room.CombatState);
                _dodgedThisDamageByCombat.Remove(room.CombatState);
            }
        };
    }

    private static HashSet<Creature> GetSetForCombat(ConditionalWeakTable<ICombatState, HashSet<Creature>> table, ICombatState? combat)
    {
        if (combat == null)
        {
            return new HashSet<Creature>();
        }

        if (!table.TryGetValue(combat, out var set))
        {
            set = new HashSet<Creature>();
            table.Add(combat, set);
        }

        return set;
    }

    /// <summary>
    /// 标记目标在本段伤害中应被闪避抵消。
    /// 由 flametailDodgePower.BeforeDamageReceived 调用。
    /// </summary>
    public static void RegisterDodge(Creature target)
    {
        var set = GetSetForCombat(_pendingDodgesByCombat, target.CombatState);
        // 先清除可能残留的标记，避免目标转移等边界情况导致下一次受击被错误闪避。
        set.Remove(target);
        set.Add(target);
    }

    /// <summary>
    /// 查询指定生物在本段伤害中是否被闪避抵消。
    /// 只能在伤害结算的后续阶段（如 AfterDamageReceived）调用。
    /// </summary>
    public static bool WasAttackDodged(Creature target)
    {
        return GetSetForCombat(_dodgedThisDamageByCombat, target.CombatState).Contains(target);
    }

    /// <summary>
    /// 清除指定生物在本段伤害中的闪避记录。
    /// </summary>
    public static void ClearDodge(Creature target)
    {
        GetSetForCombat(_pendingDodgesByCombat, target.CombatState).Remove(target);
        GetSetForCombat(_dodgedThisDamageByCombat, target.CombatState).Remove(target);
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
    private static class DamageBlockInternalPatch
    {
        private static bool Prefix(Creature __instance, decimal amount, ref decimal __result)
        {
            // 每次进入格挡消耗阶段都重置“本段伤害是否被闪避”的记录；
            // 这样同一生物的下一段/下一次伤害不会误读到上一次的闪避结果。
            var dodgedThisDamage = GetSetForCombat(_dodgedThisDamageByCombat, __instance.CombatState);
            dodgedThisDamage.Remove(__instance);

            var pending = GetSetForCombat(_pendingDodgesByCombat, __instance.CombatState);
            if (!pending.Remove(__instance))
            {
                return true;
            }

            dodgedThisDamage.Add(__instance);

            // 跳过原始格挡消耗逻辑，把 amount 全部视为已被“缓冲”抵消，
            // 这样后续未格挡伤害 = 0，HP 不会减少，真实格挡也不会被扣。
            __result = amount;
            return false;
        }
    }
}
