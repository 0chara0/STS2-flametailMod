using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Cards;

/// <summary>
/// 所有“反制”牌生成器，供随机生成反制牌的效果使用。
/// </summary>
public static class flametailCounterCardRegistry
{
    public static readonly List<Func<CardModel>> Creators = new()
    {
        () => ModelDb.Card<flametailBackstep>(),
        () => ModelDb.Card<flametailBlisteringCounter>(),
        () => ModelDb.Card<flametailCandleFlash>(),
        () => ModelDb.Card<flametailCrossguardArts>(),
        () => ModelDb.Card<flametailDeftAssault>(),
        () => ModelDb.Card<flametailDisarmingAttack>(),
        () => ModelDb.Card<flametailExploitOpening>(),
        () => ModelDb.Card<flametailFeatherSupport>(),
        () => ModelDb.Card<flametailFlourish>(),
        () => ModelDb.Card<flametailJab>(),
        () => ModelDb.Card<flametailKnightsJoust>(),
        () => ModelDb.Card<flametailMeteorTail>(),
        () => ModelDb.Card<flametailRideTheMomentum>(),
        () => ModelDb.Card<flametailThrust>(),
        () => ModelDb.Card<flametailTurningAttack>(),
    };
}
