using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;
using flametail.Powers;

namespace flametail.Patches;

/// <summary>
/// 战斗开始时为所有玩家施加一层 <see cref="flametailCounterManagerPower"/>。
///
/// 无条件施加是刻意的：该能力是隐藏且惰性的——<see cref="flametailCounterManagerPower.BeforeFlushLate"/>
/// 在手牌没有反制牌时直接返回，<see cref="flametailCounterManagerPower.AfterDamageReceived"/> 在手牌
/// 找不到反制牌时也直接返回。因此对没有反制牌的玩家它只是空转的隐藏 Buff。
///
/// 曾尝试按“牌组含反制牌”等条件施加，但这会导致**非焰尾角色中途获得反制牌**（如事件/遗物
/// 中途生成进手牌、或反制牌只存在于手牌而不在牌组）时管理器缺失、保留/自动打出失效。
/// 恢复到无条件施加以覆盖所有路径。
///
/// 实现方式：用 Harmony 的“跳过原方法并返回替换 Task”模式（<see cref="__result"/>），
/// 先施加反制管理器，再调用原 <see cref="Hook.BeforeCombatStart"/> 逻辑。
/// 静态标志 <see cref="_isApplying"/> 防止递归。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class CounterManagerAutoApplyPatch
{
    private static bool _isApplying;

    private static bool Prefix(IRunState runState, ICombatState? combatState, ref Task __result)
    {
        // 当内部调用 Hook.BeforeCombatStart 时，直接放行原方法，避免无限递归。
        if (_isApplying)
        {
            return true;
        }

        __result = ApplyManagerAndRunOriginal(runState, combatState);
        return false;
    }

    private static async Task ApplyManagerAndRunOriginal(IRunState runState, ICombatState? combatState)
    {
        await ApplyCounterManagerPower(combatState);

        _isApplying = true;
        try
        {
            await Hook.BeforeCombatStart(runState, combatState);
        }
        finally
        {
            _isApplying = false;
        }
    }

    private static async Task ApplyCounterManagerPower(ICombatState? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (Player player in combatState.Players)
        {
            Creature? creature = player.Creature;
            if (creature == null || creature.HasPower<flametailCounterManagerPower>())
            {
                continue;
            }

            Entry.Logger.Info($"CounterManagerAutoApplyPatch: applying flametailCounterManagerPower to {creature.Name}.");

            await PowerCmd.Apply<flametailCounterManagerPower>(
                new ThrowingPlayerChoiceContext(),
                creature,
                1,
                creature,
                null);
        }
    }
}
