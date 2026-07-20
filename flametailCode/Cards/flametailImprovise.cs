using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailImprovise : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public flametailImprovise() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> choices = new()
        {
            Owner.Creature.CombatState!.CreateCard<Finesse>(Owner),
            Owner.Creature.CombatState!.CreateCard<Impatience>(Owner),
            Owner.Creature.CombatState!.CreateCard<ThinkingAhead>(Owner),
        };

        foreach (CardModel choice in choices.Where(c => !c.Keywords.Contains(CardKeyword.Exhaust)))
        {
            choice.AddKeyword(CardKeyword.Exhaust);
        }

        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            choices,
            Owner,
            canSkip: false);

        if (chosen == null)
        {
            return;
        }

        chosen.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
