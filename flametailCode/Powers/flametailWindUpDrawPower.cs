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

/// <summary>
/// 隐藏 Buff：在「蓄势待发」的抽牌期间，把抽到的非反制牌丢弃。
/// </summary>
[RegisterPower]
public sealed class flametailWindUpDrawPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailWindUpDrawPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailWindUpDrawPower.png");
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

        if (Amount <= 0)
        {
            await PowerCmd.Remove(this);
            return;
        }

        if (card is not ICounterCard)
        {
            await CardCmd.Discard(choiceContext, card);
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null);
        if (Amount <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
