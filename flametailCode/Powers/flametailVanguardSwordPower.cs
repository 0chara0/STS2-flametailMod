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
public sealed class flametailVanguardSwordPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailVanguardSwordPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailVanguardSwordPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!CounterSystem.IsCounterPlay)
        {
            return;
        }

        if (cardPlay.Card.Owner.Creature != Owner)
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
