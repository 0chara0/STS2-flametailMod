using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Cards;

/// <summary>
/// 所有“能够获得步法”的卡牌生成器，供随机添加此类卡牌的效果使用。
/// </summary>
public static class flametailFootworkCardRegistry
{
    public static readonly List<Func<CardModel>> Creators = new()
    {
        () => ModelDb.Card<flametailSwift>(),
        () => ModelDb.Card<flametailSideStep>(),
        () => ModelDb.Card<flametailBackstep>(),
        () => ModelDb.Card<flametailFlip>(),
        () => ModelDb.Card<flametailFeint>(),
        () => ModelDb.Card<flametailPreFightWarmup>(),
        () => ModelDb.Card<flametailGaleDash>(),
        () => ModelDb.Card<flametailRegainPosture>(),
    };
}
