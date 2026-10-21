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
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace flametail.Cards;

/// <summary>
/// 借过一下（原釜底抽薪，修改文档：调整并更名）：2 费（升级 1 费）。
/// 选择手牌或抽牌堆中的一张非能力牌，将其打出 2 次；基础版打出后消耗，升级版不消耗。
/// 不再消耗闪避，改为：你当前每有 1 层闪避，这张牌（借过一下自身）的耗能减少 1——
/// 由卡牌级 <see cref="TryModifyEnergyCostInCombat"/> 实时折算并显示。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailDrain : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailDrain"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Dodge };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => IsUpgraded
        ? []
        : [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

    public flametailDrain() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    /// <summary>
    /// 闪避折扣：当前每有 1 层闪避，此牌耗能 -1（下限 0）。
    /// 闪避层数变化时由引擎按需重算耗能，卡面实时显示（修改文档：借过一下）。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card != this)
        {
            return false;
        }

        int dodge = Owner.Creature?.GetPower<flametailDodgePower>()?.Amount ?? 0;
        if (dodge <= 0)
        {
            return false;
        }

        modifiedCost = originalCost - dodge;
        if (modifiedCost < 0m)
        {
            modifiedCost = 0m;
        }

        return true;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = Owner.PlayerCombatState?.Hand;
        var drawPile = Owner.PlayerCombatState?.DrawPile;
        int estimatedCount = (hand?.Cards.Count ?? 0) + (drawPile?.Cards.Count ?? 0);
        List<CardModel> candidates = new(estimatedCount);

        if (hand != null)
        {
            foreach (CardModel card in hand.Cards)
            {
                if (card.Type != CardType.Power)
                {
                    candidates.Add(card);
                }
            }
        }

        if (drawPile != null)
        {
            foreach (CardModel card in drawPile.Cards)
            {
                if (card.Type != CardType.Power)
                {
                    candidates.Add(card);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(
            new LocString("cards", "FLAMETAIL_DRAIN_PROMPT"),
            1,
            1)
        {
            Cancelable = true,
            PretendCardsCanBePlayed = true,
        };

        IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            Owner,
            prefs);

        CardModel? targetCard = selected.FirstOrDefault();
        if (targetCard == null)
        {
            return;
        }

        // 修改文档：固定打出 2 次（借过一下不再消耗闪避、也不再随闪避加次数）。
        // 打出前移回手牌顶（与回击一致），保证 AutoPlay 正常工作。
        if (targetCard.Pile?.Type != PileType.Hand)
        {
            await CardPileCmd.Add(targetCard, PileType.Hand, CardPilePosition.Top, this);
        }

        // 目标随选中的牌走：单一目标型牌弹目标选择，其余/取消传 null 由引擎兜底。
        Creature? target = await AutoPlayTargetResolver.ResolveTargetAsync(targetCard, this);

        // 第二次打出由隐藏的 Echo 能力实现（打出次数 +1，触发后自移除）。
        await PowerCmd.Apply<flametailDrainEchoPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);

        // 基础版打出后消耗；升级版不消耗（修改文档：（不）消耗）。
        if (!IsUpgraded)
        {
            targetCard.ExhaustOnNextPlay = true;
        }

        await CardCmd.AutoPlay(choiceContext, targetCard, target);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
