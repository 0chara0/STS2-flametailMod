using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 困兽之斗：消耗你所有诅咒和状态牌，3 回合后把消耗牌堆中的诅咒/状态牌返回弃牌堆。
/// 多次打出会在各自的对应回合分别触发。升级后移除本牌的消耗词条。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailCornered : ModCardTemplate
{
    

    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{nameof(flametailCornered)}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public flametailCornered() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 先快照手牌、抽牌堆、弃牌堆中的全部诅咒/状态牌，再逐个消耗，避免修改牌堆时枚举器失效。
        List<CardModel> curseStatus = new();
        foreach (CardModel card in Owner.PlayerCombatState?.Hand?.Cards ?? [])
        {
            if (card.Type == CardType.Curse || card.Type == CardType.Status)
            {
                curseStatus.Add(card);
            }
        }

        foreach (CardModel card in Owner.PlayerCombatState?.DrawPile?.Cards ?? [])
        {
            if (card.Type == CardType.Curse || card.Type == CardType.Status)
            {
                curseStatus.Add(card);
            }
        }

        foreach (CardModel card in Owner.PlayerCombatState?.DiscardPile?.Cards ?? [])
        {
            if (card.Type == CardType.Curse || card.Type == CardType.Status)
            {
                curseStatus.Add(card);
            }
        }

        foreach (CardModel card in curseStatus)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        // 同一玩家回合重复打出不再叠加新的倒计时（消耗效果照常执行）；
        // 不同回合每次打出都新建一个独立的倒计时实例（InstanceType.Instanced），
        // 各自在对应回合触发，互不影响。
        int turn = Owner.PlayerCombatState?.TurnNumber ?? int.MinValue;
        if (!flametailCorneredRecallPower.MarkAppliedIfFirstThisTurn(Owner.Creature?.CombatState, turn))
        {
            return;
        }

        await PowerCmd.Apply<flametailCorneredRecallPower>(
            choiceContext,
            Owner.Creature,
            3,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
