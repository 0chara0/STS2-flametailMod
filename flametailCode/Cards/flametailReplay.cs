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

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailReplay"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public flametailReplay() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardPile discardPile = PileType.Discard.GetPile(Owner);

        var prefs = new CardSelectorPrefs(
            new LocString("cards", "FLAMETAIL_REPLAY_PROMPT"),
            1);

        CardModel? chosen = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            discardPile,
            Owner,
            prefs,
            card => IsUpgraded || card.Type == CardType.Attack)).FirstOrDefault();

        if (chosen == null)
        {
            return;
        }

        await CardPileCmd.Add(chosen, PileType.Hand, CardPilePosition.Top);
        await CardCmd.AutoPlay(choiceContext, chosen, null, AutoPlayType.Default);
        await CardCmd.Exhaust(choiceContext, chosen);
    }
}
