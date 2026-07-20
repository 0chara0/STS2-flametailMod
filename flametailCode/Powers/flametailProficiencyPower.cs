using System.Collections.Generic;
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

[RegisterPower]
public sealed class flametailProficiencyPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailProficiencyPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailProficiencyPower.png");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        if (!cardPlay.Card.Keywords.Contains(CardKeyword.Innate))
        {
            return;
        }

        int drawAmount = Amount;
        if (cardPlay.Card is flametailProficiency)
        {
            // 本次打出的这张熟稔本身不应触发自己；扣掉它刚叠加的这一层。
            drawAmount--;
        }

        if (drawAmount > 0)
        {
            if (Owner?.Player is not { } player)
            {
                return;
            }

            await CardPileCmd.Draw(choiceContext, drawAmount, player);
        }
    }
}
