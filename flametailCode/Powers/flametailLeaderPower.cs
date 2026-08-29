using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 领导者：你存活时，生命值高于一半的玩家获得力量，生命值不高于一半的玩家获得敏捷。
/// 在每位玩家的回合开始时重新评估所有玩家：先移除上次授予的增益，再按当前血量重新授予。
/// 授予层数（GrantAmount）由 flametailLeader 卡牌按升级状态写入。
/// </summary>
[RegisterPower]
public sealed class flametailLeaderPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 每次评估时授予的力量 / 敏捷层数（升级前 2，升级后 3）。
    /// </summary>
    [SavedProperty]
    public int GrantAmount { get; set; } = 2;

    /// <summary>
    /// 当前被授予力量的玩家 NetId 列表（逗号分隔字符串，兼容 [SavedProperty] 序列化类型）。
    /// </summary>
    [SavedProperty]
    public string StrengthGrantedNetIds { get; set; } = "";

    /// <summary>
    /// 当前被授予敏捷的玩家 NetId 列表。
    /// </summary>
    [SavedProperty]
    public string DexterityGrantedNetIds { get; set; } = "";

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailLeaderPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailLeaderPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 每位玩家（含自己）的回合开始时都重新评估一次所有玩家。
        await Evaluate(choiceContext);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        // 领导者死亡后光环失效：移除已授予的力量/敏捷，避免增益残留。
        if (creature != Owner)
        {
            return;
        }

        await RemoveGrantedBuffs(choiceContext);
        StrengthGrantedNetIds = "";
        DexterityGrantedNetIds = "";
    }

    /// <summary>
    /// 重新评估：先移除上次授予的增益，再按当前血量给所有存活玩家重新授予力量/敏捷。
    /// 由卡牌打出时调用一次以获得即时反馈，之后在每个玩家回合开始自动调用。
    /// </summary>
    public async Task Evaluate(PlayerChoiceContext choiceContext)
    {
        if (Owner == null || Owner.IsDead || Owner.CombatState is not { } combat)
        {
            return;
        }

        await RemoveGrantedBuffs(choiceContext);

        var strengthIds = new List<string>();
        var dexterityIds = new List<string>();

        foreach (Player p in combat.Players)
        {
            if (p.Creature == null || p.Creature.IsDead)
            {
                continue;
            }

            // 生命值高于一半 → 力量；不高于一半 → 敏捷。
            bool aboveHalf = p.Creature.CurrentHp * 2 > p.Creature.MaxHp;
            if (aboveHalf)
            {
                await PowerCmd.Apply<StrengthPower>(choiceContext, p.Creature, GrantAmount, Owner, null);
                strengthIds.Add(p.NetId.ToString());
            }
            else
            {
                await PowerCmd.Apply<DexterityPower>(choiceContext, p.Creature, GrantAmount, Owner, null);
                dexterityIds.Add(p.NetId.ToString());
            }
        }

        StrengthGrantedNetIds = string.Join(",", strengthIds);
        DexterityGrantedNetIds = string.Join(",", dexterityIds);
    }

    /// <summary>
    /// 把上次由本能力授予的力量/敏捷从对应玩家身上移除。
    /// </summary>
    private async Task RemoveGrantedBuffs(PlayerChoiceContext choiceContext)
    {
        foreach (ulong netId in SplitNetIds(StrengthGrantedNetIds))
        {
            await RemoveGranted(choiceContext, netId, true);
        }

        foreach (ulong netId in SplitNetIds(DexterityGrantedNetIds))
        {
            await RemoveGranted(choiceContext, netId, false);
        }
    }

    private async Task RemoveGranted(PlayerChoiceContext choiceContext, ulong netId, bool isStrength)
    {
        var player = Owner?.CombatState?.Players.FirstOrDefault(p => p.NetId == netId);
        if (player?.Creature == null)
        {
            return;
        }

        if (isStrength)
        {
            if (player.Creature.GetPower<StrengthPower>() is { } power && power.Amount > 0)
            {
                decimal offset = -System.Math.Min(power.Amount, (decimal)GrantAmount);
                await PowerCmd.ModifyAmount(choiceContext, power, offset, Owner, null);
            }
        }
        else
        {
            if (player.Creature.GetPower<DexterityPower>() is { } power && power.Amount > 0)
            {
                decimal offset = -System.Math.Min(power.Amount, (decimal)GrantAmount);
                await PowerCmd.ModifyAmount(choiceContext, power, offset, Owner, null);
            }
        }
    }

    private static IEnumerable<ulong> SplitNetIds(string csv)
    {
        if (string.IsNullOrEmpty(csv))
        {
            yield break;
        }

        foreach (string part in csv.Split(','))
        {
            if (ulong.TryParse(part, out ulong id))
            {
                yield return id;
            }
        }
    }
}
