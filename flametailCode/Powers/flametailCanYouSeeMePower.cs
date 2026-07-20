using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

[RegisterPower]
public sealed class flametailCanYouSeeMePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailCanYouSeeMePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailCanYouSeeMePower.png");

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not flametailDodgePower)
        {
            return;
        }

        if (power.Owner != Owner)
        {
            return;
        }

        if (amount >= 0)
        {
            return;
        }

        if (Owner?.Player is not { } player)
        {
            return;
        }

        await CardPileCmd.Draw(choiceContext, Amount, player);
    }
}
