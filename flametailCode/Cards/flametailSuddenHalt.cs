using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Cards.DynamicVars;
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

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailSuddenHalt"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("BlockPerFootwork", 5)
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

        decimal amount = footwork.Amount;
        int blockPerFootwork = DynamicVars["BlockPerFootwork"].IntValue;

        await PowerCmd.ModifyAmount(choiceContext, footwork, -amount, Owner.Creature, this);

        // 逐层获得格挡：每失去 1 层步法独立获得一次格挡（可多次触发势不可当等）。
        for (int i = 0; i < (int)amount; i++)
        {
            await CreatureCmd.GainBlock(Owner.Creature, blockPerFootwork, ValueProp.Move, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BlockPerFootwork"].UpgradeValueBy(2);
    }
}
