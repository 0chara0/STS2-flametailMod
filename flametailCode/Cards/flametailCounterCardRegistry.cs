using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Cards;

/// <summary>
/// 所有“反制”牌生成器，供随机生成反制牌的效果使用。
/// </summary>
public static class flametailCounterCardRegistry
{
    private static readonly IReadOnlyList<Func<CardModel>> _creators = new List<Func<CardModel>>
    {
        () => ModelDb.Card<flametailBackstep>(),
        // 烛火一闪不进随机反制生成池：它是即逝+濒死反制机制，被随机到手牌只会立即消失（修改文档 [31][32][33]）。
        () => ModelDb.Card<flametailCrossguardArts>(),
        () => ModelDb.Card<flametailDisarmingAttack>(),
        () => ModelDb.Card<flametailExploitOpening>(),
        () => ModelDb.Card<flametailFeatherSupport>(),
        () => ModelDb.Card<flametailKnightsJoust>(),
        () => ModelDb.Card<flametailMeteorTail>(),
        () => ModelDb.Card<flametailRideTheMomentum>(),
        () => ModelDb.Card<flametailRiposte>(),
        () => ModelDb.Card<flametailThrust>(),
        () => ModelDb.Card<flametailTurningAttack>(),
    };

    /// <summary>
    /// 反制牌生成器集合。只读，避免外部意外修改。
    /// </summary>
    public static IReadOnlyList<Func<CardModel>> Creators => _creators;
}
