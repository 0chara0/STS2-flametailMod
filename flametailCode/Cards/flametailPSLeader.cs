using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailPSLeader : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailPSLeader"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Retain, CardKeyword.Exhaust };

    public flametailPSLeader() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> choices = new()
        {
            Owner.Creature.CombatState!.CreateCard<flametailCannonSupport>(Owner),
            Owner.Creature.CombatState!.CreateCard<flametailLanceSupport>(Owner),
            Owner.Creature.CombatState!.CreateCard<flametailFeatherSupport>(Owner),
        };

        // 升级后，三张候选牌先升级，选牌界面直接展示升级后的牌，玩家看到的就是最终获得的形态。
        if (IsUpgraded)
        {
            foreach (CardModel choice in choices)
            {
                CardCmd.Upgrade(choice);
            }
        }

        // 三选一获取的牌都添加消耗词条（炮击/骑枪支援本身已带消耗，仅补齐光箭支援）。
        foreach (CardModel choice in choices.Where(c => !c.Keywords.Contains(CardKeyword.Exhaust)))
        {
            choice.AddKeyword(CardKeyword.Exhaust);
        }

        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            choices,
            Owner,
            canSkip: false);

        if (chosen == null)
        {
            return;
        }

        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        // 升级效果由 OnPlay 实现：本卡升级后，三选一获取的牌也会升级。
    }
}
