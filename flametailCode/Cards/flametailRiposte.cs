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
/// 回击：无法主动点击打出，由反制流程或釜底抽薪/重演等自动打出效果打出。
/// 打出时选择抽牌堆中的一张牌并打出；若打出的是反制牌，则按反制打出并触发其
/// 反制效果（打出的牌不消耗）。反制流程外打出时没有攻击者语义。
/// 回击本体打出时在手牌/弃牌堆被选中时可能包含自身以外的任何牌，无需额外排除。
/// 升级后添加“保留”词条（基础版在手牌中回合结束会被弃置，升级版可跨回合保留）。
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

    // 保留由升级通过 AddKeyword 添加，基础版不带。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    public bool HasCounterEffect => true;

    // 任何方式打出（受击反制、百战先锋重放、釜底抽薪/重演等）都会执行反制效果，
    // 百战先锋据此把反制阶段外的回击打出也计入重放队列。
    public bool CounterEffectTriggersOnAnyPlay => true;

    // 无法主动点击打出，只能被自动打出（反制流程，或釜底抽薪/重演等自动打出效果）。
    protected override bool IsPlayable => false;

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
    /// 反制时：打出抽牌堆中你选择的一张牌（不消耗）。
    /// 选中的是反制牌时，以反制上下文打出（触发其反制效果）；否则按普通自动打出结算。
    /// 回击本体打出时在手牌中，抽牌堆里不会包含自身，无需排除。
    /// </summary>
    public async Task TriggerCounterEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? attacker)
    {
        // 收集抽牌堆中的所有牌。
        List<CardModel> candidates = new();
        var drawPile = Owner.PlayerCombatState?.DrawPile;
        if (drawPile != null)
        {
            candidates.AddRange(drawPile.Cards);
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(
            new LocString("cards", "FLAMETAIL_RIPOSTE_PROMPT"),
            1,
            1)
        {
            Cancelable = true,
        };

        CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            Owner,
            prefs)).FirstOrDefault();

        if (chosen == null)
        {
            return;
        }

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
        // 升级后添加“保留”词条。
        AddKeyword(CardKeyword.Retain);
    }
}
