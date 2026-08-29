using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 华舞：执行 3 次（每 2 层步法额外 1 次），对随机敌人造成 4(5) + 已执行次数 点伤害，
/// 即第 1 次 4(5)、第 2 次 5(6)、第 3 次 6(7)……。重做后不再具有反制。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailFlourish : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.RandomEnemy;
    private const bool ShowInCardLibrary = true;
    private const int BaseHitCount = 3;
    private const int FootworkPerExtraHit = 2;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailFlourish"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move)
    ];

    public flametailFlourish() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal footwork = Owner.Creature.GetPower<flametailFootworkPower>()?.Amount ?? 0m;
        int hitCount = BaseHitCount + (int)(footwork / FootworkPerExtraHit);

        // 每次独立攻击：随机目标，伤害 = 基础伤害 + 已执行次数。
        for (int i = 0; i < hitCount; i++)
        {
            decimal hitDamage = DynamicVars.Damage.BaseValue + i;
            await DamageCmd.Attack(hitDamage)
                .FromCard(this, cardPlay)
                .TargetingRandomOpponents(Owner.Creature.CombatState!)
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级：基础伤害 4→5（不再增加执行次数）。
        DynamicVars.Damage.UpgradeValueBy(1);
    }
}
