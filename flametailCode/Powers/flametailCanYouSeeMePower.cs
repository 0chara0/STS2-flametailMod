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

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailCanYouSeeMePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailCanYouSeeMePower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not flametailFootworkPower)
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

        // 每次失去步法时抽 Amount 张（多张/多层能力按各自层数结算）。
        await CardPileCmd.Draw(choiceContext, (int)Amount, player);
    }
}
