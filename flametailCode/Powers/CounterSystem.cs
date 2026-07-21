using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Powers;

/// <summary>
/// 全局反制系统状态与辅助方法。
/// </summary>
public static class CounterSystem
{
    private static readonly AsyncLocal<bool> IsCounterPlayLocal = new();
    private static readonly AsyncLocal<Creature?> CurrentAttackerLocal = new();
    private static readonly AsyncLocal<bool> WasAttackDodgedLocal = new();
    private static readonly AsyncLocal<bool> LastAttackMitigatedLocal = new();
    private static readonly AsyncLocal<bool> IsHardenedVanguardReplicationLocal = new();
    private static readonly AsyncLocal<bool> ShouldRetargetCounterToAllEnemiesLocal = new();

    /// <summary>
    /// 当前是否正在由反制系统自动打出卡牌。
    /// </summary>
    public static bool IsCounterPlay
    {
        get => IsCounterPlayLocal.Value;
        set => IsCounterPlayLocal.Value = value;
    }

    /// <summary>
    /// 当前正在处理的那次攻击的攻击者。
    /// 仅在 <see cref="IsCounterPlay"/> 期间有效。
    /// </summary>
    public static Creature? CurrentAttacker
    {
        get => CurrentAttackerLocal.Value;
        set => CurrentAttackerLocal.Value = value;
    }

    /// <summary>
    /// 当前正在处理的那次攻击是否被闪避。
    /// </summary>
    public static bool WasAttackDodged
    {
        get => WasAttackDodgedLocal.Value;
        set => WasAttackDodgedLocal.Value = value;
    }

    /// <summary>
    /// 当前正在处理的那次攻击是否被闪避或完全格挡（即是否被完全抵消）。
    /// </summary>
    public static bool LastAttackMitigated
    {
        get => LastAttackMitigatedLocal.Value;
        set => LastAttackMitigatedLocal.Value = value;
    }

    /// <summary>
    /// 是否正在执行百战先锋的第二次反制复制，避免无限循环与重复计数。
    /// </summary>
    public static bool IsHardenedVanguardReplication
    {
        get => IsHardenedVanguardReplicationLocal.Value;
        set => IsHardenedVanguardReplicationLocal.Value = value;
    }

    /// <summary>
    /// 当前反制是否应重定向到所有敌人（剑如火舞）。
    /// </summary>
    public static bool ShouldRetargetCounterToAllEnemies
    {
        get => ShouldRetargetCounterToAllEnemiesLocal.Value;
        set => ShouldRetargetCounterToAllEnemiesLocal.Value = value;
    }

    /// <summary>
    /// 获取本次反制应当生效的目标集合。
    /// </summary>
    public static IEnumerable<Creature> GetCounterTargets(Creature? attacker, ICombatState combat)
    {
        if (ShouldRetargetCounterToAllEnemies && attacker != null)
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

        bool oldIsCounterPlay = IsCounterPlay;
        Creature? oldAttacker = CurrentAttacker;
        bool oldDodged = WasAttackDodged;
        bool oldMitigated = LastAttackMitigated;
        bool oldRetarget = ShouldRetargetCounterToAllEnemies;
        bool oldReplication = IsHardenedVanguardReplication;
        IsCounterPlay = true;
        CurrentAttacker = target;
        ShouldRetargetCounterToAllEnemies = card.Owner.Creature.HasPower<flametailFireDancingSwordPower>();

        try
        {
            await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default);
        }
        finally
        {
            if (!IsHardenedVanguardReplication)
            {
                var manager = GetManager(card.Owner.Creature);
                if (manager != null)
                {
                    manager.CountersTriggeredThisCombat++;
                }
            }

            IsCounterPlay = oldIsCounterPlay;
            CurrentAttacker = oldAttacker;
            WasAttackDodged = oldDodged;
            LastAttackMitigated = oldMitigated;
            ShouldRetargetCounterToAllEnemies = oldRetarget;
            IsHardenedVanguardReplication = oldReplication;
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
                && counter.CanAutoPlayAsCounter(CurrentAttacker))
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
