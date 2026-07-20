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
public sealed class flametailSuddenHalt : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("BlockPerFootwork", 6)
    ];

    public flametailSuddenHalt() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var footwork = Owner.Creature.GetPower<flametailFootworkPower>();
        if (footwork == null || footwork.Amount <= 0)
        {
            return;
        }

        int footworkAmount = footwork.Amount;
        await PowerCmd.ModifyAmount(choiceContext, footwork, -footworkAmount, Owner.Creature, this);

        decimal block = footworkAmount * DynamicVars["BlockPerFootwork"].IntValue;
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BlockPerFootwork"].UpgradeValueBy(2);
    }
}
