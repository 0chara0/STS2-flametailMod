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
/// 体力分配：获得 3(4) 点能量，每有 2 层步法减少 1 点获得的能量（最少 0）。消耗。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailPhysicalAllocation : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailPhysicalAllocation"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(3),
        new IntVar("FootworkPerEnergy", 2)
    ];

    public flametailPhysicalAllocation() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal footwork = Owner.Creature.GetPower<flametailFootworkPower>()?.Amount ?? 0m;
        int reduction = (int)(footwork / DynamicVars["FootworkPerEnergy"].IntValue);

        int energyGain = Math.Max(0, DynamicVars["Energy"].IntValue - reduction);
        if (energyGain > 0)
        {
            await PlayerCmd.GainEnergy(energyGain, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级：能量 3→4。
        DynamicVars["Energy"].UpgradeValueBy(1);
    }
}
