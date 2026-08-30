using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 领导者：你存活时，生命值高于一半的玩家获得力量，生命值不高于一半的玩家获得敏捷。
/// 可叠加：Amount 即授予层数（每打出一次叠加，授予的力量/敏捷随 Amount 变化）。
/// 实际的力量/敏捷授予与按血量实时切换，由每个盟友身上的 flametailLeaderAuraPower 各自独立管理（每连接一份，互不影响）。
/// 本能力只在卡牌打出时调用 Evaluate 建立/同步各连接。不依赖回合开始判定。
/// </summary>
[RegisterPower]
public sealed class flametailLeaderPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailLeaderPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailLeaderPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    /// <summary>
    /// 全量对齐：为所有存活玩家建立/同步“领导者-盟友”连接。
    /// InstancedPerApplier：同一施放者叠加层数，不同施放者各自独立实例（分开显示、分开结算）。
    /// 仅由 flametailLeader 卡牌打出时调用（即时反馈 + 叠加后同步层数）。
    /// </summary>
    public async Task Evaluate(PlayerChoiceContext choiceContext)
    {
        if (Owner == null || Owner.IsDead || Owner.CombatState is not { } combat)
        {
            return;
        }

        int grant = (int)Amount;
        if (grant <= 0)
        {
            return;
        }

        foreach (Player p in combat.Players)
        {
            if (p.Creature == null || p.Creature.IsDead)
            {
                continue;
            }

            // 每个盟友身上的 aura 会自行按血量授予力量/敏捷、并实时切换。
            await PowerCmd.Apply<flametailLeaderAuraPower>(choiceContext, p.Creature, grant, Owner, null);
        }
    }
}
