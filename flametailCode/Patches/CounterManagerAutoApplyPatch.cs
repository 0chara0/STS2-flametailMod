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
/// 战斗开始时无条件为所有玩家施加一层 <see cref="flametailCounterManagerPower"/>。
/// 这样反制系统不再依赖初始遗物，其他角色获得反制牌后也能正常保留与自动打出。
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
