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
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 回击：无法主动打出。反制时，触发抽牌堆或弃牌堆中随机（升级前）/ 你选择（升级后）
/// 的一张反制牌的反制效果，并将其消耗。排除自身与其它回击，避免自我递归。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailRiposte : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = -1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailRiposte"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Retain, FlametailKeywords.Counter };

    public bool HasCounterEffect => true;

    // 无法主动打出，只能作为反制牌被自动打出。
    protected override bool IsPlayable => false;

    public flametailRiposte() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 只有作为反制自动打出时才执行效果。
        var counterContext = this.GetCounterContext();
        if (!counterContext.IsCounterPlay)
        {
            return;
        }

        await TriggerCounterEffect(choiceContext, cardPlay, counterContext.CurrentAttacker);
    }

    /// <summary>
    /// 反制时：触发抽牌堆或弃牌堆中随机（升级前）/ 你选择（升级后）的一张反制牌的反制效果，并将其消耗。
    /// 排除自身与其它回击，避免自我递归。
    /// </summary>
    public async Task TriggerCounterEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? attacker)
    {
        // 收集抽牌堆 + 弃牌堆中的反制牌（排除自身与其它回击，避免自我递归）。
        List<CardModel> candidates = new();
        CollectCounterCards(Owner.PlayerCombatState?.DrawPile, candidates);
        CollectCounterCards(Owner.PlayerCombatState?.DiscardPile, candidates);

        if (candidates.Count == 0)
        {
            return;
        }

        CardModel? chosen;
        if (IsUpgraded)
        {
            var prefs = new CardSelectorPrefs(
                new LocString("cards", "FLAMETAIL_RIPOSTE_PROMPT"),
                1,
                1)
            {
                Cancelable = true,
            };

            chosen = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                candidates,
                Owner,
                prefs)).FirstOrDefault();
        }
        else
        {
            var rng = Owner.RunState?.Rng.CombatCardSelection;
            if (rng == null)
            {
                return;
            }

            chosen = rng.NextItem(candidates);
        }

        if (chosen == null)
        {
            return;
        }

        // 以反制上下文触发目标牌的反制效果（与 CounterSystem.PlayCounterCard 一致），随后将其消耗。
        await CounterSystem.PlayCounterCard(choiceContext, chosen, attacker);
        await CardCmd.Exhaust(choiceContext, chosen);
    }

    private void CollectCounterCards(CardPile? pile, List<CardModel> into)
    {
        if (pile == null)
        {
            return;
        }

        foreach (CardModel card in pile.Cards)
        {
            // 排除自身与其它回击实例，避免自我递归。
            if (card == this || card is flametailRiposte)
            {
                continue;
            }

            if (card is ICounterCard)
            {
                into.Add(card);
            }
        }
    }
}
