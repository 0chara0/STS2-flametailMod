using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 骑士对决：每回合你前 N 次主动打出反制牌时，该牌的直接打出效果照常执行，
/// 并额外以反制上下文把整张牌再打出一次（触发其反制效果，计入一次反制发动）。
/// </summary>
[RegisterPower]
public sealed class flametailKnightsDuelPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    private int _remainingThisTurn;

    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        _remainingThisTurn = Amount;
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power != this || amount <= 0)
        {
            return Task.CompletedTask;
        }

        _remainingThisTurn += (int)amount;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var counterContext = this.GetCounterContext();

        if (!IsKnightsDuelTrigger(cardPlay))
        {
            return;
        }

        _remainingThisTurn--;

        // 新语义（修改文档 [40]）：主动打出反制牌 = 该牌的直接打出效果 + 额外以反制打出一次此牌。
        // 基础效果已由打出流程正常执行；这里以反制上下文把整张牌再打一次——
        // 经 CounterSystem.PlayCounterCard → CardCmd.AutoPlay，会走该牌 OnPlay 的反制分支并触发反制效果与反制计数。
        // IsKnightsDuelReplication 让 IsKnightsDuelTrigger 返回 false，防止重打的打出再次触发本能力（无限递归）。
        var card = cardPlay.Card;
        if (card.Pile?.Type != PileType.Hand)
        {
            // 部分反制牌打出时会被消耗/丢弃（如卸武攻势反制自毁）：先取回手牌顶再重打。
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, this);
        }

        bool oldReplication = counterContext.IsKnightsDuelReplication;
        counterContext.IsKnightsDuelReplication = true;
        try
        {
            // 招架匕首（修改文档 ③）：由骑士对决打出反制牌前触发一次。
            await flametailParryingDaggerPower.TriggerForOwner(choiceContext, Owner, cardPlay.Target);

            await CounterSystem.PlayCounterCard(choiceContext, card, cardPlay.Target);
        }
        finally
        {
            counterContext.IsKnightsDuelReplication = oldReplication;
        }
    }

    /// <summary>
    /// 百战先锋在 <c>BeforeCardPlayed</c> 时预测本次出牌是否会在结算后由骑士对决
    /// 触发重打（即是否应入重放队列）。预测与 <see cref="AfterCardPlayedLate"/> 同一判定。
    /// </summary>
    public bool WouldTriggerCounterEffect(CardPlay cardPlay) => IsKnightsDuelTrigger(cardPlay);

    /// <summary>
    /// 判定骑士对决是否会在当前出牌后触发（整张重打）。
    /// </summary>
    private bool IsKnightsDuelTrigger(CardPlay cardPlay)
    {
        var counterContext = this.GetCounterContext();

        // 仅主动打出（非反制自动打出）时触发。
        if (counterContext.IsCounterPlay)
        {
            return false;
        }

        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return false;
        }

        if (counterContext.IsKnightsDuelReplication)
        {
            return false;
        }

        if (_remainingThisTurn <= 0)
        {
            return false;
        }

        // 所有反制牌都响应（2026-10-15 用户裁定）：纯反制牌（闪击、破隙等，反制=普通效果）
        // 也重打，与卡面"主动打出反制牌时触发其反制效果"一致——此前只放行
        // HasCounterEffect 牌是过滤错误，导致闪击这类牌无法被骑士对决响应。
        // 消耗型反制牌同样覆盖（2026-10-15 用户裁定）：打出后进消耗堆的由
        // AfterCardPlayedLate 的取回逻辑取回手牌顶再重打，不在此处排除。
        if (cardPlay.Card is not ICounterCard)
        {
            return false;
        }

        return true;
    }
}
