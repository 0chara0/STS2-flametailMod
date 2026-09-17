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
/// 骑士对决：每回合你前 N 次主动打出反制牌时，触发其“反制时：”专属效果
/// （不重跑基础效果；纯反制牌不受影响），并计入一次反制发动（可与渐入佳境等联动）。
/// 不再整张重打，避免出现“两次非反制效果”。
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

    /// <summary>
    /// 在卡牌基础效果执行前（OnPlay 之前）预先判定骑士对决是否会触发该牌的反制效果。
    /// 若是“改为”类反制牌，则设置 <see cref="CounterContext.SuppressBaseEffect"/>，
    /// 让该牌 OnPlay 跳过基础效果（“改为”语义：基础效果被反制效果替换）。
    /// 先清除可能残留的旧标志，再按本张牌重新判定，避免标志泄漏到下一张牌。
    /// </summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var counterContext = this.GetCounterContext();
        counterContext.SuppressBaseEffect = false;

        if (cardPlay.Card is ICounterCard { CounterEffectReplacesBaseEffect: true }
            && IsKnightsDuelTrigger(cardPlay))
        {
            counterContext.SuppressBaseEffect = true;
        }

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

        // 直接触发该牌的“反制时：”专属效果（不再整张重打，避免基础效果重复出现）。
        // 剑如火舞在场时，反制效果同样重定向到所有敌人。
        bool oldRetarget = counterContext.ShouldRetargetCounterToAllEnemies;
        counterContext.ShouldRetargetCounterToAllEnemies = Owner.HasPower<flametailFireDancingSwordPower>();
        try
        {
            var counterCard = (ICounterCard)cardPlay.Card;
            await counterCard.TriggerCounterEffect(choiceContext, cardPlay, cardPlay.Target);

            // 计入一次反制发动（供渐入佳境等使用）。
            var manager = Owner.GetPower<flametailCounterManagerPower>();
            if (manager != null)
            {
                manager.CountersTriggeredThisCombat++;
            }
        }
        finally
        {
            counterContext.ShouldRetargetCounterToAllEnemies = oldRetarget;
        }
    }

    /// <summary>
    /// 供百战先锋在 <c>BeforeCardPlayed</c> 时预测本次出牌是否会在结算后由骑士对决
    /// 触发“反制时：”效果（与 <see cref="IsKnightsDuelTrigger"/> 同一判定），
    /// 以便把这张牌计入重放队列。
    /// </summary>
    public bool WouldTriggerCounterEffect(CardPlay cardPlay) => IsKnightsDuelTrigger(cardPlay);

    /// <summary>
    /// 判定骑士对决是否会在当前出牌后触发该牌的反制效果。
    /// <see cref="BeforeCardPlayed"/>（设置基础效果抑制标志）与 <see cref="AfterCardPlayedLate"/>
    /// （触发反制效果）共用同一判定，保证“抑制基础效果”与“触发反制效果”总是成对发生。
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

        // 只对拥有“反制时：”专属效果的反制牌生效；纯反制牌（反制 = 普通效果）不受影响。
        if (cardPlay.Card is not ICounterCard { HasCounterEffect: true })
        {
            return false;
        }

        if (cardPlay.Card.Pile?.Type == PileType.Exhaust)
        {
            return false;
        }

        return true;
    }
}
