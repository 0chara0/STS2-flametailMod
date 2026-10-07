using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailKindling : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AllEnemies;
    private const bool ShowInCardLibrary = true;

    private int _extraDamage;

    [SavedProperty]
    public int ExtraDamage
    {
        get => _extraDamage;
        set
        {
            AssertMutable();
            _extraDamage = value;
            DynamicVars.Damage.BaseValue = 3 + _extraDamage;
        }
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };   // 修改文档 [18]：增加“消耗”词条

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailKindling"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3 + ExtraDamage, ValueProp.Move)
    ];

    public flametailKindling() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(Owner.Creature.CombatState!)
            .WithHitCount(2)
            .Execute(choiceContext);

        // 升级后每次打出的基础伤害成长 1→2（修改文档 [17][18]）。
        int growth = IsUpgraded ? 2 : 1;
        AddDamage(growth);
        (DeckVersion as flametailKindling)?.AddDamage(growth);
    }

    public void AddDamage(int amount)
    {
        ExtraDamage += amount;
    }

    // 升级只提高“每次打出的伤害成长”（1→2），攻击次数固定 2 次（用户裁定：升级不应增加次数）。
}
