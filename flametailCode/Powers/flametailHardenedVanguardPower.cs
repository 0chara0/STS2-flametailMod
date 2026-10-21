using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 历战形态（原百战先锋，修改文档：重做并更名）：
/// 每回合开始时，你可以选择场上（手牌、抽牌堆和弃牌堆）的一张反制牌，将其视作反制打出。
/// <para>
/// 选择可跳过；抽牌堆/弃牌堆中的牌会先移回手牌顶再打出。打出经
/// <see cref="CounterSystem.PlayCounterCard"/> 以反制上下文执行——剑如火舞重定向、
/// 渐入佳境计数等由其统一处理，该牌的反制效果与打出效果正常结算。
/// </para>
/// <para>
/// 耗能递减部分（“这张牌在手牌中时，每反制一次，其耗能-1”）由卡牌本体
/// <see cref="Cards.flametailHardenedVanguard"/> 的 <c>BeforeCardPlayed</c> 实现，不在本能力内。
/// </para>
/// </summary>
[RegisterPower]
public sealed class flametailHardenedVanguardPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        // 候选：场上（手牌 + 抽牌堆 + 弃牌堆）中的所有反制牌。
        var hand = player.PlayerCombatState?.Hand;
        var drawPile = player.PlayerCombatState?.DrawPile;
        var discardPile = player.PlayerCombatState?.DiscardPile;

        List<CardModel> candidates = new();
        if (hand != null)
        {
            candidates.AddRange(hand.Cards.Where(card => card is ICounterCard));
        }

        if (drawPile != null)
        {
            candidates.AddRange(drawPile.Cards.Where(card => card is ICounterCard));
        }

        if (discardPile != null)
        {
            candidates.AddRange(discardPile.Cards.Where(card => card is ICounterCard));
        }

        if (candidates.Count == 0)
        {
            return;
        }

        // 可跳过（“你可以选择”）。
        var prefs = new CardSelectorPrefs(
            new LocString("powers", "FLAMETAIL_VETERAN_FORM_PROMPT"),
            0,
            1)
        {
            Cancelable = true,
        };

        CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            player,
            prefs)).FirstOrDefault();

        if (chosen == null)
        {
            return;
        }

        // 抽牌堆/弃牌堆中的牌先移回手牌顶再打出（与百战先锋重放的处理一致）。
        if (chosen.Pile?.Type != PileType.Hand)
        {
            await CardPileCmd.Add(chosen, PileType.Hand, CardPilePosition.Top, this);
        }

        await CounterSystem.PlayCounterCard(choiceContext, chosen, null);
    }
}
