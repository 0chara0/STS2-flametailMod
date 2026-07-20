using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using flametail.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 隐藏 Buff：获得步法时把「进退自如」返回手牌。
/// </summary>
[RegisterPower]
public sealed class flametailFluidMotionPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailFluidMotionPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailFluidMotionPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not flametailFootworkPower || amount <= 0 || Owner == null)
        {
            return;
        }

        var state = Owner.Player?.PlayerCombatState;
        if (state == null)
        {
            return;
        }

        foreach (CardModel card in state.DrawPile.Cards)
        {
            if (card is flametailFluidMotion)
            {
                await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top);
            }
        }

        foreach (CardModel card in state.DiscardPile.Cards)
        {
            if (card is flametailFluidMotion)
            {
                await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top);
            }
        }

        foreach (CardModel card in state.ExhaustPile.Cards)
        {
            if (card is flametailFluidMotion)
            {
                await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top);
            }
        }
    }
}
