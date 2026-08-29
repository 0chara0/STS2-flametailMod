using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using flametail.Cards;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Relics;

/// <summary>
/// 《归来》：每场战斗开始时，向你的手牌中加入一张随机的反制牌，该牌在本场战斗中保留。
/// 给生成的牌加 Retain 关键字实现整场战斗保留；若该牌随出牌离开手牌，则不再返回，符合「保留」语义。
/// </summary>
[RegisterRelic(typeof(flametailRelicPool))]
public sealed class flametailTheReturn : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    private static readonly RelicAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailTheReturn)}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{nameof(flametailTheReturn)}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailTheReturn)}.png");
    public override RelicAssetProfile AssetProfile => _assetProfile;

    public override async Task BeforeCombatStart()
    {
        Func<CardModel> creator = Owner.RunState!.Rng.CombatCardSelection.NextItem(flametailCounterCardRegistry.Creators)!;
        CardModel card = Owner.Creature.CombatState!.CreateCard(creator(), Owner);
        card.AddKeyword(CardKeyword.Retain);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }
}
