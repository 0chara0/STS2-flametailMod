using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 历战形态（原百战先锋，修改文档：重做并更名）：
/// 每回合开始时，你可以选择场上（手牌、抽牌堆和弃牌堆）的 N 张反制牌，将其视作反制打出
/// （N=能力层数，可叠加；2026-10-21 用户裁定）。
/// <para>
/// 选择可跳过；抽牌堆/弃牌堆中的牌会先移回手牌顶再打出。打出经
/// <see cref="CounterSystem.PlayCounterCard"/> 以反制上下文执行——剑如火舞重定向、
/// 渐入佳境计数等由其统一处理，该牌的反制效果与打出效果正常结算。
/// 回合开始没有攻击者，按回击先例随机取一个可攻击敌人作为反制对象。
/// </para>
/// <para>
/// 耗能递减部分（“这张牌在手牌中时，每反制一次，其耗能-1”）由卡牌本体
/// <see cref="Cards.flametailHardenedVanguard"/> 的 <c>BeforeCardPlayed</c> 实现，不在本能力内。
/// </para>
/// </summary>
[RegisterPower]
public sealed class flametailHardenedVanguardPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        // 候选：场上（手牌 + 抽牌堆 + 弃牌堆）中的所有反制牌。
        var hand = player.PlayerCombatState?.Hand;
        var drawPile = player.PlayerCombatState?.DrawPile;
        var discardPile = player.PlayerCombatState?.DiscardPile;

        List<CardModel> candidates = new();
        if (hand != null)
        {
            candidates.AddRange(hand.Cards.Where(card => card is ICounterCard));
        }

        if (drawPile != null)
        {
            candidates.AddRange(drawPile.Cards.Where(card => card is ICounterCard));
        }

        if (discardPile != null)
        {
            candidates.AddRange(discardPile.Cards.Where(card => card is ICounterCard));
        }

        if (candidates.Count == 0)
        {
            return;
        }

        // 可跳过（“你可以选择”）；可选张数=能力层数（叠加生效）。
        int selectCount = Math.Max(1, (int)Amount);
        var prefs = new CardSelectorPrefs(
            new LocString("powers", "FLAMETAIL_VETERAN_FORM_PROMPT"),
            0,
            selectCount)
        {
            Cancelable = true,
        };

        List<CardModel> chosenCards = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            player,
            prefs)).ToList();

        // 回合开始没有真实攻击发生：反制上下文的受击分支标志（LastAttackMitigated 等）
        // 是上一次攻击的残留或 false——破隙的反制分支据此判定"未满足打出条件"直接自弃。
        // 这里预置为有效反制状态（2026-10-21 用户裁定：破隙经历战形态打出应该有效）。
        var context = CounterSystem.GetContext(Owner);
        context.LastAttackMitigated = true;
        context.TookDamage = false;
        context.WasDodged = false;

        foreach (CardModel chosen in chosenCards)
        {
            // 抽牌堆/弃牌堆中的牌先移回手牌顶再打出（与百战先锋重放的处理一致）。
            if (chosen.Pile?.Type != PileType.Hand)
            {
                await CardPileCmd.Add(chosen, PileType.Hand, CardPilePosition.Top, this);
            }

            // 回合开始没有攻击者：按回击先例随机取一个可攻击敌人作为反制对象（2026-10-21 用户裁定）。
            // 剑格术/缴械攻击等以 CurrentAttacker 为目标的反制效果才能结算；
            // 无可攻击敌人时传 null，反制增伤部分按旧守卫跳过（剑如火舞重定向不受影响）。
            Creature? counterTarget = null;
            if (Owner.CombatState is { } combat && combat.HittableEnemies.Any())
            {
                counterTarget = player.RunState!.Rng.CombatTargets.NextItem(combat.HittableEnemies);
            }

            await CounterSystem.PlayCounterCard(choiceContext, chosen, counterTarget);
        }
    }
}
