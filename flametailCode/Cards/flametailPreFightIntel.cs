using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailPreFightIntel : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AllEnemies;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailPreFightIntel"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("WeakAmount", 1),
        new IntVar("VulnerableAmount", 1)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Innate, CardKeyword.Exhaust };

    public flametailPreFightIntel() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var enemies = Owner.Creature.CombatState?.HittableEnemies;
        if (enemies == null)
        {
            return;
        }

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            enemies,
            DynamicVars["WeakAmount"].IntValue,
            Owner.Creature,
            this);

        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            enemies,
            DynamicVars["VulnerableAmount"].IntValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["WeakAmount"].UpgradeValueBy(1);
        DynamicVars["VulnerableAmount"].UpgradeValueBy(1);
    }
}
