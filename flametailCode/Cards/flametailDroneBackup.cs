using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailDroneBackup : ModCardTemplate
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[] {
        HoverTipFactory.FromCard<Decay>()
    };
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailDroneBackup"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("DrawAmount", 3),
        new EnergyVar(2)
    ];

    public flametailDroneBackup() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DrawCardsNextTurnPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["DrawAmount"].IntValue,
            Owner.Creature,
            this);

        await PowerCmd.Apply<flametailEnergyNextTurnPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Energy"].IntValue,
            Owner.Creature,
            this);

        CardModel decay = Owner.Creature.CombatState!.CreateCard<Decay>(Owner);
        CardPileAddResult decayResult = await CardPileCmd.AddGeneratedCardToCombat(decay, PileType.Discard, Owner, CardPilePosition.Top);
        CardCmd.PreviewCardPileAdd(decayResult);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Energy"].UpgradeValueBy(1);
    }
}
