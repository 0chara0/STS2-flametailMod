using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 烈阳：所有玩家随机消耗 3 张诅咒或状态牌（手牌 + 抽牌堆 + 弃牌堆）。消耗。
/// 升级后：消耗数量 +1（3 → 4）。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailSunflare : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailSunflare"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("ExhaustCount", 3)
    ];

    // 仅多人模式可用的卡：同时清理所有玩家（含自己）的诅咒/状态牌，多人合作中才有完整价值。
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    public flametailSunflare() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (Player player in Owner.Creature.CombatState!.Players)
        {
            var pcs = player.PlayerCombatState;
            if (pcs == null)
            {
                continue;
            }

            // 收集该玩家手牌 + 抽牌堆 + 弃牌堆中的全部诅咒/状态牌。
            List<CardModel> candidates = new();
            foreach (CardModel card in pcs.Hand.Cards)
            {
                if (card.Type == CardType.Curse || card.Type == CardType.Status)
                {
                    candidates.Add(card);
                }
            }

            foreach (CardModel card in pcs.DrawPile.Cards)
            {
                if (card.Type == CardType.Curse || card.Type == CardType.Status)
                {
                    candidates.Add(card);
                }
            }

            foreach (CardModel card in pcs.DiscardPile.Cards)
            {
                if (card.Type == CardType.Curse || card.Type == CardType.Status)
                {
                    candidates.Add(card);
                }
            }

            if (candidates.Count == 0)
            {
                continue;
            }

            // 随机消耗至多 ExhaustCount 张（逐张随机并移除，避免重复选中同一张）。
            var rng = Owner.RunState?.Rng.CombatCardSelection;
            if (rng == null)
            {
                continue;
            }

            int exhaustCount = DynamicVars["ExhaustCount"].IntValue;
            for (int i = 0; i < exhaustCount && candidates.Count > 0; i++)
            {
                CardModel? chosen = rng.NextItem(candidates);
                if (chosen == null)
                {
                    break;
                }

                candidates.Remove(chosen);
                await CardCmd.Exhaust(choiceContext, chosen);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ExhaustCount"].UpgradeValueBy(1);
    }
}
