using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using flametail.Relics;

namespace flametail.Patches;

/// <summary>
/// 焰尾专属"乘算后加算"位（修改清单 [3] 裁定：不改引擎全局顺序，量尺另建乘算后加算）。
///
/// 引擎 Hook.ModifyBlock 的顺序 = 标准加算循环先跑、乘算循环后跑——遗物若坐在标准
/// ModifyBlockAdditive 槽，+1 会先于脆弱 ×0.75 结算：(4+1)×0.75=3，而文档要求 4×0.75+1=4。
/// 本 postfix 在两条循环全部结束后追加"最终加算"贡献（当前唯一贡献者=设计师量尺）。
/// 无焰尾遗物在场时集合为空、零开销，不影响原版全局顺序。
/// </summary>
public static class DesignersRulerFinalAdditivePatch
{
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyBlock))]
    private static class ModifyBlockFinalAdditivePatch
    {
        // cardPlay == null 表示本次格挡不是由打出卡牌直接引起（能力、药水、遗物等）。
        private static void Postfix(ICombatState combatState, Creature target, CardPlay? cardPlay, ref decimal __result)
        {
            foreach (Player player in combatState.Players)
            {
                foreach (RelicModel relic in player.Relics)
                {
                    if (relic is flametailDesignersRuler ruler)
                    {
                        __result += ruler.FinalBlockAdditive(target, __result, cardPlay);
                    }
                }
            }
        }
    }
}
