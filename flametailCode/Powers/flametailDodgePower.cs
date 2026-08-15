using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using flametail.Patches;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 闪避：优先于格挡抵消 1 次攻击伤害；在玩家回合开始时清除。
///
/// 实现上通过 Harmony 补丁拦截 Creature.DamageBlockInternal，使闪避在真实格挡
/// 被扣除之前就把伤害吸收掉，同时不影响敌方意图预览的攻击数字。
/// </summary>
[RegisterPower]
public sealed class flametailDodgePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailDodgePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailDodgePower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner)
        {
            return Task.CompletedTask;
        }

        if (!props.IsPoweredAttack())
        {
            return Task.CompletedTask;
        }

        if (dealer == null || !dealer.IsEnemy)
        {
            return Task.CompletedTask;
        }

        if (Amount <= 0 || (int)amount <= 0)
        {
            return Task.CompletedTask;
        }

        return ConsumeDodgeAsync(choiceContext);
    }

    private async Task ConsumeDodgeAsync(PlayerChoiceContext choiceContext)
    {
        // 消耗 1 层闪避，并通知补丁跳过本次格挡消耗。
        await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null);
        if (Owner != null)
        {
            DodgeBlockPatch.RegisterDodge(Owner);
        }
    }

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (Amount > 0)
        {
            await PowerCmd.ModifyAmount(choiceContext, this, -Amount, Owner, null);
        }
    }
}
