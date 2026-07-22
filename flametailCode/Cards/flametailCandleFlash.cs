using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Nodes.Vfx;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailCandleFlash : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = -1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private Creature? _pendingAttacker;
    private bool _healedByCounter;

    [SavedProperty]
    public Creature? PendingAttacker
    {
        get => _pendingAttacker;
        set
        {
            AssertMutable();
            _pendingAttacker = value;
        }
    }

    [SavedProperty]
    public bool HealedByCounter
    {
        get => _healedByCounter;
        set
        {
            AssertMutable();
            _healedByCounter = value;
        }
    }

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailCandleFlash"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Innate, CardKeyword.Exhaust, FlametailKeywords.Counter, FlametailKeywords.Ephemeral };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(60, ValueProp.Move | ValueProp.Unpowered),
        new IntVar("HealPercent", 20)
    ];

    protected override bool IsPlayable => CounterSystem.IsCounterPlay;

    public flametailCandleFlash() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public bool CanAutoPlayAsCounter(Creature? attacker) => false;

    /// <summary>
    /// 受到敌方攻击的致死伤害时，记录攻击者，为死亡防止做准备。
    /// </summary>
    public override async Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner.Creature)
        {
            return;
        }

        if (!props.IsPoweredAttack())
        {
            return;
        }

        if (dealer == null || !dealer.IsEnemy)
        {
            return;
        }

        if (Pile?.Type != PileType.Hand)
        {
            return;
        }

        if (Owner.Creature.CurrentHp > amount)
        {
            return;
        }

        PendingAttacker = dealer;
        await Task.CompletedTask;
    }

    /// <summary>
    /// 当角色即将死亡且本牌已记录攻击者时，阻止死亡。
    /// </summary>
    public override bool ShouldDie(Creature creature)
    {
        if (creature != Owner.Creature)
        {
            return true;
        }

        if (PendingAttacker == null)
        {
            return true;
        }

        if (Pile?.Type != PileType.Hand)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 死亡被阻止后：将这张牌像反制牌一样自动打出，使其经历正常的打出动画、
    /// 效果结算并进入消耗牌堆，最后从主牌组中永久移除。
    /// </summary>
    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner.Creature)
        {
            return;
        }

        Creature? attacker = PendingAttacker;
        PendingAttacker = null;

        if (attacker == null)
        {
            return;
        }

        // 先完整复活并结算治疗，再打出卡牌造成伤害。
        // 这样血条会先清空，随后回复 20% 最大生命，最后才播放出牌动画并反击。
        decimal healAmount = (decimal)Owner.Creature.MaxHp * DynamicVars["HealPercent"].IntValue / 100m;
        await CreatureCmd.Heal(Owner.Creature, healAmount);
        HealedByCounter = true;

        bool oldIsCounterPlay = CounterSystem.IsCounterPlay;
        Creature? oldAttacker = CounterSystem.CurrentAttacker;
        bool oldRetarget = CounterSystem.ShouldRetargetCounterToAllEnemies;

        CounterSystem.IsCounterPlay = true;
        CounterSystem.CurrentAttacker = attacker;
        CounterSystem.ShouldRetargetCounterToAllEnemies =
            Owner.Creature.HasPower<flametailFireDancingSwordPower>();
        var manager = Owner.Creature.GetPower<flametailCounterManagerPower>();
        if (manager != null)
        {
            manager.CountersTriggeredThisCombat++;
        }

        try
        {
            await CardCmd.AutoPlay(
                new ThrowingPlayerChoiceContext(),
                this,
                attacker,
                AutoPlayType.Default);
        }
        finally
        {
            CounterSystem.IsCounterPlay = oldIsCounterPlay;
            CounterSystem.CurrentAttacker = oldAttacker;
            CounterSystem.ShouldRetargetCounterToAllEnemies = oldRetarget;

            // 如果因为某些原因 AutoPlay 没有把牌从主牌组移除（例如被 hook 阻止），
            // 在这里兜底移除。
            if (DeckVersion?.Pile?.Type == PileType.Deck)
            {
                await CardPileCmd.RemoveFromDeck(DeckVersion, showPreview: false);
            }
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? target = cardPlay.Target ?? CounterSystem.CurrentAttacker;
        if (target == null)
        {
            return;
        }

        if (target.IsDead)
        {
            return;
        }

        var impactTcs = new TaskCompletionSource();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Unpowered()
            .WithAttackerFx(() => SummonedAllyVfx.Create(
                Owner.Creature,
                target,
                $"{Entry.ResPath}/scenes/vfx/summons/candle_flash_summon.tscn",
                impactTcs))
            .BeforeDamage(() => impactTcs.Task)
            .Execute(choiceContext);

        if (!HealedByCounter)
        {
            decimal healAmount = (decimal)Owner.Creature.MaxHp * DynamicVars["HealPercent"].IntValue / 100m;
            await CreatureCmd.Heal(Owner.Creature, healAmount);
        }

        // 即逝：打出后从卡组中移除。
        if (DeckVersion?.Pile?.Type == PileType.Deck)
        {
            await CardPileCmd.RemoveFromDeck(DeckVersion, showPreview: false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(15);
        DynamicVars["HealPercent"].UpgradeValueBy(5);
    }
}
