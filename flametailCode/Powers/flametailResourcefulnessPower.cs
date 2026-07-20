using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 八面玲珑：回合开始时，优先从抽牌堆，否则从弃牌堆，将一张随机反制牌放入手牌。
/// </summary>
[RegisterPower]
public sealed class flametailResourcefulnessPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailResourcefulnessPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailResourcefulnessPower.png");

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        var state = player.PlayerCombatState;
        if (state == null)
        {
            return;
        }

        List<CardModel> counters = state.DrawPile.Cards
            .Where(c => c is ICounterCard)
            .ToList();

        if (counters.Count == 0)
        {
            counters = state.DiscardPile.Cards
                .Where(c => c is ICounterCard)
                .ToList();
        }

        if (counters.Count == 0)
        {
            return;
        }

        CardModel chosen = player.RunState!.Rng.CombatCardSelection.NextItem(counters)!;
        await CardPileCmd.Add(chosen, PileType.Hand, CardPilePosition.Top, this);
    }
}
