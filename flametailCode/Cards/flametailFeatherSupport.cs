using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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
public sealed class flametailFeatherSupport : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailFeatherSupport"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Retain, FlametailKeywords.Counter, FlametailKeywords.Support };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(25, ValueProp.Move | FlametailValueProps.IgnoreAttackerDamageModifiers)
    ];

    public flametailFeatherSupport() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        if (cardPlay.Target.IsDead)
        {
            return;
        }

        var impactTcs = new TaskCompletionSource();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .IgnoreAttackerModifiers()
            .WithAttackerFx(() => SummonedAllyVfx.Create(
                Owner.Creature,
                cardPlay.Target,
                $"{Entry.ResPath}/scenes/vfx/summons/feather_support_summon.tscn",
                impactTcs))
            .BeforeDamage(() => impactTcs.Task)
            .Execute(choiceContext);
    }

    /// <summary>
    /// 把“反制时打出下一张反制牌”放在 <see cref="AfterCardPlayed"/> 中，
    /// 而不是 <see cref="OnPlay"/> 内。
    ///
    /// 若在内层 OnPlay 中直接连锁，当前这张牌的 AfterCardPlayed 钩子（如
    /// <see cref="flametailParryingDaggerPower"/>）会等到整条连锁结束后才结算，
    /// 导致伤害/格挡被一次性延后触发。移到 AfterCardPlayed 后，当前牌的所有
    /// 出牌后能力先正常结算，再开始下一张反制牌。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!CounterSystem.IsCounterPlay)
        {
            return;
        }

        if (cardPlay.Card != this)
        {
            return;
        }

        await CounterSystem.PlayNextCounterCard(choiceContext, this, cardPlay.Target);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(7);
    }
}
