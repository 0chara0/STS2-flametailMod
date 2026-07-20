using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

[RegisterPower]
public sealed class flametailReflexMovePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailReflexMovePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailReflexMovePower.png");

    public override async Task BeforeFlushLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        var candidates = player.PlayerCombatState?.Hand.Cards
            .Where(c => c is IGainFootworkCard)
            .ToList();

        if (candidates == null || candidates.Count == 0)
        {
            return;
        }

        CardModel chosen = player.RunState!.Rng.CombatCardSelection.NextItem(candidates)!;
        await CardCmd.AutoPlay(choiceContext, chosen, null, AutoPlayType.Default);
    }
}
