using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Collections.Generic;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailBlisteringCounter : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Ancient;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailBlisteringCounter"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain, FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8, ValueProp.Move),
        new IntVar("HitCount", 2),
        new IntVar("VulnerableAmount", 2)
    ];

    public bool HasCounterEffect => true;

    public flametailBlisteringCounter() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        if (cardPlay.Target.IsDead)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(DynamicVars["HitCount"].IntValue)
            .Execute(choiceContext);

        // 基础效果：给予敌人 2 层易伤（目标被本次伤害击杀则跳过）。
        if (!cardPlay.Target.IsDead)
        {
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars["VulnerableAmount"].IntValue,
                Owner.Creature,
                this);
        }

        if (this.GetCounterContext().IsCounterPlay)
        {
            await TriggerCounterEffect(choiceContext, cardPlay, cardPlay.Target);
        }
    }

    /// <summary>反制时：额外打出手牌中的下一张反制牌。</summary>
    public async Task TriggerCounterEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? attacker)
    {
        await CounterSystem.PlayNextCounterCard(choiceContext, this, cardPlay.Target);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HitCount"].UpgradeValueBy(1);
    }
}
