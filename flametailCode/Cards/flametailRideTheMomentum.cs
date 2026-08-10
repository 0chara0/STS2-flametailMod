using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Patches;
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

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailRideTheMomentum"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("FootworkAmount", 2),
        new IntVar("DrawAmount", 2)
    ];

    protected override bool IsPlayable => this.GetCounterContext().IsCounterPlay;

    /// <summary>
    /// 只在本次敌方强化攻击被闪避时才会作为反制牌自动打出；
    /// 未闪避时不打出，也不会出现“打出但无效果”的情况。
    /// </summary>
    public bool CanAutoPlayAsCounter(Creature? attacker) => Owner.Creature != null && DodgeBlockPatch.WasAttackDodged(Owner.Creature);

    public flametailRideTheMomentum() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!this.GetCounterContext().IsCounterPlay || Owner.Creature == null || !DodgeBlockPatch.WasAttackDodged(Owner.Creature))
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
