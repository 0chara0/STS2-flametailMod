using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailFlourish : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.RandomEnemy;
    private const bool ShowInCardLibrary = true;
    private const decimal BaseHitDamage = 3m;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailFlourish"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.ComputedDamage(
            "Damage", BaseHitDamage,
            (card, target) => ResolveHitDamage(card, isUpgradePreview: false),
            (card, mode, target, runGlobalHooks) => ResolveHitDamage(card, isUpgradePreview: mode == CardPreviewMode.Upgrade),
            ValueProp.Move),
        new IntVar("HitCount", 3),
        new IntVar("CounterBonusHits", 2)
    ];

    public flametailFlourish() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    /// <summary>
    /// 实时计算每次命中伤害：基础伤害(升级 +1) + 当前步法。
    /// 卡牌图书馆等非战斗场景渲染的是规范模型，访问 Owner 会抛异常，按无步法回退为基础伤害。
    /// </summary>
    private static decimal ResolveHitDamage(CardModel? card, bool isUpgradePreview)
    {
        if (card?.CombatState == null)
        {
            return BaseHitDamage;
        }

        decimal baseHit = (card.IsUpgraded || isUpgradePreview) ? BaseHitDamage + 1m : BaseHitDamage;
        decimal footwork = card.Owner?.Creature?.GetPower<flametailFootworkPower>()?.Amount ?? 0m;
        return baseHit + footwork;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal hitDamage = DynamicVars.ComputeDynamicValue("Damage", BaseHitDamage, null);

        await DamageCmd.Attack(hitDamage)
            .FromCard(this, cardPlay)
            .TargetingRandomOpponents(Owner.Creature.CombatState!)
            .WithHitCount(DynamicVars["HitCount"].IntValue)
            .Execute(choiceContext);

        if (this.GetCounterContext().IsCounterPlay)
        {
            DynamicVars["HitCount"].BaseValue += DynamicVars["CounterBonusHits"].IntValue;
        }
    }

    protected override void OnUpgrade()
    {
        // 基础伤害升级由 ResolveHitDamage 依据 card.IsUpgraded 处理。
    }
}
