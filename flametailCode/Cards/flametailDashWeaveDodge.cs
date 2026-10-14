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
using flametail.Keywords;

namespace flametail.Cards;

/// <summary>
/// 闪！转！腾！挪！（修改清单 [14] 重做）：抽 2 张牌，获得 2 点能量。
/// 本回合，当你交替打出攻击牌和技能牌时，每打出一张牌，抽 1 张牌，获得 1 点能量。
/// 效果由 <see cref="flametailDashWeaveDodgePower"/> 实现。消耗。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailDashWeaveDodge : ModCardTemplate
{
    private const int BaseEnergyCost = 3;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{nameof(flametailDashWeaveDodge)}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(2),
        // 触发奖励的能量独立成变量：正文第二处 {TriggerEnergy:energyIcons()} 是 1，
        // 不能复用 {Energy}（会显示成 2）。
        new EnergyVar("TriggerEnergy", 1)
    ];

    public flametailDashWeaveDodge() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, 2, Owner);

        await PlayerCmd.GainEnergy(DynamicVars["Energy"].IntValue, Owner);

        await PowerCmd.Apply<flametailDashWeaveDodgePower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
