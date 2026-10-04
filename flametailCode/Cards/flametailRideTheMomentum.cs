using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Patches;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailRideTheMomentum : ModCardTemplate, ICounterCard, IGainFootworkCard
{
    private const int BaseEnergyCost = -1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailRideTheMomentum"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter, FlametailKeywords.Footwork };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("FootworkAmount", 2),
        new IntVar("DrawAmount", 2)
    ];

    public bool HasCounterEffect => true;

    protected override bool IsPlayable => this.GetCounterContext().IsCounterPlay;

    /// <summary>
    /// 只要响应反制链就会被打出（效果按闪避分支：步法/抽牌/消耗只在闪避时发生；
    /// “额外打出下一张反制牌”是无条件的——修改文档 [13][14]）。
    /// </summary>
    public bool CanAutoPlayAsCounter(Creature? attacker) => true;

    public flametailRideTheMomentum() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!this.GetCounterContext().IsCounterPlay)
        {
            return;
        }

        await TriggerCounterEffect(choiceContext, cardPlay, cardPlay.Target);
    }

    /// <summary>
    /// 反制时：无条件打出手牌中的下一张反制牌；若闪避了此次攻击，
    /// 再获得步法、抽牌并消耗此牌（判读 CounterContext.WasDodged，不查全局 dodge 标记）。
    /// </summary>
    public async Task TriggerCounterEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay, Creature? attacker)
    {
        // 1) 无条件链式。
        await CounterSystem.PlayNextCounterCard(choiceContext, this, cardPlay.Target);

        // 2) 闪避分支。
        if (!this.GetCounterContext().WasDodged)
        {
            return;
        }

        await PowerCmd.Apply<flametailFootworkPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["FootworkAmount"].IntValue,
            Owner.Creature,
            this);

        await CardPileCmd.Draw(
            choiceContext,
            DynamicVars["DrawAmount"].IntValue,
            Owner);

        await CardCmd.Exhaust(choiceContext, this);
    }

    protected override void OnUpgrade()
    {
        // 升级：抽牌 2→3，并添加“保留”词条（保留仅升级后出现）。
        DynamicVars["DrawAmount"].UpgradeValueBy(1);
        AddKeyword(CardKeyword.Retain);
    }
}
