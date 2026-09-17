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
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailStrideSlash : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailStrideSlash"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5, ValueProp.Move),
        new IntVar("FootworkRemoved", 1)
    ];

    protected override bool IsPlayable => base.IsPlayable
        && Owner.Creature.GetPower<flametailFootworkPower>() is { } footwork
        && footwork.HasUsableFootwork;

    public flametailStrideSlash() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        var footwork = Owner.Creature.GetPower<flametailFootworkPower>();
        if (footwork != null)
        {
            await PowerCmd.ModifyAmount(choiceContext, footwork, -DynamicVars["FootworkRemoved"].IntValue, Owner.Creature, this);
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    /// <summary>
    /// 打出后返回手牌顶部的语义修正：
    /// a) 玩家正常主动打出 → 返回手牌顶部；
    /// b) 被故技重施/釜底抽薪打出（打出前设置了 ExhaustOnNextPlay）→ 也返回手牌顶部，
    ///    且不进消耗堆——出牌管线会先在 GetResultLocationForCardPlay 里把 ExhaustOnNextPlay
    ///    消费成 Exhaust 结果位置，因此“入参位置为 Exhaust”即可可靠识别这种重放，
    ///    改写为手牌后天然不会被消耗（标志已被管线清除）；
    /// c) 其它自动打出（原版遗物历史课 HISTORY_COURSE 打出的“复制品”是新建实例，
    ///    card != this，走上面的分支）→ 保持默认结果位置。
    /// </summary>
    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation cardLocation)
    {
        if (card != this)
        {
            return base.ModifyCardPlayResultLocation(card, isAutoPlay, resources, cardLocation);
        }

        if (!isAutoPlay)
        {
            return new CardLocation(card.Owner, PileType.Hand, CardPilePosition.Top);
        }

        if (cardLocation.pileType == PileType.Exhaust)
        {
            return new CardLocation(card.Owner, PileType.Hand, CardPilePosition.Top);
        }

        return base.ModifyCardPlayResultLocation(card, isAutoPlay, resources, cardLocation);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
    }
}
