using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 反制管理器：回合结束时让玩家选择保留至多 N 张反制牌，并在玩家受攻击时自动打出反制牌。
/// 这是一个隐藏 Buff，由初始遗物在战斗开始时施加。
/// </summary>
[RegisterPower]
public sealed class flametailCounterManagerPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png");

    /// <summary>
    /// 使用 BeforeFlushLate，让玩家在看到其他 BeforeFlush 效果后再决定保留哪张反制牌。
    /// </summary>
    public override async Task BeforeFlushLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (player.PlayerCombatState?.Hand is not { } hand)
        {
            return;
        }

        var counterCards = hand.Cards
            .Where(c => c is ICounterCard)
            .ToList();

        if (counterCards.Count == 0)
        {
            return;
        }

        int extraRetain = (Owner.GetPower<flametailExtraCounterRetainPower>()?.Amount ?? 0)
                        + (Owner.GetPower<flametailForesightPower>()?.Amount ?? 0);
        int retainLimit = Amount + extraRetain;
        if (retainLimit <= 0)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(
            new LocString("powers", "FLAMETAIL_COUNTER_RETAIN_PROMPT"),
            0,
            retainLimit)
        {
            Cancelable = true,
        };

        IEnumerable<CardModel> selected = await CardSelectCmd.FromHand(
            choiceContext,
            player,
            prefs,
            c => c is ICounterCard,
            this);

        foreach (CardModel card in selected)
        {
            card.GiveSingleTurnRetain();
            // 把保留的反制牌移到手牌序列最后，让 UI 顺序与后端顺序一致，
            // 这样它不会插队到其他反制牌前面。
            hand.MoveToBottomInternal(card);
        }
    }

    public override async Task AfterApplied(Creature? owner, CardModel? source)
    {
        CounterSystem.CountersTriggeredThisCombat = 0;
        await Task.CompletedTask;
    }

    /// <summary>
    /// 在受到伤害后触发反制。此时闪避已经被消耗，可以正确读取 WasAttackDodged。
    /// </summary>
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner)
        {
            return;
        }

        if (!props.IsPoweredAttack())
        {
            return;
        }

        if (dealer == null || !dealer.IsEnemy)
        {
            return;
        }

        if (CounterSystem.IsCounterPlay)
        {
            return;
        }

        bool oldMitigated = CounterSystem.LastAttackMitigated;
        bool oldDodged = CounterSystem.WasAttackDodged;
        Creature? oldAttacker = CounterSystem.CurrentAttacker;
        CounterSystem.LastAttackMitigated = CounterSystem.WasAttackDodged || result.WasFullyBlocked;
        CounterSystem.CurrentAttacker = dealer;

        try
        {
            CardModel? counterCard = Owner.Player?.PlayerCombatState?.Hand.Cards
                .FirstOrDefault(c => c is ICounterCard counter && counter.CanAutoPlayAsCounter(dealer));

            if (counterCard == null)
            {
                return;
            }

            await CounterSystem.PlayCounterCard(choiceContext, counterCard, dealer);
        }
        finally
        {
            CounterSystem.LastAttackMitigated = oldMitigated;
            CounterSystem.WasAttackDodged = oldDodged;
            CounterSystem.CurrentAttacker = oldAttacker;
        }
    }
}
