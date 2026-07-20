using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Cards;

public static class flametailRareAttackRegistry
{
    public static readonly List<Func<CardModel>> Creators = new()
    {
        () => ModelDb.Card<flametailCannonSupport>(),
        () => ModelDb.Card<flametailLanceSupport>(),
        () => ModelDb.Card<flametailFeatherSupport>(),
        () => ModelDb.Card<flametailMeteorTail>(),
        () => ModelDb.Card<flametailHittingStride>(),
        () => ModelDb.Card<flametailFluidMotion>(),
        () => ModelDb.Card<flametailNocturnalBrightPower>(),
        () => ModelDb.Card<flametailCandleFlash>(),
    };
}
