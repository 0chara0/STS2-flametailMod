using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

[RegisterPower]
public sealed class flametailRefocusPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailRefocusPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailRefocusPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    private readonly HashSet<CardModel> _processing = new();

    public override async Task AfterCardChangedPilesLate(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        if (card.Owner.Creature != Owner)
        {
            return;
        }

        if (card.Type != CardType.Curse)
        {
            return;
        }

        PileType? newPile = card.Pile?.Type;
        if (newPile != PileType.Discard && newPile != PileType.Exhaust)
        {
            return;
        }

        if (!_processing.Add(card))
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(
            null!,
            Owner,
            Amount,
            Owner,
            null);

        _processing.Remove(card);
    }
}
