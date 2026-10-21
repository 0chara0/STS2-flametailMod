using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;
using flametail.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 自强不息（修改文档 [43][44][45]，替代已往不谏）：稀有技能，耗能 X。
/// 从场上（手牌、抽牌堆、弃牌堆）选择消耗 X（升级 +1）张牌；
/// 每消耗一张诅咒或状态牌，回复 2 点生命值。消耗。
/// 不应在战斗中被生成（CanBeGeneratedInCombat => false）。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailPerseverance : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailPerseverance"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    // 能量 X 费（原版 Skewer/Malaise 同款模式）；升级 = X+1（由 XBonus 实现）。
    protected override bool HasEnergyCostX => true;

    // 此牌不应能在战斗中被生成（修改文档 [45]）。
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust, FlametailKeywords.Onfield };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("XBonus", 0),
        new HealVar("HealPerCurse", 2m)
    ];

    public flametailPerseverance() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int count = ResolveEnergyXValue() + DynamicVars["XBonus"].IntValue;

        // 从场上（手牌、抽牌堆、弃牌堆）合选 count 张消耗。
        var candidates = new List<CardModel>();
        var hand = Owner.PlayerCombatState?.Hand;
        var drawPile = Owner.PlayerCombatState?.DrawPile;
        var discard = Owner.PlayerCombatState?.DiscardPile;
        if (hand != null)
        {
            candidates.AddRange(hand.Cards);
        }

        if (drawPile != null)
        {
            candidates.AddRange(drawPile.Cards);
        }

        if (discard != null)
        {
            candidates.AddRange(discard.Cards);
        }

        var prefs = new CardSelectorPrefs(
            new LocString("cards", "FLAMETAIL_PERSEVERANCE_PROMPT"),
            count,
            count)
        {
            Cancelable = false,
        };

        var chosen = await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, Owner, prefs);

        int heal = 0;
        foreach (CardModel card in chosen)
        {
            // 诅咒或状态牌：每张回复 2 点生命值（在消耗前判定类型）。
            bool isCurseOrStatus = card.Type is CardType.Curse or CardType.Status;
            await CardCmd.Exhaust(choiceContext, card);
            if (isCurseOrStatus)
            {
                heal += (int)DynamicVars["HealPerCurse"].BaseValue;
            }
        }

        if (heal > 0)
        {
            await CreatureCmd.Heal(Owner.Creature, heal);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级：数量 X → X+1。
        DynamicVars["XBonus"].UpgradeValueBy(1);
    }
}
