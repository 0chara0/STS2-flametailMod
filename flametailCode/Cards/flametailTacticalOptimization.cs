using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailTacticalOptimization : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("ExhaustAmount", 3)
    ];

    public flametailTacticalOptimization() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> attacks = new();
        attacks.AddRange(Owner.PlayerCombatState?.Hand.Cards.Where(c => c.Type == CardType.Attack) ?? Enumerable.Empty<CardModel>());
        attacks.AddRange(Owner.PlayerCombatState?.DiscardPile.Cards.Where(c => c.Type == CardType.Attack) ?? Enumerable.Empty<CardModel>());

        if (attacks.Count == 0)
        {
            return;
        }

        int max = DynamicVars["ExhaustAmount"].IntValue;
        var prefs = new CardSelectorPrefs(
            new LocString("cards", "FLAMETAIL_TACTICAL_OPTIMIZATION_PROMPT"),
            0,
            max)
        {
            Cancelable = true,
        };

        IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            attacks,
            Owner,
            prefs);

        foreach (CardModel card in selected)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ExhaustAmount"].UpgradeValueBy(2);
    }
}