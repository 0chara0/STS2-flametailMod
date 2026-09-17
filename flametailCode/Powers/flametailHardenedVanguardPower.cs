using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 百战先锋：每层使一次“反制效果触发”对应的反制牌被额外重放一次，重放按“先到先重放”的队列处理。
/// <para>
/// 反制触发的检测覆盖所有场景，不限于反制阶段的自动打出：
/// ① 反制阶段打出的反制牌（受击反制、先发制人、烛火一闪、回击选中的反制牌等）；
/// ② 任何方式打出的回击（打出即执行反制效果，含釜底抽薪/重演等自动打出效果打出）；
/// ③ 骑士对决主动打出反制牌时触发的“反制时：”效果（含该牌链出的后续反制牌）。
/// 反制牌被主动打出但反制效果未触发时（如无骑士对决时的普通打出）不计数。
/// </para>
/// <para>
/// 本回合每次检测到反制触发，对应反制牌按打出顺序进入重放队列（含重放自身
/// 引出的新链——例如链 A→B→C 结束后依次重放 A、B、C；重放 A 触发的
/// 新链 A'→D→E 中的 D、E 排在队列尾部，等 A、B、C 都重放完后再依次重放）。
/// 每重放一张消耗 1 层，层数耗尽即停止，剩余队列作废——因此天然无无限循环。
/// </para>
/// <para>
/// 队列的处理时机：反制链是嵌套结算的（起始牌在打出过程中触发下一张反制牌，
/// 越早打出的牌越晚结算完成），所以等队列首位那张牌自身的
/// <see cref="AfterCardPlayedLate"/> 触发（即整条链——含期间排队的牌——结算到
/// 最外层）时才开始逐个重放。重放以反制上下文执行（经
/// <see cref="CounterSystem.PlayCounterCard"/>，攻击者取记录时的目标），
/// 其触发的后续反制牌会继续入队。
/// </para>
/// <para>
/// 反制牌即使结算中自行消耗（如卸武攻势的反制、烛火一闪）也会被取回重放——
/// 只要反制效果被触发过就该重放。被重放的那张牌自身不会再次入队。
/// </para>
/// </summary>
[RegisterPower]
public sealed class flametailHardenedVanguardPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _remainingThisTurn;

    /// <summary>
    /// 待重放的反制牌队列（FIFO，按打出顺序）。链起始牌结算完成时开始
    /// 逐个出队重放；重放引出的新反制牌继续排在队尾。
    /// 攻击者取记录该牌打出时的目标（<see cref="CardPlay.Target"/>），
    /// 重放时作为反制上下文的攻击者传入；为 null 时由打出流程自行取目标。
    /// </summary>
    private readonly List<(CardModel Card, Creature? Attacker)> _pendingReplays = new();

    /// <summary>
    /// 当前正在重放的那张牌。重放自身的打出不会再次入队，
    /// 保证每张牌不会被连续重放两次。
    /// </summary>
    private CardModel? _replayingCard;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        _remainingThisTurn = Amount;
        _pendingReplays.Clear();
        _replayingCard = null;
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power != this || amount <= 0)
        {
            return Task.CompletedTask;
        }

        _remainingThisTurn += (int)amount;
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        if (_remainingThisTurn <= 0)
        {
            return Task.CompletedTask;
        }

        // 全面覆盖反制触发场景：反制阶段打出的反制牌、任何方式打出的回击、
        // 骑士对决主动打出将触发的“反制时：”效果。
        var counterContext = this.GetCounterContext();
        bool isCounterTrigger =
            (counterContext.IsCounterPlay && cardPlay.Card is ICounterCard)
            || cardPlay.Card is ICounterCard { CounterEffectTriggersOnAnyPlay: true }
            || Owner.GetPower<flametailKnightsDuelPower>()?.WouldTriggerCounterEffect(cardPlay) == true;

        if (!isCounterTrigger)
        {
            return Task.CompletedTask;
        }

        if (cardPlay.Card == _replayingCard || _pendingReplays.Exists(e => e.Card == cardPlay.Card))
        {
            // 重放自身的打出不再入队，保证同一张牌不会被连续重放两次。
            return Task.CompletedTask;
        }

        // 按打出顺序入队（含重放期间打出的新反制牌，它们排在队尾）。
        _pendingReplays.Add((cardPlay.Card, cardPlay.Target));
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        // 只有队列首位（最早记录、尚未重放的那张）结算完成时才开始重放；
        // 嵌套的后续反制牌和重放期间的打出都由这一次统一处理。
        if (_pendingReplays.Count == 0 || cardPlay.Card != _pendingReplays[0].Card || _replayingCard != null)
        {
            return;
        }

        try
        {
            // 逐个出队重放；重放引出的新反制牌会排到队尾继续处理（BFS），
            // 直到层数耗尽或队列清空。
            while (_pendingReplays.Count > 0 && _remainingThisTurn > 0)
            {
                (CardModel card, Creature? attacker) = _pendingReplays[0];
                _pendingReplays.RemoveAt(0);
                _remainingThisTurn--;

                _replayingCard = card;
                try
                {
                    // 无论该牌结算后去了哪个牌堆（弃牌堆、消耗堆、回到手牌）都取回重放。
                    if (card.Pile?.Type != PileType.Hand)
                    {
                        await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, this);
                    }

                    // 以反制上下文重放（经 PlayCounterCard）：反制效果拿到攻击者语义，
                    // 剑如火舞重定向、渐入佳境计数均由其统一处理。
                    // 记录时的攻击者已死亡则传 null，由打出流程自行取目标。
                    if (attacker is { IsDead: true })
                    {
                        attacker = null;
                    }

                    await CounterSystem.PlayCounterCard(choiceContext, card, attacker);
                }
                finally
                {
                    _replayingCard = null;
                }
            }

            // 层数耗尽后队列中剩余的牌作废。
            _pendingReplays.Clear();
        }
        finally
        {
            _replayingCard = null;
        }
    }
}
