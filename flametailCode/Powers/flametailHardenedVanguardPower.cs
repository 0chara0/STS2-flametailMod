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
/// 百战先锋：每回合你前 n 次反制时，额外执行一次该反制效果。
/// </summary>
[RegisterPower]
public sealed class flametailHardenedVanguardPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _remainingThisTurn;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

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
        if (!CounterSystem.IsCounterPlay)
        {
            return;
        }

        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        if (CounterSystem.IsHardenedVanguardReplication)
        {
            return;
        }

        if (_remainingThisTurn <= 0)
        {
            return;
        }

        if (cardPlay.Card.Pile?.Type == PileType.Exhaust)
        {
            return;
        }

        _remainingThisTurn--;
        CounterSystem.IsHardenedVanguardReplication = true;
        try
        {
            // 把反制牌移回手牌再自动打出一次。
            if (cardPlay.Card.Pile?.Type != PileType.Hand)
            {
                await CardPileCmd.Add(cardPlay.Card, PileType.Hand, CardPilePosition.Top, this);
            }

            await CardCmd.AutoPlay(choiceContext, cardPlay.Card, CounterSystem.CurrentAttacker, AutoPlayType.Default);
        }
        finally
        {
            CounterSystem.IsHardenedVanguardReplication = false;
        }
    }
}
