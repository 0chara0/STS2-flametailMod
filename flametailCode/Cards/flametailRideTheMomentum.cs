using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailRideTheMomentum : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = -1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("FootworkAmount", 2),
        new IntVar("DrawAmount", 2)
    ];

    protected override bool IsPlayable => CounterSystem.IsCounterPlay;

    public flametailRideTheMomentum() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!CounterSystem.IsCounterPlay || !CounterSystem.WasAttackDodged)
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
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DrawAmount"].UpgradeValueBy(1);
    }
}
