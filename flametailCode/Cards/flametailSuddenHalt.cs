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
using flametail.Keywords;

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
        new IntVar("BlockPerFootwork", 4)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Footwork };

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

        // 逐层剥离：每次只移除 1 层步法并立刻获得一次格挡，
        // 步法一格一格掉，每次失去都是独立的步法减少事件（修改文档：急停）。
        // 步法在任何变动后 ≥5 层都会立即转化，故打出时层数恒 ≤4，逐层剥离不会触发中途转化。
        for (int i = 0; i < (int)amount; i++)
        {
            if (footwork.Amount <= 0)
            {
                break;
            }

            await PowerCmd.ModifyAmount(choiceContext, footwork, -1, Owner.Creature, this);
            await CreatureCmd.GainBlock(Owner.Creature, blockPerFootwork, ValueProp.Move, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BlockPerFootwork"].UpgradeValueBy(1);
    }
}
