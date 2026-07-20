using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 步里藏招：每当你获得步法时，抽 1 张牌。
/// </summary>
[RegisterPower]
public sealed class flametailStepGambitPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailStepGambitPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailStepGambitPower.png");

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power is not flametailFootworkPower)
        {
            return;
        }

        if (amount <= 0)
        {
            return;
        }

        if (Owner.Player == null)
        {
            return;
        }

        await CardPileCmd.Draw(choiceContext, Amount, Owner.Player);
    }
}
