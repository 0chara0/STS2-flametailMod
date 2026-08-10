using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 隐藏 Buff：本回合打出的前 N 张攻击牌无视格挡。由「虚晃」施加。
/// 每打出一张攻击牌消耗 1 层；在玩家回合开始时清除剩余层数。
/// </summary>
[RegisterPower]
public sealed class flametailIgnoreBlockPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailIgnoreBlockPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailIgnoreBlockPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override Task BeforeAttack(AttackCommand command)
    {
        if (command.Attacker != Owner)
        {
            return Task.CompletedTask;
        }

        // 剩余层数 > 0 时让本次攻击无视格挡。
        // 层数按“张”消耗，见 AfterCardPlayed。
        if (Amount > 0)
        {
            command.DamageProps |= ValueProp.Unblockable;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        if (cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        if (Amount <= 0)
        {
            return;
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null);
        if (Amount <= 0)
        {
            await PowerCmd.Remove(this);
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
