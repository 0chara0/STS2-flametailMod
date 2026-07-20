using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Keywords;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailPreempt : ModCardTemplate, ICounterCard
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Innate, CardKeyword.Exhaust, FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move)
    ];

    public flametailPreempt() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        List<CardModel> counters = Owner.PlayerCombatState?.DrawPile.Cards
            .Where(c => c is ICounterCard)
            .ToList() ?? new List<CardModel>();

        if (counters.Count == 0)
        {
            return;
        }

        CardModel chosen = Owner.RunState!.Rng.CombatCardSelection.NextItem(counters)!;
        await CardPileCmd.Add(chosen, PileType.Hand, CardPilePosition.Top);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
    }
}