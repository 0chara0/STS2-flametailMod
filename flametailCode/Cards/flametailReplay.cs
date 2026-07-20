using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailReplay : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public flametailReplay() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> candidates = Owner.PlayerCombatState?.DiscardPile.Cards
            .Where(c => IsUpgraded || c.Type == CardType.Attack)
            .ToList() ?? new List<CardModel>();

        if (candidates.Count == 0)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(
            new LocString("cards", "FLAMETAIL_REPLAY_PROMPT"),
            0,
            1)
        {
            Cancelable = true,
        };

        IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            Owner,
            prefs);

        CardModel? chosen = selected.FirstOrDefault();
        if (chosen == null)
        {
            return;
        }

        await CardPileCmd.Add(chosen, PileType.Hand, CardPilePosition.Top);
        await CardCmd.AutoPlay(choiceContext, chosen, null, AutoPlayType.Default);
        await CardCmd.Exhaust(choiceContext, chosen);
    }
}
