using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Cards;

/// <summary>
/// 所有“能够获得步法”的卡牌生成器，供随机添加此类卡牌的效果使用。
/// 与 <see cref="IGainFootworkCard"/> 保持一致，仅剔除初始牌（如 迅敏）。
/// </summary>
public static class flametailFootworkCardRegistry
{
    private static readonly IReadOnlyList<Func<CardModel>> _creators = new List<Func<CardModel>>
    {
        // 注意：初始卡（如 迅敏）不应出现在随机生成池中。
        () => ModelDb.Card<flametailSideStep>(),
        () => ModelDb.Card<flametailBackstep>(),
        () => ModelDb.Card<flametailFlip>(),
        () => ModelDb.Card<flametailFeint>(),
        () => ModelDb.Card<flametailPreFightWarmup>(),
        () => ModelDb.Card<flametailGaleDash>(),
        () => ModelDb.Card<flametailRegainPosture>(),
        () => ModelDb.Card<flametailMeteorTail>(),
        () => ModelDb.Card<flametailRideTheMomentum>(),
        () => ModelDb.Card<flametailWhirlingAttack>(),
        () => ModelDb.Card<flametailEvenFaster>(),
        () => ModelDb.Card<flametailDashWeaveDodge>(),
    };

    /// <summary>
    /// 步法卡生成器集合。只读，避免外部意外修改。
    /// </summary>
    public static IReadOnlyList<Func<CardModel>> Creators => _creators;
}
