using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailDrain : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public flametailDrain() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> candidates = new();
        candidates.AddRange(Owner.PlayerCombatState?.Hand.Cards ?? Enumerable.Empty<CardModel>());
        candidates.AddRange(Owner.PlayerCombatState?.DrawPile.Cards ?? Enumerable.Empty<CardModel>());

        if (candidates.Count == 0)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(
            new LocString("cards", "FLAMETAIL_DRAIN_PROMPT"),
            1,
            1)
        {
            Cancelable = true,
        };

        IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            Owner,
            prefs);

        CardModel? targetCard = selected.FirstOrDefault();
        if (targetCard == null)
        {
            return;
        }

        if (targetCard.Pile?.Type != PileType.Hand)
        {
            await CardPileCmd.Add(targetCard, PileType.Hand, CardPilePosition.Top, this);
        }

        targetCard.SetToFreeThisTurn();

        int playCount = 2;
        var dodgePower = Owner.Creature.GetPower<flametailDodgePower>();
        if (dodgePower != null && dodgePower.Amount > 0)
        {
            playCount = 3;
            await PowerCmd.ModifyAmount(choiceContext, dodgePower, -dodgePower.Amount, Owner.Creature, this);
        }

        for (int i = 0; i < playCount; i++)
        {
            if (targetCard.Pile?.Type != PileType.Hand)
            {
                await CardPileCmd.Add(targetCard, PileType.Hand, CardPilePosition.Top, this);
            }

            await CardCmd.AutoPlay(choiceContext, targetCard, null, AutoPlayType.Default);
        }

        await CardCmd.Exhaust(choiceContext, targetCard);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
