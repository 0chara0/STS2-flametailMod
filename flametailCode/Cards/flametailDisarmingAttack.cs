using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
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
public sealed class flametailDisarmingAttack : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailDisarmingAttack"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6, ValueProp.Move),
        new IntVar("WeakAmount", 1)
    ];

    public bool HasCounterEffect => true;

    /// <summary>反制效果为“改为”（替换基础效果）：骑士对决触发时不再执行基础效果。</summary>
    public bool CounterEffectReplacesBaseEffect => true;

    public flametailDisarmingAttack() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var counterContext = this.GetCounterContext();
        if (counterContext.IsCounterPlay)
        {
            await TriggerCounterEffect(choiceContext, cardPlay, counterContext.CurrentAttacker);
            return;
        }

        // 骑士对决主动打出触发“反制时：”效果时，本牌的“改为”语义替换基础效果：
        // 不执行造成伤害/施加虚弱，反制效果由骑士对决在 AfterCardPlayedLate 触发（消耗此牌等）。
        if (counterContext.SuppressBaseEffect)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        if (!cardPlay.Target.IsDead)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .Execute(choiceContext);

            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars["WeakAmount"].IntValue,
                Owner.Creature,
                this);
        }
    }

    /// <summary>反制时：改为消耗此牌，使攻击者失去 {WeakAmount} 点力量。</summary>
    public async Task TriggerCounterEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? attacker)
    {
        if (Owner.Creature.CombatState is { } combat)
        {
            foreach (Creature target in CounterSystem.GetCounterTargets(attacker, combat))
            {
                if (target.IsDead)
                {
                    continue;
                }

                await PowerCmd.Apply<StrengthPower>(
                    choiceContext,
                    target,
                    -DynamicVars["WeakAmount"].IntValue,
                    Owner.Creature,
                    this);
            }
        }

        await CardCmd.Exhaust(choiceContext, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["WeakAmount"].UpgradeValueBy(1);
    }
}
