using System.Collections.Generic;
using System.Linq;
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
    /// 本战斗已触发的反制次数（用于渐入佳境）。
    /// </summary>
    public static int CountersTriggeredThisCombat { get; set; }

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
                foreach (Creature enemy in state.Enemies.Where(e => e != null && !e.IsDead))
                {
                    yield return enemy;
                }
            }

            yield break;
        }

        if (attacker != null)
        {
            yield return attacker;
        }
    }

    /// <summary>
    /// 从手牌中找出第一张反制牌。
    /// </summary>
    public static CardModel? FindFirstCounterCard(Creature owner)
    {
        return owner.Player?.PlayerCombatState?.Hand.Cards
            .FirstOrDefault(c => c is ICounterCard);
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
                CountersTriggeredThisCombat++;
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
        var next = currentCard.Owner.PlayerCombatState?.Hand.Cards
            .FirstOrDefault(c => c != currentCard
                && c is ICounterCard counter
                && counter.CanAutoPlayAsCounter(CurrentAttacker));

        if (next != null)
        {
            await PlayCounterCard(choiceContext, next, target);
        }
    }
}
