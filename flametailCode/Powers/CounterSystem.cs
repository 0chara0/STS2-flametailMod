using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Powers;

/// <summary>
/// 单次反制流程的上下文。所有状态都绑定到 <see cref="ICombatState"/>，
/// 不再使用 <see cref="System.Threading.AsyncLocal{T}"/>，以避免异步边界、多人联机和读档时的状态丢失。
/// </summary>
public sealed class CounterContext
{
    /// <summary>
    /// 当前是否正在由反制系统自动打出卡牌。
    /// </summary>
    public bool IsCounterPlay { get; set; }

    /// <summary>
    /// 当前正在处理的那次攻击的攻击者。
    /// 仅在 <see cref="IsCounterPlay"/> 期间有效。
    /// </summary>
    public Creature? CurrentAttacker { get; set; }

    /// <summary>
    /// 当前正在处理的那次攻击是否被完全抵消（闪避或完全格挡）。
    /// </summary>
    public bool LastAttackMitigated { get; set; }

    /// <summary>
    /// 当前正在处理的那次攻击是否让你实际受到了伤害（伤害 &gt; 0）。
    /// 闪避、完全格挡、或攻击伤害本身为 0 时都为 false。
    /// </summary>
    public bool TookDamage { get; set; }

    /// <summary>
    /// 是否正在执行百战先锋的第二次反制复制，避免无限循环与重复计数。
    /// </summary>
    public bool IsHardenedVanguardReplication { get; set; }

    /// <summary>
    /// 是否正在执行骑士对决对主动打出反制牌的复制，避免与百战先锋等互相递归。
    /// </summary>
    public bool IsKnightsDuelReplication { get; set; }

    /// <summary>
    /// 当前反制是否应重定向到所有敌人（剑如火舞）。
    /// </summary>
    public bool ShouldRetargetCounterToAllEnemies { get; set; }

    /// <summary>
    /// 当前即将被打出的“改为”类反制牌，其基础效果应被替换（不执行）。
    /// 由骑士对决在 <c>BeforeCardPlayed</c> 时根据本张牌的触发条件设置，
    /// “改为”类反制牌的 <c>OnPlay</c> 读取后跳过基础效果；
    /// 反制效果本身仍由骑士对决在 <c>AfterCardPlayedLate</c> 触发。
    /// </summary>
    public bool SuppressBaseEffect { get; set; }
}

/// <summary>
/// 全局反制系统状态与辅助方法。所有状态按 <see cref="ICombatState"/> 隔离，
/// 战斗结束时自动清理。
/// </summary>
public static class CounterSystem
{
    private static readonly ConditionalWeakTable<ICombatState, CounterContext> _contexts = new();

    static CounterSystem()
    {
        CombatManager.Instance.CombatEnded += room =>
        {
            if (room?.CombatState != null)
            {
                _contexts.Remove(room.CombatState);
            }
        };
    }

    /// <summary>
    /// 获取指定战斗的反制上下文。如果 combat 为 null，返回一个临时的独立上下文，
    /// 修改不会保存到任何战斗中。
    /// </summary>
    public static CounterContext GetContext(ICombatState? combat)
    {
        if (combat == null)
        {
            return new CounterContext();
        }

        if (!_contexts.TryGetValue(combat, out var context))
        {
            context = new CounterContext();
            _contexts.Add(combat, context);
        }

        return context;
    }

    /// <summary>
    /// 通过生物获取其所在战斗的反制上下文。
    /// </summary>
    public static CounterContext GetContext(Creature? creature) => GetContext(creature?.CombatState);

    /// <summary>
    /// 强制移除指定战斗的上下文。通常由战斗结束事件自动处理。
    /// </summary>
    public static void ResetContext(ICombatState combat) => _contexts.Remove(combat);

    /// <summary>
    /// 获取本次反制应当生效的目标集合。
    /// </summary>
    public static IEnumerable<Creature> GetCounterTargets(Creature? attacker, ICombatState combat)
    {
        var context = GetContext(combat);
        if (context.ShouldRetargetCounterToAllEnemies && attacker != null)
        {
            if (combat is CombatState state)
            {
                foreach (Creature enemy in state.Enemies)
                {
                    if (enemy != null && !enemy.IsDead)
                    {
                        yield return enemy;
                    }
                }
            }

            yield break;
        }

        if (attacker is { IsDead: false })
        {
            yield return attacker;
        }
    }

