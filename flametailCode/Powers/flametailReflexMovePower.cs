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

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailReflexMovePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailReflexMovePower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task BeforeFlushLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        var hand = player.PlayerCombatState?.Hand;
        if (hand == null)
        {
            return;
        }

        var candidates = new List<CardModel>(hand.Cards.Count);
        foreach (CardModel card in hand.Cards)
        {
            if (card is IGainFootworkCard)
            {
                candidates.Add(card);
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        CardModel chosen = player.RunState!.Rng.CombatCardSelection.NextItem(candidates)!;
        await CardCmd.AutoPlay(choiceContext, chosen, null, AutoPlayType.Default);
    }
}
