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
public sealed class flametailPSLeader : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Retain, CardKeyword.Exhaust };

    public flametailPSLeader() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> choices = new()
        {
            Owner.Creature.CombatState!.CreateCard<flametailCannonSupport>(Owner),
            Owner.Creature.CombatState!.CreateCard<flametailLanceSupport>(Owner),
            Owner.Creature.CombatState!.CreateCard<flametailFeatherSupport>(Owner),
        };

        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            choices,
            Owner,
            canSkip: false);

        if (chosen == null)
        {
            return;
        }

        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
