using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailSingleFile : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("VulnerableAmount", 2),
        new IntVar("WeakAmount", 2)
    ];

    public flametailSingleFile() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars["VulnerableAmount"].IntValue,
            Owner.Creature,
            this);

        var otherEnemies = Owner.Creature.CombatState?.Enemies
            .Where(e => e != cardPlay.Target)
            .ToList();

        if (otherEnemies is { Count: > 0 })
        {
            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                otherEnemies,
                DynamicVars["WeakAmount"].IntValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
