using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using flametail.Cards;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Potions;

/// <summary>
/// 反制药水：从 3 张随机的反制牌中选择一张加入你的手牌，这张牌在本回合保留且耗能变为 0。
/// </summary>
[RegisterPotion(typeof(flametailPotionPool))]
public sealed class flametailCounterPotion : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.Self;

    private static readonly PotionAssetProfile _assetProfile = new(
        ImagePath: $"{Entry.ResPath}/images/relics/flametailRelic.png",
        OutlinePath: $"{Entry.ResPath}/images/relics/flametailRelic.png");
    public override PotionAssetProfile AssetProfile => _assetProfile;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        // 从反制牌注册表中抽 3 张去重的随机牌，各造一张。
        List<Func<CardModel>> candidates = flametailCounterCardRegistry.Creators.ToList();
        var choices = new List<CardModel>();
        while (choices.Count < 3 && candidates.Count > 0)
        {
            Func<CardModel> creator = Owner.RunState!.Rng.CombatCardSelection.NextItem(candidates)!;
            candidates.Remove(creator);
            choices.Add(Owner.Creature.CombatState!.CreateCard(creator(), Owner));
        }

        if (choices.Count == 0)
        {
            return;
        }

        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: false);
        if (chosen == null)
        {
            return;
        }

        // 本回合保留 + 耗能变为 0。
        chosen.GiveSingleTurnRetain();
        chosen.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }
}
