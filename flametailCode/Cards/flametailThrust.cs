using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailThrust : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailThrust"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7, ValueProp.Move),
        new IntVar("DrawAmount", 1)
    ];

    public bool HasCounterEffect => true;

    public flametailThrust() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        bool isCounter = this.GetCounterContext().IsCounterPlay;
        // 反制时少抽 1 张牌（max(0, DrawAmount-1)：基础 0、升级 1），且额外造成一次伤害。
        int draw = Math.Max(0, DynamicVars["DrawAmount"].IntValue - (isCounter ? 1 : 0));

        if (!cardPlay.Target.IsDead)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .Execute(choiceContext);
        }

        if (draw > 0)
        {
            await CardPileCmd.Draw(choiceContext, draw, Owner);
        }

        if (isCounter)
        {
            await TriggerCounterEffect(choiceContext, cardPlay, this.GetCounterContext().CurrentAttacker);
        }
    }

    /// <summary>反制时：额外打出此牌1次（即再次造成本牌伤害）。</summary>
    public async Task TriggerCounterEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? attacker)
    {
        if (cardPlay.Target.IsDead)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 伤害升级 7→9（修改文档 [5]“升级后伤害减少1”：旧升级值 10 → 新 9）；抽牌 +1（1→2）。
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["DrawAmount"].UpgradeValueBy(1);
    }
}
