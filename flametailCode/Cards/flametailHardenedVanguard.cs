using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using flametail.Keywords;

namespace flametail.Cards;

/// <summary>
/// 历战形态（原百战先锋，修改文档：重做并更名）：稀有能力牌，5（4）费，保留。
/// 每回合开始时，你可以选择场上任一张反制牌，将其视作反制打出（由能力实现）。
/// 这张牌在手牌中时，每反制一次，其耗能-1（见 <see cref="BeforeCardPlayed"/>）。
/// 本体不是反制牌（2026-10-21 用户裁定：不受反制系统自动打出，也不被历战形态自身选中）。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailHardenedVanguard : ModCardTemplate
{
    private const int BaseEnergyCost = 5;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailHardenedVanguard"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    // 保留为常驻词条（修改文档：耗能 5（4）保留）。本体不带反制词条。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Retain };

    public flametailHardenedVanguard() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<flametailHardenedVanguardPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);
    }

    /// <summary>
    /// 耗能递减：这张牌在手牌中时，每反制一次（任何反制，含自身被反制打出），耗能-1。
    /// 反制触发的判定与旧百战先锋重放队列一致：反制上下文打出的反制牌、
    /// 任何方式打出的“打出即反制”牌（回击等）、骑士对决将主动响应的牌。
    /// </summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Pile?.Type != PileType.Hand)
        {
            return Task.CompletedTask;
        }

        var counterContext = this.GetCounterContext();
        bool isCounterTrigger =
            (counterContext.IsCounterPlay && cardPlay.Card is ICounterCard)
            || cardPlay.Card is ICounterCard { CounterEffectTriggersOnAnyPlay: true }
            || Owner.Creature.GetPower<flametailKnightsDuelPower>()?.WouldTriggerCounterEffect(cardPlay) == true;

        if (!isCounterTrigger)
        {
            return Task.CompletedTask;
        }

        // 耗能 -1 持续整场战斗（reduceOnly：只在会降低耗能时生效，避免叠加异常）。
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
