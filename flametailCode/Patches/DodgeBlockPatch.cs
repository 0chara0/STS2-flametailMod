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
/// </summary>
public static class DodgeBlockPatch
{
    // 按战斗隔离闪避标记，避免跨战斗泄漏或在联机/存档读档时错位。
    private static readonly ConditionalWeakTable<ICombatState, HashSet<Creature>> _dodgedTargetsByCombat = new();

    static DodgeBlockPatch()
    {
        CombatManager.Instance.CombatEnded += room =>
        {
            if (room?.CombatState != null)
            {
                _dodgedTargetsByCombat.Remove(room.CombatState);
            }
        };
    }

    private static HashSet<Creature> GetSetForCombat(ICombatState? combat)
    {
        if (combat == null)
        {
            return new HashSet<Creature>();
        }

        if (!_dodgedTargetsByCombat.TryGetValue(combat, out var set))
        {
            set = new HashSet<Creature>();
            _dodgedTargetsByCombat.Add(combat, set);
        }

        return set;
    }

    /// <summary>
    /// 标记目标在本段伤害中应被闪避抵消。
    /// 由 flametailDodgePower.BeforeDamageReceived 调用。
    /// </summary>
    public static void RegisterDodge(Creature target)
    {
        var set = GetSetForCombat(target.CombatState);
        // 先清除可能残留的标记，避免目标转移等边界情况导致下一次受击被错误闪避。
        set.Remove(target);
        set.Add(target);
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
    private static class DamageBlockInternalPatch
    {
        private static bool Prefix(Creature __instance, decimal amount, ref decimal __result)
        {
            // 只在玩家受到伤害时维护闪避标记，避免反制牌攻击敌人时把标记清掉。
            if (__instance.IsPlayer)
            {
                // 每次进入格挡消耗阶段都重置闪避标记；
                // 若本次伤害被注册为闪避，则在同步上下文中设为 true，
                // 这样 AfterDamageReceived / 反制牌读取时不会因为 AsyncLocal 跨 async 边界而丢失。
                CounterSystem.WasAttackDodged = false;
            }

            var set = GetSetForCombat(__instance.CombatState);
            if (!set.Remove(__instance))
            {
                return true;
            }

            if (__instance.IsPlayer)
            {
                CounterSystem.WasAttackDodged = true;
            }

            // 跳过原始格挡消耗逻辑，把 amount 全部视为已被“缓冲”抵消，
            // 这样后续未格挡伤害 = 0，HP 不会减少，真实格挡也不会被扣。
            __result = amount;
            return false;
        }
    }
}
