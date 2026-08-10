using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 献身：从手牌、抽牌堆和弃牌堆中消耗至多 2 张诅咒或状态牌，再将一张执迷加入弃牌堆。
/// 升级后最多可消耗 3 张。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailSacrifice : ModCardTemplate
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[] {
        HoverTipFactory.FromCard<Enthralled>()
    };
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{nameof(flametailSacrifice)}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("ExhaustAmount", 2)
    ];

    public flametailSacrifice() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = Owner.PlayerCombatState?.Hand;
        var drawPile = Owner.PlayerCombatState?.DrawPile;
        var discard = Owner.PlayerCombatState?.DiscardPile;
        int estimatedCount = (hand?.Cards.Count ?? 0) + (drawPile?.Cards.Count ?? 0) + (discard?.Cards.Count ?? 0);
        List<CardModel> candidates = new(estimatedCount);

        foreach (CardModel card in hand?.Cards ?? [])
        {
            if (card.Type == CardType.Curse || card.Type == CardType.Status)
            {
                candidates.Add(card);
            }
        }

        foreach (CardModel card in drawPile?.Cards ?? [])
        {
            if (card.Type == CardType.Curse || card.Type == CardType.Status)
            {
                candidates.Add(card);
            }
        }

        foreach (CardModel card in discard?.Cards ?? [])
        {
            if (card.Type == CardType.Curse || card.Type == CardType.Status)
            {
                candidates.Add(card);
            }
        }

        if (candidates.Count > 0)
        {
            int max = DynamicVars["ExhaustAmount"].IntValue;
            var prefs = new CardSelectorPrefs(
                new LocString("cards", "FLAMETAIL_SACRIFICE_PROMPT"),
                0,
                max)
            {
                Cancelable = true,
            };

            IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                candidates,
                Owner,
                prefs);

            foreach (CardModel card in selected)
            {
                await CardCmd.Exhaust(choiceContext, card);
            }
        }

        // 无论是否消耗了诅咒或状态牌，都将一张执迷加入弃牌堆。
        CardModel enthralled = Owner.Creature.CombatState!.CreateCard<Enthralled>(Owner);
        CardPileAddResult enthralledResult = await CardPileCmd.AddGeneratedCardToCombat(enthralled, PileType.Discard, Owner);
        CardCmd.PreviewCardPileAdd(enthralledResult);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ExhaustAmount"].UpgradeValueBy(1);
    }
}
