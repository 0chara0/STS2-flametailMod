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
public sealed class flametailRegainPosture : ModCardTemplate, IGainFootworkCard
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailRegainPosture"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { FlametailKeywords.Footwork };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Footwork", 2),
        new IntVar("DrawAmount", 2)
    ];

    public flametailRegainPosture() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 生效条件（步法 ≤ 1）满足时，手牌中显示金色发光边框（参考原版 Evil Eye 的 ShouldGlowGoldInternal 用法）。
    protected override bool ShouldGlowGoldInternal =>
        CombatState != null
        && Owner.Creature is { } creature
        && (creature.GetPower<flametailFootworkPower>()?.Amount ?? 0m) <= 1m;

    // 升级后获得保留。
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 「步法 ≤ 1」判断：步法为 0 或 1 时才生效（配合再快一点重做后不再有步法下限）。
        var footwork = Owner.Creature.GetPower<flametailFootworkPower>();
        if ((footwork?.Amount ?? 0m) > 1m)
        {
            return;
        }

        await PowerCmd.Apply<flametailFootworkPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Footwork"].IntValue,
            Owner.Creature,
            this);

        await CardPileCmd.Draw(choiceContext, DynamicVars["DrawAmount"].IntValue, Owner);
    }
}
