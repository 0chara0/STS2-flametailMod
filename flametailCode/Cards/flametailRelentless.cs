using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using flametail.Keywords;
using MegaCrit.Sts2.Core.HoverTips;

namespace flametail.Cards;

/// <summary>
/// 一往无前（修改文档 [37][38]）：耗能 1，获得 2 点能量，抽 2 张牌，所有手牌获得虚无，
/// 本场战斗中此牌耗能 +2（升级 +1）。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailRelentless : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailRelentless"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(2)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[] {
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    };

    public flametailRelentless() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(DynamicVars["Energy"].IntValue, Owner);

        await CardPileCmd.Draw(choiceContext, 2, Owner);

        foreach (CardModel card in Owner.PlayerCombatState?.Hand.Cards ?? Array.Empty<CardModel>())
        {
            if (!card.Keywords.Contains(CardKeyword.Ethereal))
            {
                card.AddKeyword(CardKeyword.Ethereal);
            }
        }

        // 本场战斗中此牌耗能增长 +2（升级 +1，升级不加能量值）。
        EnergyCost.AddThisCombat(IsUpgraded ? 1 : 2, reduceOnly: false);
    }

    protected override void OnUpgrade()
    {
        // 升级：本场耗能增长 2→1（差异在 OnPlay 中按 IsUpgraded 结算）。
    }
}
