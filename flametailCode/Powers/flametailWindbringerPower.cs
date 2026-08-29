using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 携风：每当所有者（Owner）获得步法时，将 1 层步法分享给记录的目标玩家。
/// 目标玩家由 flametailWindbringer 卡牌打出时的“选择其他玩家”目标确定。
/// </summary>
[RegisterPower]
public sealed class flametailWindbringerPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 目标玩家的 NetId（以字符串保存，符合 [SavedProperty] 支持的序列化类型）。
    /// </summary>
    [SavedProperty]
    public string TargetPlayerNetId { get; set; } = "";

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

        // 只在“获得步法”（正增量）时分享；步法转化/扣除产生的负增量忽略。
        if (amount <= 0)
        {
            return;
        }

        if (Owner?.CombatState is not { } combat)
        {
            return;
        }

        if (!ulong.TryParse(TargetPlayerNetId, out ulong targetNetId))
        {
            return;
        }

        var targetPlayer = combat.Players.FirstOrDefault(p => p.NetId == targetNetId);
        if (targetPlayer?.Creature == null || targetPlayer.Creature.IsDead)
        {
            return;
        }

        await PowerCmd.Apply<flametailFootworkPower>(
            choiceContext,
            targetPlayer.Creature,
            1,
            Owner,
            cardSource);
    }
}
