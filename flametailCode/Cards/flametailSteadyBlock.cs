using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailSteadyBlock : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailSteadyBlock"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("BlockPerFootwork", 6),
        new IntVar("MaxFootwork", 5)
    ];

    public flametailSteadyBlock() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int footwork = Owner.Creature.GetPower<flametailFootworkPower>()?.Amount ?? 0;
        int missing = int.Max(0, DynamicVars["MaxFootwork"].IntValue - footwork);
        if (missing > 0)
        {
            decimal block = missing * DynamicVars["BlockPerFootwork"].IntValue;
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BlockPerFootwork"].UpgradeValueBy(2);
    }
}
