using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;
using flametail.Powers;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：在任意敌人执行回合行动之前，为拥有 <see cref="flametailPreemptPower"/> 的玩家触发先发制人效果。
///
/// 注意：通过反射调用原方法时会重新进入本 patch，因此用按战斗隔离的 HashSet 做递归守卫，
/// 避免无限递归/Task 死锁。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.TakeTurn))]
internal static class PreemptTakeTurnPatch
{
    private static readonly ConditionalWeakTable<ICombatState, HashSet<Creature>> _processingEnemies = new();

    static PreemptTakeTurnPatch()
    {
        CombatManager.Instance.CombatEnded += room =>
        {
            if (room?.CombatState != null)
            {
                _processingEnemies.Remove(room.CombatState);
            }
        };
    }

    private static bool IsProcessing(ICombatState? combatState, Creature enemy)
    {
        if (combatState == null)
        {
            return false;
        }

        if (!_processingEnemies.TryGetValue(combatState, out var set))
        {
            set = new HashSet<Creature>();
            _processingEnemies.Add(combatState, set);
        }

        return !set.Add(enemy);
    }

    private static void MarkDone(ICombatState? combatState, Creature enemy)
    {
        if (combatState != null && _processingEnemies.TryGetValue(combatState, out var set))
        {
            set.Remove(enemy);
        }
    }

    private static bool Prefix(Creature __instance, MethodBase __originalMethod, ref Task __result)
    {
        if (!__instance.IsMonster || __instance.Side != CombatSide.Enemy)
        {
            return true;
        }

        // 没有任何玩家拥有先发制人时，不要拦截 TakeTurn，直接放行原方法。
        // 之前无条件替换原方法：敌人以“已死但待复活”状态进入 TakeTurn 时，
        // 下方的 IsDead 判断会跳过原方法，卡住敌方回合开始的复活效果。
        if (!AnyPlayerHasPreempt(__instance.CombatState))
        {
            return true;
        }

        // 递归守卫：如果是从原方法反射调用重新进入的，直接执行原方法。
        if (IsProcessing(__instance.CombatState, __instance))
        {
            return true;
        }

        __result = RunPreemptThenOriginal(__instance, __originalMethod);
        return false;
    }

    /// <summary>
    /// 是否有玩家当前拥有先发制人能力。
    /// </summary>
    private static bool AnyPlayerHasPreempt(ICombatState? combatState)
    {
        if (combatState == null)
        {
            return false;
        }

        foreach (Player player in combatState.Players)
        {
            if (player.Creature?.GetPower<flametailPreemptPower>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task RunPreemptThenOriginal(Creature enemy, MethodBase originalMethod)
    {
        ICombatState? combatState = enemy.CombatState;
        try
        {
            if (CombatManager.Instance.IsOverOrEnding)
            {
                await (Task)originalMethod.Invoke(enemy, null)!;
                return;
            }

            if (combatState != null)
            {
                foreach (Player player in combatState.Players)
                {
                    if (player.Creature?.IsDead ?? true)
                    {
                        continue;
                    }

                    flametailPreemptPower? power = player.Creature.GetPower<flametailPreemptPower>();
                    if (power == null)
                    {
                        continue;
                    }

                    // 在行动执行期间复用当前 GameAction 的 ChoiceContext，
                    // 避免新建 HookPlayerChoiceContext 可能导致的嵌套 action 死锁。
                    PlayerChoiceContext context = RunManager.Instance.ActionExecutor.CurrentlyRunningAction is { } action
                        ? new GameActionPlayerChoiceContext(action)
                        : new ThrowingPlayerChoiceContext();

                    await power.OnBeforeEnemyAction(context, player, enemy);
                }
            }

            // 只有先发制人把敌人打死时才跳过原行动。
            // 若敌人死后不会被移出战斗（如带复活机制的“死亡待复活”状态），必须放行原 TakeTurn
            // 以执行复活，否则跳过会卡住敌人的复活流程。判断不再依赖“回合开始时是否已死”，
            // 而是直接问“死后是否会被移出战斗”，这样即便敌人是本回合先发制人打死的，
            // 只要它要复活就不会被误跳过。
            if (enemy.IsDead
                && combatState != null
                && Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, enemy))
            {
                return;
            }

            if (CombatManager.Instance.IsOverOrEnding)
            {
                return;
            }

            await (Task)originalMethod.Invoke(enemy, null)!;
        }
        finally
        {
            MarkDone(combatState, enemy);
        }
    }
}
