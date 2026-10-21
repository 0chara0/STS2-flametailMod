using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;
using flametail.Helpers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using flametail.Keywords;
using MegaCrit.Sts2.Core.HoverTips;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailReplay : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailReplay"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[] {
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    };

    public flametailReplay() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardPile discardPile = PileType.Discard.GetPile(Owner);

        var prefs = new CardSelectorPrefs(
            new LocString("cards", "FLAMETAIL_REPLAY_PROMPT"),
            1);

        CardModel? chosen = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            discardPile,
            Owner,
            prefs,
            card => IsUpgraded || card.Type == CardType.Attack)).FirstOrDefault();

        if (chosen == null)
        {
            return;
        }

        // 直接从弃牌堆打出（AutoPlay 支持任意堆），不经过手牌：
        // 避免手牌满时被重定向进弃牌堆、以及误触进入手牌钩子。

        // 与「破灭」对齐：设置一次性 ExhaustOnNextPlay 标志，打出后直接进消耗堆，避免诅咒牌二次触发回神。
        chosen.ExhaustOnNextPlay = true;

        // 目标随选中的牌走：单一目标型牌弹目标选择，其余/取消传 null 由引擎兜底（修改文档：故技重施）。
        Creature? target = await AutoPlayTargetResolver.ResolveTargetAsync(chosen, this);

        await CardCmd.AutoPlay(choiceContext, chosen, target, AutoPlayType.Default);
    }
}
