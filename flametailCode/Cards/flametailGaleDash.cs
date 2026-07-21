using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using flametail.Characters;
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailGaleDash : ModCardTemplate, IGainFootworkCard
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailGaleDash"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust, FlametailKeywords.Footwork };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("TargetFootwork", 4)
    ];

    public flametailGaleDash() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int currentFootwork = Owner.Creature.GetPower<flametailFootworkPower>()?.Amount ?? 0;
        if (currentFootwork < DynamicVars["TargetFootwork"].IntValue)
        {
            await PowerCmd.Apply<flametailFootworkPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars["TargetFootwork"].IntValue - currentFootwork,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
