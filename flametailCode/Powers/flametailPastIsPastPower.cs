using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 已往不谏：当一张诅咒牌进入弃牌堆时，改为将其消耗。
/// </summary>
[RegisterPower]
public sealed class flametailPastIsPastPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailPastIsPastPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailPastIsPastPower.png");

    public override async Task AfterCardChangedPilesLate(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Owner.Creature != Owner)
        {
            return;
        }

        if (card.Type != CardType.Curse)
        {
            return;
        }

        // 诅咒牌只在进入弃牌堆时消耗。
        if (card.Pile?.Type != PileType.Discard)
        {
            return;
        }

        await CardPileCmd.Add(card, PileType.Exhaust, CardPilePosition.Top, this);
    }
}
