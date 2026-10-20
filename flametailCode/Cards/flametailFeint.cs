using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailFeint : ModCardTemplate, IGainFootworkCard
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailFeint"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Footwork };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Footwork", 1)
    ];

    /// <summary>
    /// 本回合是否已经打出过此牌（每张独立判定，非同名牌——修改文档 [9]）。
    /// 用回合号比对实现自动过回合清零，无需回合开始钩子；瞬态不序列化（读档后重置可接受）。
    /// </summary>
    private int _lastDrawTurn;

    /// <summary>
    /// 本回合尚未首次打出时金色高亮（类似取回的条件高亮：可抽牌时提示）。
    /// </summary>
    protected override bool ShouldGlowGoldInternal =>
        CombatState != null
        && Owner.PlayerCombatState is { } pcs
        && _lastDrawTurn != pcs.TurnNumber;

    // ⚠ 不设 GainsBlock：本牌只给步法不给格挡，true 会被灵巧附魔错误附魔出假显示
    // （见 flametailWindbringer 注释）。
    public flametailFeint() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<flametailFootworkPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Footwork"].IntValue,
            Owner.Creature,
            this);

        // 每回合首次打出此牌时，抽 1 张牌（对单张牌实例判定）。
        if (Owner.PlayerCombatState is { } pcs && _lastDrawTurn != pcs.TurnNumber)
        {
            _lastDrawTurn = pcs.TurnNumber;
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }

        // 未升级：下 1 张攻击牌无视格挡；升级后：本回合所有攻击牌无视格挡。
        var ignoreBlock = await PowerCmd.Apply<flametailIgnoreBlockPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);
        if (ignoreBlock != null)
        {
            ignoreBlock.IgnoreAllAttacks = IsUpgraded;
        }
    }

    protected override void OnUpgrade()
    {
        // 升级不再增加层数，而是由 OnPlay 在施加能力时切换到「本回合全部」模式。
    }
}
