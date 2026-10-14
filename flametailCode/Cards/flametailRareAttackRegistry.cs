using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Cards;

public static class flametailRareAttackRegistry
{
    // 修改清单 [16]：烛火一闪是《归来》的专属反制牌，不参与随机生成（已移除）。
    public static readonly List<Func<CardModel>> Creators = new()
    {
        () => ModelDb.Card<flametailCannonSupport>(),
        () => ModelDb.Card<flametailLanceSupport>(),
        () => ModelDb.Card<flametailFeatherSupport>(),
        () => ModelDb.Card<flametailMeteorTail>(),
        () => ModelDb.Card<flametailHittingStride>(),
        () => ModelDb.Card<flametailFluidMotion>(),
        () => ModelDb.Card<flametailNocturnalBrightPower>(),
    };
}
