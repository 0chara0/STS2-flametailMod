using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailCrossguardArts : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailCrossguardArts"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7, ValueProp.Move),
        new DamageVar(7, ValueProp.Move)
    ];

    public bool HasCounterEffect => true;

    public flametailCrossguardArts() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        var counterContext = this.GetCounterContext();
        if (counterContext.IsCounterPlay)
        {
            await TriggerCounterEffect(choiceContext, cardPlay, counterContext.CurrentAttacker);
        }
    }

    /// <summary>反制时：额外对当前攻击者造成 {Damage} 点伤害。</summary>
    public async Task TriggerCounterEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? attacker)
    {
        if (Owner.Creature.CombatState is not { } combat)
        {
            return;
        }

        // 主动打出触发（骑士对决）时没有真实攻击者，attacker 会指向自身；此时不造成伤害，避免误伤自己。
        // 除非剑如火舞将反制重定向到所有敌人（此时 GetCounterTargets 会返回所有敌人）。
        if ((attacker == null || attacker == Owner.Creature)
            && !this.GetCounterContext().ShouldRetargetCounterToAllEnemies)
        {
            return;
        }

        foreach (Creature target in CounterSystem.GetCounterTargets(attacker, combat))
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars.Damage.UpgradeValueBy(3);
    }
}
