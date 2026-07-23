using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailKindling : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AllEnemies;
    private const bool ShowInCardLibrary = true;

    private int _extraDamage;

    [SavedProperty]
    public int ExtraDamage
    {
        get => _extraDamage;
        set
        {
            AssertMutable();
            _extraDamage = value;
            DynamicVars.Damage.BaseValue = 1 + _extraDamage;
        }
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailKindling"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1 + ExtraDamage, ValueProp.Move),
        new IntVar("HitCount", 2)
    ];

    public flametailKindling() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(Owner.Creature.CombatState!)
            .WithHitCount(DynamicVars["HitCount"].IntValue)
            .Execute(choiceContext);

        AddDamage(1);
        (DeckVersion as flametailKindling)?.AddDamage(1);
    }

    public void AddDamage(int amount)
    {
        ExtraDamage += amount;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HitCount"].UpgradeValueBy(1);
    }
}
