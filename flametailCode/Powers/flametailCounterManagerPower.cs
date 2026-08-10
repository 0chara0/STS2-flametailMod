using System.Collections.Generic;
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
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using flametail.Patches;

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

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    [SavedProperty]
    public int CountersTriggeredThisCombat { get; set; }

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

        var counterCards = new List<CardModel>(hand.Cards.Count);
        foreach (CardModel card in hand.Cards)
        {
            if (card is ICounterCard)
            {
                counterCards.Add(card);
            }
        }

        if (counterCards.Count == 0)
        {
            return;
        }

        // 如果本回合有“保留所有反制牌”效果，直接跳过选择阶段并保留全部。
        if (Owner.GetPower<flametailRetainAllCountersPower>() is not null)
        {
            foreach (CardModel card in counterCards)
            {
                card.GiveSingleTurnRetain();
                // 把保留的反制牌移到手牌序列最后，让 UI 顺序与后端顺序一致，
                // 这样它不会插队到其他反制牌前面。
                hand.MoveToBottomInternal(card);
            }

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
        CountersTriggeredThisCombat = 0;
        await Task.CompletedTask;
    }

    /// <summary>
    /// 在受到伤害后触发反制。此时闪避已经被消耗，可以正确读取 DodgeBlockPatch.WasAttackDodged。
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

        var context = this.GetCounterContext();
        if (context.IsCounterPlay)
        {
            return;
        }

        bool oldMitigated = context.LastAttackMitigated;
        Creature? oldAttacker = context.CurrentAttacker;
        bool oldTookDamage = context.TookDamage;
        bool dodged = DodgeBlockPatch.WasAttackDodged(target);
        bool fullyBlocked = result.WasFullyBlocked;
        context.LastAttackMitigated = dodged || fullyBlocked;
        // 只有实际受到伤害（伤害 > 0）才为 true；闪避、完全格挡、或攻击伤害为 0 都视为未受伤。
        context.TookDamage = !dodged && !fullyBlocked && result.TotalDamage > 0;
        context.CurrentAttacker = dealer;

        try
        {
            CardModel? counterCard = null;
            var hand = Owner.Player?.PlayerCombatState?.Hand;
            if (hand != null)
            {
                foreach (CardModel card in hand.Cards)
                {
                    if (card is ICounterCard counter && counter.CanAutoPlayAsCounter(dealer))
                    {
                        counterCard = card;
                        break;
                    }
                }
            }

            if (counterCard == null)
            {
                return;
            }

            await CounterSystem.PlayCounterCard(choiceContext, counterCard, dealer);
        }
        finally
        {
            context.LastAttackMitigated = oldMitigated;
            context.CurrentAttacker = oldAttacker;
            context.TookDamage = oldTookDamage;
        }
    }
}
