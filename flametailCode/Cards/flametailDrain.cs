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

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailDrain"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public flametailDrain() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = Owner.PlayerCombatState?.Hand;
        var drawPile = Owner.PlayerCombatState?.DrawPile;
        int estimatedCount = (hand?.Cards.Count ?? 0) + (drawPile?.Cards.Count ?? 0);
        List<CardModel> candidates = new(estimatedCount);

        if (hand != null)
        {
            foreach (CardModel card in hand.Cards)
            {
                candidates.Add(card);
            }
        }

        if (drawPile != null)
        {
            foreach (CardModel card in drawPile.Cards)
            {
                candidates.Add(card);
            }
        }

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
            PretendCardsCanBePlayed = true,
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

        int playCount = 2;
        var dodgePower = Owner.Creature.GetPower<flametailDodgePower>();
        if (dodgePower != null && dodgePower.Amount > 0)
        {
            // 每有 1 层闪避额外打出 1 次，随后扣光闪避。
            playCount = 2 + (int)dodgePower.Amount;
            await PowerCmd.ModifyAmount(choiceContext, dodgePower, -dodgePower.Amount, Owner.Creature, this);
        }

        await PowerCmd.Apply<flametailDrainEchoPower>(
            choiceContext,
            Owner.Creature,
            playCount - 1,
            Owner.Creature,
            this);

        // 与「破灭」对齐：设置一次性 ExhaustOnNextPlay 标志，打出后直接进消耗堆，无需二次 Exhaust。
        targetCard.ExhaustOnNextPlay = true;
        await CardCmd.AutoPlay(choiceContext, targetCard, null);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
