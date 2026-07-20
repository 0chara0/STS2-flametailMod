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

/// <summary>
/// 崇高准则：前 N 次抽到诅咒牌时，将其丢弃并抽 1 张牌。
/// </summary>
[RegisterPower]
public sealed class flametailLoftyPrinciplePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailLoftyPrinciplePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailLoftyPrinciplePower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (card.Owner.Creature != Owner)
        {
            return;
        }

        if (card.Type != CardType.Curse)
        {
            return;
        }

        if (Amount <= 0)
        {
            return;
        }

        if (Owner?.Player is not { } player)
        {
            return;
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null);
        await CardCmd.Discard(choiceContext, card);
        await CardPileCmd.Draw(choiceContext, 1, player);
    }
}
