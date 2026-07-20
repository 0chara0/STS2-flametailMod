using MegaCrit.Sts2.Core.Entities.Creatures;

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
}
