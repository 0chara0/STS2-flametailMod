using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Nodes.Vfx;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 盔甲改良：召唤瑕光，为你提供 3 点格挡（每次升级额外 +2,+3,+4…）和 2(+已升级次数) 层覆甲。
/// 灼热攻击式递增升级（可多次升级）；进入休息点时自动升级一次。支援、消耗。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailArmorUpgrade : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;
    private const int BaseBlock = 3;
    private const int BasePlatedArmor = 2;

    private int _upgradeCount;

    /// <summary>
    /// 已升级次数（灼热攻击式递增升级的核心，[SavedProperty] 持久化）。
    /// 这是本卡进入联机序列化/校验和的唯一状态（int 可序列化）；实际数值始终由下面的
    /// 属性从它实时计算，两端必然一致。setter 中同步显示用 DynamicVars：
    /// DynamicVars 不属于 [SavedProperty] 序列化/校验和范围，改动不会造成联机分歧，
    /// 但能覆盖“战斗内被外部效果升级”时对端只走反序列化（不跑 OnUpgrade）的显示刷新。
    /// </summary>
    [SavedProperty]
    public int UpgradeCount
    {
        get => _upgradeCount;
        set
        {
            AssertMutable();
            _upgradeCount = value;
            SyncDynamicVars();
        }
    }

    /// <summary>格挡 = 4 + 累加增量（第 n 次升级 +n+1，即 +2,+3,+4…）。</summary>
    public int BlockAmount => BaseBlock + UpgradeCount * (UpgradeCount + 3) / 2;

    /// <summary>覆甲 = 4 + 已升级次数（每次 +1）。</summary>
    public int PlatedArmorAmount => BasePlatedArmor + UpgradeCount;

    // 灼热攻击式：可被多次升级。
    public override int MaxUpgradeLevel => 99;

    public override bool GainsBlock => true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailArmorUpgrade"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Support, CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("BlockAmount", BlockAmount),
        new IntVar("PlatedArmorAmount", PlatedArmorAmount)
    ];

    public flametailArmorUpgrade() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 召唤瑕光（char_423_blemsh 专属 Spine 召唤场景）。
        var impactTcs = new TaskCompletionSource();
        Owner.Creature.GetVfxContainer()?.AddChildSafely(SummonedAllyVfx.Create(
            Owner.Creature,
            null,
            $"{Entry.ResPath}/scenes/vfx/summons/armor_upgrade_summon.tscn",
            impactTcs,
            aoe: true));
        await impactTcs.Task;

        // 支援：格挡不受敏捷、脆弱等能力影响。
        await CreatureCmd.GainBlock(Owner.Creature, BlockAmount, ValueProp.Move | ValueProp.Unpowered, cardPlay);
        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            Owner.Creature,
            PlatedArmorAmount,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 灼热攻击式递增升级：每次升级 +1 次已升级次数，从而抬升格挡/覆甲数值。
        // 显示变量的刷新由 UpgradeCount setter 统一处理。
        UpgradeCount++;
    }

    /// <summary>进入战斗时同步一次动态变量，保证读档恢复后卡面显示值与升级次数一致。</summary>
    public override async Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this)
        {
            SyncDynamicVars();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 把格挡/覆甲的当前属性值写回动态变量（仅影响卡面显示，实际数值在 OnPlay 中用属性实时计算）。
    /// 调用时机：UpgradeCount setter（覆盖本地升级、对端反序列化恢复、读档恢复）、
    /// OnUpgrade、AfterCardEnteredCombat。DynamicVars 尚未初始化（早期反序列化）时跳过，
    /// 此时随后必有 AfterCardEnteredCombat 兜底刷新。
    /// </summary>
    private void SyncDynamicVars()
    {
        if (DynamicVars == null)
        {
            return;
        }

        if (DynamicVars.ContainsKey("BlockAmount"))
        {
            DynamicVars["BlockAmount"].BaseValue = BlockAmount;
        }

        if (DynamicVars.ContainsKey("PlatedArmorAmount"))
        {
            DynamicVars["PlatedArmorAmount"].BaseValue = PlatedArmorAmount;
        }
    }

    /// <summary>
    /// 进入休息点时，此牌自动升级一次。
    /// <para>
    /// 多人安全：AfterRoomEntered 由房间进入流程在每个客户端对称触发一次，
    /// 两端各自对镜像的卡牌执行一次相同的确定性升级，状态保持一致。
    /// </para>
    /// <para>
    /// 防重入守卫：CardCmd.Upgrade 会把本卡 Id 记入当前地图点历史的 UpgradedCards
    /// （该历史随存档/联机状态持久化）。若在休息点内存档后重新进入同一地图点
    /// （房间恢复会重跑进入流程），历史中已存在本卡的记录，此时跳过自动升级，
    /// 避免每次读档多升一级。正常首次进入时记录不存在，行为不变。
    /// </para>
    /// </summary>
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is RestSiteRoom)
        {
            List<ModelId>? upgradedCards = Owner.RunState?.CurrentMapPointHistoryEntry?.GetEntry(Owner.NetId).UpgradedCards;
            if (upgradedCards == null || !upgradedCards.Contains(Id))
            {
                CardCmd.Upgrade(this);
            }
        }

        await Task.CompletedTask;
    }
}
