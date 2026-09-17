using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 领悟：能力牌。你每生成一张牌，随机将手牌中一张有费用的牌的耗能变为 0。
/// 未升级与升级后施加两个相互独立的能力（<see cref="flametailInsightPower"/> /
/// <see cref="flametailInsightUpgradedPower"/>），同类可叠加、不同类互不混合；
/// 未升级仅本回合，升级后持续到本场战斗结束。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailInsight : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailInsight"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<flametailInsightPower>("Amount", 1m)
    ];

    public flametailInsight() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 未升级与升级后施加不同的能力：同类之间正常叠加，不同类互不混合。
        if (IsUpgraded)
        {
            await PowerCmd.Apply<flametailInsightUpgradedPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars["Amount"].BaseValue,
                Owner.Creature,
                this);
        }
        else
        {
            await PowerCmd.Apply<flametailInsightPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars["Amount"].BaseValue,
                Owner.Creature,
                this);
        }
    }
}
