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
using flametail.Helpers;
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

    // 单段伤害结算内临时记录的攻击前血量，供 AfterDamageReceived 校验本次攻击是否真的致死。
    // 哨兵值 -1 表示“本段伤害未武装”。仅在 BeforeDamageReceived → AfterDamageReceived 之间有效，无需持久化。
    private decimal _hpBeforeDamage;

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
        new[] { CardKeyword.Innate, CardKeyword.Exhaust, FlametailKeywords.Ephemeral, FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(60, ValueProp.Move | FlametailValueProps.GetIgnoreAttackerDamageModifiers()),
        new IntVar("HealPercent", 20)
    ];

    protected override bool IsPlayable => this.GetCounterContext().IsCounterPlay;

    public flametailCandleFlash() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public bool CanAutoPlayAsCounter(Creature? attacker) => false;

    /// <summary>
    /// 受到敌方攻击时，先临时记录攻击者，为死亡防止做准备。
    ///
    /// 这里不做“是否致死”的预测：BeforeDamageReceived 拿到的 amount 是格挡/闪避结算
    /// 之前的原始伤害，若在这里手动判定致死，就得把格挡、闪避以及未来所有免伤因素
    /// 逐个纳入考虑。因此本方法只负责临时武装（配合原生的“血量 ≤ 原始伤害才武装”
    /// 预筛，只挡掉明显不可能致死的情况），真正的致死与否由
    /// <see cref="AfterDamageReceived"/> 在结算完成后用 <see cref="DamageResult.UnblockedDamage"/>
    /// 校验，不致死就撤销武装。
    ///
    /// 注意：按“单段伤害致死”判断。对于每一段单独都不致死、但合计致死的多段攻击
    /// （如 3×4 打 12 血），本逻辑无法在 BeforeDamageReceived 内累计判断，
    /// 因为该 hook 不提供“本次攻击结束”的边界。要完整支持需在内省游戏伤害结算
    /// 流程后实现攻击边界的累计跟踪。
    /// </summary>
    public override async Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        // 先标记本段伤害为“未武装”，AfterDamageReceived 据此跳过校验，
        // 避免读到上一次伤害结算残留的血量快照。
        _hpBeforeDamage = -1m;

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

        // 原始伤害低于当前血量时，即使没有格挡也必然不致死，无需武装。
        if (Owner.Creature.CurrentHp > amount)
        {
            return;
        }

        _hpBeforeDamage = Owner.Creature.CurrentHp;
        PendingAttacker = dealer;
        await Task.CompletedTask;
    }

    /// <summary>
    /// 伤害结算完成后，用真实的净伤害校验本次武装是否成立。
    ///
    /// 格挡、闪避、减伤等所有免伤因素都已经反映在 <see cref="DamageResult.UnblockedDamage"/>
    /// 中，因此无需逐个枚举。若净伤害低于攻击前血量，说明本次攻击并未把血打空
    /// （被格挡/闪避等完全抵消），撤销错误的武装，避免它残留在卡上、在后续
    /// 无关的致死判定中误触发死亡拦截而卡住。
    /// </summary>
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner.Creature)
        {
            return;
        }

        // 本段伤害没有在 BeforeDamageReceived 武装（哨兵值），跳过。
        if (_hpBeforeDamage < 0m)
        {
            return;
        }

        // 只有净伤害 >= 攻击前血量（真致死）才保留武装；
        // 否则说明被格挡/闪避等抵消，撤销误报。
        if (_hpBeforeDamage > result.UnblockedDamage)
        {
            PendingAttacker = null;
        }

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
        // 这样血条会先清空，随后回复到最大生命的 20%，最后才播放出牌动画并反击。
        await HealToPercentOfMax();
        HealedByCounter = true;

        // 统一走反制系统：设置 IsCounterPlay / CurrentAttacker / 重定向、
        // 递增反制计数，并在结束后恢复上下文。
        try
        {
            await CounterSystem.PlayCounterCard(
                new ThrowingPlayerChoiceContext(),
                this,
                attacker);
        }
        finally
        {
            // 如果因为某些原因 AutoPlay 没有把牌从主牌组移除（例如被 hook 阻止），
            // 在这里兜底移除。
            if (DeckVersion?.Pile?.Type == PileType.Deck)
            {
                await CardPileCmd.RemoveFromDeck(DeckVersion, showPreview: false);
            }
        }
    }

    /// <summary>
    /// 将生命值补齐到最大生命值的 HealPercent%（仅在当前生命低于该阈值时生效）。
    /// 例：最大生命 100、当前 8、HealPercent 20 → 回复 12，落到 20；当前 50 → 不回复。
    /// </summary>
    private async Task HealToPercentOfMax()
    {
        decimal threshold = (decimal)Owner.Creature.MaxHp * DynamicVars["HealPercent"].IntValue / 100m;
        decimal healAmount = Math.Max(threshold - Owner.Creature.CurrentHp, 0m);
        await CreatureCmd.Heal(Owner.Creature, healAmount);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? target = cardPlay.Target ?? this.GetCounterContext().CurrentAttacker;
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
            .IgnoreAttackerModifiers()
            .WithAttackerFx(() => SummonedAllyVfx.Create(
                Owner.Creature,
                target,
                $"{Entry.ResPath}/scenes/vfx/summons/candle_flash_summon.tscn",
                impactTcs))
            .BeforeDamage(() => impactTcs.Task)
            .Execute(choiceContext);

        if (!HealedByCounter)
        {
            await HealToPercentOfMax();
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
