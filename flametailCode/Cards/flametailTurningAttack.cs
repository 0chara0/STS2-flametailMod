using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.HoverTips;
using flametail.Characters;
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailTurningAttack : ModCardTemplate, ICounterCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[] {
        HoverTipFactory.FromCard<Clumsy>()
    };
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailTurningAttack"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12, ValueProp.Move),
        new IntVar("DrawAmount", 3)
    ];

    public flametailTurningAttack() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        if (!cardPlay.Target.IsDead)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .Execute(choiceContext);
        }

        CardModel clumsy = Owner.Creature.CombatState!.CreateCard<Clumsy>(Owner);
        CardPileAddResult clumsyResult = await CardPileCmd.AddGeneratedCardToCombat(clumsy, PileType.Draw, Owner, CardPilePosition.Bottom);
        CardCmd.PreviewCardPileAdd(clumsyResult);

        if (CounterSystem.IsCounterPlay)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars["DrawAmount"].IntValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
    }
}