    private static flametailCounterManagerPower? GetManager(Creature owner)
    {
        return owner.GetPower<flametailCounterManagerPower>();
    }

    /// <summary>
    /// 获取当前战斗中已触发的反制次数（用于渐入佳境）。
    /// </summary>
    public static int GetCountersTriggeredThisCombat(Creature owner)
    {
        return GetManager(owner)?.CountersTriggeredThisCombat ?? 0;
    }

    /// <summary>
    /// 从手牌中找出第一张反制牌。
    /// </summary>
    public static CardModel? FindFirstCounterCard(Creature owner)
    {
        var hand = owner.Player?.PlayerCombatState?.Hand;
        if (hand == null)
        {
            return null;
        }

        foreach (CardModel card in hand.Cards)
        {
            if (card is ICounterCard)
            {
                return card;
            }
        }

        return null;
    }

    /// <summary>
    /// 自动打出指定反制牌。
    /// </summary>
    public static async Task PlayCounterCard(PlayerChoiceContext choiceContext, CardModel card, Creature? target)
    {
        if (CombatManager.Instance.IsOverOrEnding || (card.Owner.Creature?.IsDead ?? true))
        {
            return;
        }

        var context = GetContext(card.Owner.Creature);

        bool oldIsCounterPlay = context.IsCounterPlay;
        Creature? oldAttacker = context.CurrentAttacker;
        bool oldMitigated = context.LastAttackMitigated;
        bool oldTookDamage = context.TookDamage;
        bool oldRetarget = context.ShouldRetargetCounterToAllEnemies;
        bool oldReplication = context.IsHardenedVanguardReplication;

        context.IsCounterPlay = true;
        context.CurrentAttacker = target;
        context.ShouldRetargetCounterToAllEnemies = card.Owner.Creature.HasPower<flametailFireDancingSwordPower>();

        try
        {
            await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default);
        }
        finally
        {
            // 每次反制自动打出都是一次“发动”，即使它发生在百战先锋复制期间也要计入。
            // 百战先锋自身的复制会由该能力在复制完成后另行计入一次。
            var manager = GetManager(card.Owner.Creature);
            if (manager != null)
            {
                manager.CountersTriggeredThisCombat++;
            }

            context.IsCounterPlay = oldIsCounterPlay;
            context.CurrentAttacker = oldAttacker;
            context.LastAttackMitigated = oldMitigated;
            context.TookDamage = oldTookDamage;
            context.ShouldRetargetCounterToAllEnemies = oldRetarget;
            context.IsHardenedVanguardReplication = oldReplication;
        }
    }

    /// <summary>
    /// 打出当前手牌中的下一张反制牌（排除 currentCard 本身）。
    /// </summary>
    public static async Task PlayNextCounterCard(PlayerChoiceContext choiceContext, CardModel currentCard, Creature? target)
    {
        var hand = currentCard.Owner.PlayerCombatState?.Hand;
        if (hand == null)
        {
            return;
        }

        CardModel? next = null;
        foreach (CardModel card in hand.Cards)
        {
            if (card != currentCard
                && card is ICounterCard counter
                && counter.CanAutoPlayAsCounter(target)
                && counter.CanBePlayedBySupportEffects)
            {
                next = card;
                break;
            }
        }

        if (next != null)
        {
            await PlayCounterCard(choiceContext, next, target);
        }
    }
}

/// <summary>
/// 方便卡牌和能力从所有者快速获取反制上下文的扩展方法。
/// </summary>
public static class CounterSystemExtensions
{
    /// <summary>
    /// 获取这张牌所在战斗的反制上下文。
    /// </summary>
    public static CounterContext GetCounterContext(this CardModel card)
    {
        return CounterSystem.GetContext(card.Owner?.Creature);
    }

    /// <summary>
    /// 获取该能力所在战斗的反制上下文。
    /// </summary>
    public static CounterContext GetCounterContext(this PowerModel power)
    {
        return CounterSystem.GetContext(power.Owner);
    }
}
