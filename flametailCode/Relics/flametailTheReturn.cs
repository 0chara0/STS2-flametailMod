using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using flametail.Cards;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Relics;

/// <summary>
/// 《归来》（修改文档 [4]）：每回合开始时，向你的手牌中加入一张随机的反制牌，
/// 该牌会被消耗（Exhaust 关键词）且在本回合保留（GiveSingleTurnRetain，非整场 Retain）。
/// </summary>
[RegisterRelic(typeof(flametailRelicPool))]
public sealed class flametailTheReturn : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    private static readonly RelicAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailTheReturn)}.png",
        // 描边图：遗物图鉴按所属角色池染主题色，需用独立轮廓剪影图（_r4 为选定的描边版本，r3/r5 备选）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{nameof(flametailTheReturn)}_r4.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailTheReturn)}.png");
    public override RelicAssetProfile AssetProfile => _assetProfile;

    // 每回合开始（与八面玲珑同款 hook）而不是每场战斗开始。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || (player.Creature?.IsDead ?? true))
        {
            return;
        }

        if (flametailCounterCardRegistry.Creators.Count == 0)
        {
            return;
        }

        Func<CardModel> creator = Owner.RunState!.Rng.CombatCardSelection.NextItem(flametailCounterCardRegistry.Creators)!;
        CardModel card = Owner.Creature.CombatState!.CreateCard(creator(), Owner);

        // “该牌会被消耗”：加 Exhaust 关键词（打出后进消耗堆，幂等不与自带消耗的牌冲突）。
        card.AddKeyword(CardKeyword.Exhaust);
        // “在本回合保留”：单回合保留（打出前不会被回合结束弃置），不用整场 Retain 词条。
        card.GiveSingleTurnRetain();

        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }
}
