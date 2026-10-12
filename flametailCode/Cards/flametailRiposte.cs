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
using MegaCrit.Sts2.Core.HoverTips;

namespace flametail.Cards;

/// <summary>
/// 回击：无法主动点击打出，由反制流程或釜底抽薪/重演等自动打出效果打出。
/// 打出时从手牌或抽牌堆中选择（升级版）/随机（基础版）一张牌打出并消耗；
/// 若打出的是反制牌，则按反制打出并触发其反制效果。反制流程外打出时没有攻击者语义。
/// 回击本体打出时在手牌/弃牌堆被选中时可能包含自身以外的任何牌，无需额外排除。
/// 升级效果：随机 → 选择（修改文档 [11][12]）。
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

    // 保留词条已按文档移除（修改文档 [11]）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    public bool HasCounterEffect => true;

    // 任何方式打出（受击反制、百战先锋重放、釜底抽薪/重演等）都会执行反制效果，
    // 百战先锋据此把反制阶段外的回击打出也计入重放队列。
    public bool CounterEffectTriggersOnAnyPlay => true;

    // 无法主动点击打出，只能被自动打出（反制流程，或釜底抽薪/重演等自动打出效果）。
    protected override bool IsPlayable => false;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[] {
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    };

    public flametailRiposte() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 任何方式打出（受击反制、百战先锋重放、釜底抽薪/重演等）都执行效果。
        // 反制流程外没有攻击者，随机取一个可攻击敌人作为攻击者（与引擎 AutoPlay
        // 在 target=null 时的随机目标规则一致），保证打出的牌及其反制效果都有目标。
        var counterContext = this.GetCounterContext();
        Creature? attacker = counterContext.IsCounterPlay
            ? counterContext.CurrentAttacker
            : cardPlay.Target;

        if (attacker == null && Owner.Creature.CombatState is { } combatState)
        {
            attacker = Owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
        }

        await TriggerCounterEffect(choiceContext, cardPlay, attacker);
    }

    /// <summary>
    /// 反制时：打出并消耗手牌或抽牌堆中的一张牌（基础版随机、升级版由你选择）。
    /// 选中的是反制牌时，以反制上下文打出（触发其反制效果）；否则按普通自动打出结算。
    /// 打出前抽牌堆与手牌均为空且弃牌堆有牌时，先洗牌再取抽牌堆候选。
    /// </summary>
    public async Task TriggerCounterEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? attacker)
    {
        // 候选：手牌 + 抽牌堆（修改文档 [11]：可选择范围增加手牌区）。
        List<CardModel> candidates = new();
        var hand = Owner.PlayerCombatState?.Hand;
        var drawPile = Owner.PlayerCombatState?.DrawPile;
        if (hand != null)
        {
            candidates.AddRange(hand.Cards);
        }

        // 自动打出的回击本体在 Play 堆天然不在手牌候选里，无需排除。
        if (drawPile != null)
        {
            candidates.AddRange(drawPile.Cards);
        }

        // 抽牌堆与手牌均为空 → 先洗牌再重取抽牌堆（修改文档 [11] 优化项）。
        if (candidates.Count == 0)
        {
            await CardPileCmd.ShuffleIfNecessary(choiceContext, Owner);
            drawPile = Owner.PlayerCombatState?.DrawPile;
            candidates.Clear();
            if (drawPile != null)
            {
                candidates.AddRange(drawPile.Cards);
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        CardModel? chosen;
        if (IsUpgraded)
        {
            // 升级版：由你选择。
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
            // 基础版：随机。
            chosen = Owner.RunState!.Rng.CombatCardSelection.NextItem(candidates);
        }

        if (chosen == null)
        {
            return;
        }

        // 打出并消耗（修改文档 [11]）：与釜底抽薪一致的一次性消耗标志。
        // 反制牌若自带返回去向（如破隙），其 ModifyCardPlayResultLocation 会覆写，
        // 不为此专门适配（批复 5）。
        chosen.ExhaustOnNextPlay = true;

        // 与百战先锋一致：先移回手牌顶部再自动打出，保证 AutoPlay 正常工作。
        if (chosen.Pile?.Type != PileType.Hand)
        {
            await CardPileCmd.Add(chosen, PileType.Hand, CardPilePosition.Top, this);
        }

        if (chosen is ICounterCard)
        {
            // 以反制上下文打出（与 CounterSystem.PlayCounterCard 一致），触发其反制效果。
            await CounterSystem.PlayCounterCard(choiceContext, chosen, attacker);
        }
        else
        {
            await CardCmd.AutoPlay(choiceContext, chosen, attacker, AutoPlayType.Default);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级效果：随机 → 选择（见 TriggerCounterEffect 内 IsUpgraded 分支）。
    }
}
