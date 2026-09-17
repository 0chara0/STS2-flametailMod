using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace flametail.Powers;

/// <summary>
/// 标记一张牌为“反制”牌。CounterManagerPower 会识别这些牌并处理保留 / 自动打出。
/// </summary>
public interface ICounterCard
{
    /// <summary>
    /// 在 CounterManagerPower 自动打出这张牌前调用。
    /// 返回 false 则跳过该牌（例如仅在本次攻击被闪避/完全格挡时才触发的牌）。
    /// </summary>
    bool CanAutoPlayAsCounter(Creature? attacker) => true;

    /// <summary>
    /// 是否可以被“支援/先发制人”等额外反制触发效果打出。
    /// 返回 false 时这些效果不会选择该牌，但仍会被正常的受击反制流程打出。
    /// </summary>
    bool CanBePlayedBySupportEffects => true;

    /// <summary>
    /// 是否有独立的“反制时：”专属效果。
    /// 骑士对决等“额外触发反制效果”的效果只对这类牌生效；
    /// 纯反制牌（反制 = 普通效果）不受影响。
    /// </summary>
    bool HasCounterEffect => false;

    /// <summary>
    /// 是否任何方式打出该牌都会触发反制效果（不依赖反制上下文或骑士对决）。
    /// 例如回击：无法主动打出，只能由反制流程或釜底抽薪/重演等自动打出效果打出，
    /// 打出即执行反制效果。百战先锋据此把反制阶段外的这类打出也计入重放队列。
    /// </summary>
    bool CounterEffectTriggersOnAnyPlay => false;

    /// <summary>
    /// 反制效果是否为“改为”（替换基础效果）而非“额外”（附加在基础效果之上）。
    /// 为 true 时，骑士对决在主动打出触发该牌反制效果的同时会抑制基础效果（“改为”语义）；
    /// 为 false 时基础效果照常执行，反制效果作为额外效果附加。
    /// 注：作为反制自动打出（IsCounterPlay）时基础效果本就由 OnPlay 的分支跳过，无需此标志。
    /// </summary>
    bool CounterEffectReplacesBaseEffect => false;

    /// <summary>
    /// 执行“反制时：”专属效果（不重跑基础效果）。
    /// 受击自动反制时，由 <c>OnPlay</c> 内的 <see cref="CounterContext.IsCounterPlay"/> 分支调用，
    /// <paramref name="attacker"/> 为当前攻击者；
    /// 骑士对决主动打出时直接调用，<paramref name="attacker"/> 为打出时的目标（指向攻击者语义）。
    /// 默认无专属效果（纯反制牌），有“反制时：”效果的牌覆盖此方法。
    /// </summary>
    Task TriggerCounterEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? attacker)
        => Task.CompletedTask;
}
