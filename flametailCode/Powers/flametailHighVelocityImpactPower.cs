using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
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
/// 隐藏 Buff：记录「下回合要给予的临时力量」。下回合玩家回合开始时，
/// 把累计量转为真实力量并施加「当回合结束移除」的临时力量标记，然后移除自身。
/// </summary>
[RegisterPower]
public sealed class flametailHighVelocityImpactPendingPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactTempStrengthPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactTempStrengthPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (Amount > 0)
        {
            // 下回合内生效：转为临时力量，并由 flametailHighVelocityImpactTempStrengthPower 在本回合结束时移除。
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount, Owner, null);
            await PowerCmd.Apply<flametailHighVelocityImpactTempStrengthPower>(
                choiceContext,
                Owner,
                Amount,
                Owner,
                null);
        }

        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 隐藏 Buff：标记「当前生效的临时力量」，在玩家回合结束时移除等量力量并自删。
/// </summary>
[RegisterPower]
public sealed class flametailHighVelocityImpactTempStrengthPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactTempStrengthPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactTempStrengthPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 只在玩家回合结束时移除，避免误清敌人回合结束。
        if (side != CombatSide.Player)
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
/// 高速冲击：每当你获得步法时，在下回合内获得 1 点临时力量。
/// </summary>
[RegisterPower]
public sealed class flametailHighVelocityImpactPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHighVelocityImpactPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

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

        // 多人模式下，只有「自己」获得步法时才获得临时力量。
        if (power.Owner != Owner)
        {
            return;
        }

        // 每次“获得步法”这一事件触发时，累计等同于本能力层数的力量，
        // 由 flametailHighVelocityImpactPendingPower 在下回合玩家回合开始时一次性转成临时力量。
        await PowerCmd.Apply<flametailHighVelocityImpactPendingPower>(
            choiceContext,
            Owner,
            Amount,
            Owner,
            null);
    }
}
