using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 本能移动：每回合你打出的前 X 张能获得步法或闪避的牌耗能变为 0（X 为能力层数）。
/// </summary>
[RegisterPower]
public sealed class flametailReflexMovePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private class Data
    {
        public int CardsPlayedThisTurn;
    }

    protected override object InitInternalData()
    {
        return new Data();
    }

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailReflexMovePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailReflexMovePower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ShouldMakeFree(card))
        {
            return false;
        }
        modifiedCost = default(decimal);
        return true;
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ShouldMakeFree(card))
        {
            return false;
        }
        modifiedCost = default(decimal);
        return true;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner
            && cardPlay.Card is IGainFootworkCard or IGainDodgeCard
            && cardPlay.IsLastInSeries)
        {
            GetInternalData<Data>().CardsPlayedThisTurn++;
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner))
        {
            GetInternalData<Data>().CardsPlayedThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    private bool ShouldMakeFree(CardModel card)
    {
        if (card.Owner.Creature != Owner)
        {
            return false;
        }

        switch (card.Pile?.Type)
        {
            case PileType.Hand:
            case PileType.Play:
                break;
            default:
                return false;
        }

        // 同时接受“能获得步法”与“能获得闪避”的牌。
        if (card is not (IGainFootworkCard or IGainDodgeCard))
        {
            return false;
        }

        return GetInternalData<Data>().CardsPlayedThisTurn < Amount;
    }
}
