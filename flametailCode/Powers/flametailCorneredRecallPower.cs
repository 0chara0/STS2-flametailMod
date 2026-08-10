using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 困兽之斗的倒计时 Buff：每回合开始层数减 1，
/// 减到 0 时把消耗牌堆中的诅咒/状态牌全部返回弃牌堆，然后移除自身。
/// InstanceType 为 Instanced：每次打出困兽之斗都新建一个独立实例并各自倒计时，
/// 多次打出会在各自的对应回合分别触发，而不是重置/延长同一条倒计时。
/// 层数（Amount）即剩余回合数，在卡面/能力栏上可见。
/// </summary>
[RegisterPower]
public sealed class flametailCorneredRecallPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override bool IsVisibleInternal => true;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailCorneredRecallPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailCorneredRecallPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    /// <summary>
    /// 每个战斗最近一次打出困兽之斗时的玩家回合号，用于同回合去重。
    /// 状态按 <see cref="ICombatState"/> 隔离，战斗结束自动随战斗状态回收。
    /// </summary>
    private static readonly ConditionalWeakTable<ICombatState, TurnRecord> _lastAppliedTurn = new();

    private sealed class TurnRecord
    {
        public int TurnNumber;
    }

    /// <summary>
    /// 若该战斗当前玩家回合尚未打出过困兽之斗，则记录本回合并返回 true；
    /// 若本回合已经打出过（同一回合重复打出），返回 false，不新建倒计时实例。
    /// </summary>
    public static bool MarkAppliedIfFirstThisTurn(ICombatState? combat, int turnNumber)
    {
        if (combat == null)
        {
            return true;
        }

        if (!_lastAppliedTurn.TryGetValue(combat, out TurnRecord? record))
        {
            record = new TurnRecord();
            _lastAppliedTurn.Add(combat, record);
        }

        if (record.TurnNumber == turnNumber)
        {
            return false;
        }

        record.TurnNumber = turnNumber;
        return true;
    }

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (Amount > 1)
        {
            // 还没到时间，继续倒计时。
            await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null);
            return;
        }

        // 最后一次：把消耗牌堆中的诅咒/状态牌全部返回弃牌堆。
        var state = Owner.Player?.PlayerCombatState;
        if (state != null)
        {
            // 先快照再移动，避免修改牌堆时枚举器失效。
            List<CardModel> toReturn = new();
            foreach (CardModel card in state.ExhaustPile.Cards)
            {
                if (card.Type == CardType.Curse || card.Type == CardType.Status)
                {
                    toReturn.Add(card);
                }
            }

            foreach (CardModel card in toReturn)
            {
                await CardPileCmd.Add(card, PileType.Discard, CardPilePosition.Top);
            }
        }

        await PowerCmd.Remove(this);
    }
}
