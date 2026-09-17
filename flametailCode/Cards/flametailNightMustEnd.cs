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
/// 长夜需尽：能力牌。每回合开始时，向你手牌添加一张[睡眠不佳]，抽 1（升级 2）张牌，
/// 然后由你选择消耗一张手牌（原版选牌页面），获得 1 点能量。效果由 <see cref="flametailNightMustEndPower"/> 实现。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailNightMustEnd : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailNightMustEnd"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每层能力 = 每回合开始时额外抽的牌数（参考熟稔 flametailProficiency 的层数用法）。
        new PowerVar<flametailNightMustEndPower>("Amount", 1m),
        // 固定获得的 1 点能量，用于描述中的 {Energy:energyIcons()} 能量图标渲染。
        new EnergyVar(1)
    ];

    public flametailNightMustEnd() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<flametailNightMustEndPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Amount"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 升级：每回合开始时抽 1 → 2 张。
        DynamicVars["Amount"].UpgradeValueBy(1);
    }
}
