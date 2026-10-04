using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 跟得上吗（重做，修改文档 [20][21]）：耗能 X。
/// 随机将 X（升级 +1）张能获得步法的牌加入你的手牌，这些牌在本回合可以免费打出。
/// 消耗。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailCanYouKeepUp : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailCanYouKeepUp"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    // 能量 X 费（原版 Skewer/Malaise 同款模式）；升级 = X+1（由 XBonus 实现）。
    protected override bool HasEnergyCostX => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("XBonus", 0)
    ];

    public flametailCanYouKeepUp() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (flametailFootworkCardRegistry.Creators.Count == 0)
        {
            return;
        }

        // X 费解析（含 ChemicalX 等修正）+ 升级加成；随机抽取不重复的创建器。
        int count = ResolveEnergyXValue() + DynamicVars["XBonus"].IntValue;
        var creators = flametailFootworkCardRegistry.Creators.ToList();
        for (int i = 0; i < count && creators.Count > 0; i++)
        {
            Func<CardModel> creator = Owner.RunState!.Rng.CombatCardSelection.NextItem(creators)!;
            creators.Remove(creator);

            CardModel card = Owner.Creature.CombatState!.CreateCard(creator(), Owner);
            card.SetToFreeThisTurn();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级：数量 X → X+1。
        DynamicVars["XBonus"].UpgradeValueBy(1);
    }
}
