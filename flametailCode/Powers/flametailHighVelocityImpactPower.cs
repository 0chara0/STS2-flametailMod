using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 隐藏 Buff：在下回合开始时移除高速冲击提供的临时力量。
/// </summary>
[RegisterPower]
public sealed class flametailHighVelocityImpactTempStrengthPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactTempStrengthPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactTempStrengthPower.png");

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (Amount > 0)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, -Amount, Owner, null);
        }

        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 高速冲击：每当你获得步法时，在下回合开始前获得 1 点力量。
/// </summary>
[RegisterPower]
public sealed class flametailHighVelocityImpactPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactPower.png");

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

        int gained = (int)amount;
        if (gained <= 0)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, gained, Owner, null);
        await PowerCmd.Apply<flametailHighVelocityImpactTempStrengthPower>(
            choiceContext,
            Owner,
            gained,
            Owner,
            null);
    }
}
