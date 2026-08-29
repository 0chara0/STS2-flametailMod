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
/// 盔甲改良：召唤瑕光，为你提供 4 点格挡（每次升级额外 +2,+3,+4…）和 4(+已升级次数) 层覆甲。
/// 灼热攻击式递增升级（可多次升级）；进入休息点时自动升级一次。支援、保留、消耗。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailArmorUpgrade : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;
    private const int BaseBlock = 4;
    private const int BasePlatedArmor = 4;

    private int _upgradeCount;

    /// <summary>已升级次数（灼热攻击式递增升级的核心，[SavedProperty] 持久化）。</summary>
    [SavedProperty]
    public int UpgradeCount
    {
        get => _upgradeCount;
        set
        {
            AssertMutable();
            _upgradeCount = value;
            // 同步动态变量，保证显示值与持久化值一致（参考旧 PlatedArmorAmount/BlockAmount 模式）。
            if (DynamicVars != null)
            {
                DynamicVars["BlockAmount"].BaseValue = BlockAmount;
                DynamicVars["PlatedArmorAmount"].BaseValue = PlatedArmorAmount;
            }
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
        new[] { FlametailKeywords.Support, CardKeyword.Retain, CardKeyword.Exhaust };

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
        UpgradeCount++;
    }

    /// <summary>进入休息点时，此牌自动升级一次。</summary>
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is RestSiteRoom)
        {
            CardCmd.Upgrade(this);
        }

        await Task.CompletedTask;
    }
}
