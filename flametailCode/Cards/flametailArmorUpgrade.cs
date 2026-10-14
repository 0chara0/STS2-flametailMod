using System.Collections.Generic;
using System.Linq;
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
using MegaCrit.Sts2.Core.HoverTips;

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

    /// <summary>
    /// 已升级次数（修改清单 [17] BUG2 修复）。
    /// 旧实现把 UpgradeCount 做成 [SavedProperty] 并在 OnUpgrade 里自增——但引擎反序列化
    /// （读档与联机同步共用 CardModel.FromSerializable）会先恢复 SavedProperties，再按
    /// CurrentUpgradeLevel 重放 UpgradeInternal→OnUpgrade，导致计数每次反序列化翻倍
    /// （实测升 1 次后本地 3、远端 4）。改为直接派生自引擎自带的 CurrentUpgradeLevel
    /// （它本身就随 SerializableCard 序列化/联机同步），不再有任何自持状态。
    /// </summary>
    /// <remarks>
    /// 兼容垫片：eb5e0b6 时代的存档 Props 里残留 "UpgradeCount" 条目，引擎
    /// SavedProperties.FillInternal 按属性名反射 SetValue——只读属性会抛
    /// ArgumentException("Property set method not found")，且 FromSerializable 无
    /// try/catch，读档直接崩溃。私有 setter 吞掉旧值即可：旧值本来就是被翻倍的脏数据，
    /// 显示值始终以 CurrentUpgradeLevel 为准。
    /// </remarks>
    public int UpgradeCount
    {
        get => CurrentUpgradeLevel;
        private set { /* 旧存档残留值，忽略 */ }
    }

    /// <summary>格挡 = 3 + 累加增量（第 n 次升级 +n+1，即 +2,+3,+4…）。</summary>
    public int BlockAmount => BaseBlock + UpgradeCount * (UpgradeCount + 3) / 2;

    /// <summary>覆甲 = 2 + 已升级次数（每次 +1）。</summary>
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

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[] {
        HoverTipFactory.FromPower<PlatingPower>()
    };

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
        // 灼热攻击式递增升级：升级次数由 CurrentUpgradeLevel 派生（见 UpgradeCount），
        // 这里只负责刷新卡面显示变量（读档/联机同步的重放路径也会经过这里）。
        SyncDynamicVars();
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
    /// 防重入守卫（修改清单 [17] BUG1 修复）：CardCmd.Upgrade 会把本卡 Id 记入当前
    /// 地图点历史的 UpgradedCards（随存档/联机状态持久化）。同一卡牌类的所有副本
    /// 共享同一个 ModelId，旧实现的 Contains(Id) 会让第二张开始的副本全部被跳过。
    /// 改为计数比较：历史中该 Id 的记录数 &lt; 卡组内本卡副本数 ⇒ 本副本尚未自动升级。
    /// 读档重入同一休息点时记录数已 ≥ 副本数，所有副本都会正确跳过，不会多升。
    /// </para>
    /// </summary>
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is RestSiteRoom)
        {
            List<ModelId>? upgradedCards = Owner.RunState?.CurrentMapPointHistoryEntry?.GetEntry(Owner.NetId).UpgradedCards;
            if (upgradedCards != null)
            {
                int copies = PileType.Deck.GetPile(Owner).Cards.Count(c => c is flametailArmorUpgrade);
                int recorded = upgradedCards.Count(id => id == Id);
                if (recorded < copies)
                {
                    CardCmd.Upgrade(this);
                }
            }
        }

        await Task.CompletedTask;
    }
}
