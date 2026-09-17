using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
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
/// 携风：每当所有者（Owner）获得步法时，所有玩家各获得 Amount 点格挡（含所有者自己）。
/// 获得格挡不会改变步法层数，因此不会通过本能力再次触发自身，无需防环。
/// </summary>
[RegisterPower]
public sealed class flametailWindbringerPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailWindbringerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailWindbringerPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not flametailFootworkPower)
        {
            return;
        }

        if (power.Owner != Owner)
        {
            return;
        }

        // 只在“获得步法”（正增量）时触发；步法转化/扣除产生的负增量忽略。
        if (amount <= 0)
        {
            return;
        }

        if (Owner?.CombatState is not { } combat)
        {
            return;
        }

        // 所有玩家（包括自己）各获得 Amount 点格挡；跳过没有模型或已死亡的玩家。
        foreach (Player player in combat.Players)
        {
            if (player?.Creature == null || player.Creature.IsDead)
            {
                continue;
            }

            // Unpowered：与双手剑法等能力触发的获格一致，避免被攻击型加成影响。
            await CreatureCmd.GainBlock(player.Creature, Amount, ValueProp.Unpowered, null);
        }
    }
}
