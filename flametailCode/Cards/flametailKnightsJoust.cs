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
public sealed class flametailKnightsJoust : ModCardTemplate, ICounterCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[] {
        HoverTipFactory.FromCard<Injury>()
    };

    private const int BaseEnergyCost = 3;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailKnightsJoust"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Counter };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(27, ValueProp.Move)
    ];

    public bool CanAutoPlayAsCounter(Creature? attacker) => true;

    public bool CanBePlayedBySupportEffects => false;

    public flametailKnightsJoust() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 作为反制牌被打出来时，无论本次攻击是否被完全抵消，都会往抽牌堆添加一张受伤。
        if (CounterSystem.IsCounterPlay)
        {
            CardModel injury = Owner.Creature.CombatState!.CreateCard<Injury>(Owner);
            CardPileAddResult injuryResult = await CardPileCmd.AddGeneratedCardToCombat(injury, PileType.Draw, Owner, CardPilePosition.Bottom);
            CardCmd.PreviewCardPileAdd(injuryResult);

            // 如果本次攻击没有被完全抵消（没有完美格挡或闪避），直接丢弃此牌，不执行攻击。
            if (!CounterSystem.LastAttackMitigated)
            {
                await CardCmd.Discard(choiceContext, this);
                return;
            }
        }

        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        if (!cardPlay.Target.IsDead)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(9);
    }
}