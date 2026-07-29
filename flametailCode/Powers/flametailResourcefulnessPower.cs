using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using flametail.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 八面玲珑：在你的回合开始时，从固定反制牌池随机生成一张反制牌加入手牌。
/// </summary>
[RegisterPower]
public sealed class flametailResourcefulnessPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailResourcefulnessPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailResourcefulnessPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (flametailCounterCardRegistry.Creators.Count == 0)
        {
            return;
        }

        Func<CardModel> creator = player.RunState!.Rng.CombatCardSelection.NextItem(flametailCounterCardRegistry.Creators)!;
        CardModel card = player.Creature.CombatState!.CreateCard(creator(), player);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player, CardPilePosition.Top);
    }
}
