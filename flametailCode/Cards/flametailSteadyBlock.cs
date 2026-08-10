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
public sealed class flametailSteadyBlock : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailSteadyBlock"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.ComputedBlock(
            "Block", 0m,
            (card, target) => ResolveBlock(card),
            ValueProp.Move),
        new IntVar("BlockPerFootwork", 3),
        new IntVar("MaxFootwork", 5)
    ];

    public flametailSteadyBlock() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    /// <summary>
    /// 实时计算格挡值：(最大步法 − 当前步法) × 每点步法格挡。
    /// 卡牌图书馆等非战斗场景渲染的是规范模型，访问 Owner 会抛异常，按无步法计算即满额格挡。
    /// </summary>
    private static decimal ResolveBlock(CardModel? card)
    {
        if (card == null)
        {
            return 0m;
        }

        decimal maxFootwork = card.DynamicVars?.GetValueOrDefault("MaxFootwork", 5m) ?? 5m;
        decimal rate = card.DynamicVars?.GetValueOrDefault("BlockPerFootwork", 4m) ?? 4m;

        if (card.CombatState == null)
        {
            return maxFootwork * rate;
        }

        decimal footwork = card.Owner?.Creature?.GetPower<flametailFootworkPower>()?.Amount ?? 0m;
        decimal missing = Math.Max(0m, maxFootwork - footwork);
        return missing * rate;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int footwork = Owner.Creature.GetPower<flametailFootworkPower>()?.Amount ?? 0;
        int missing = int.Max(0, DynamicVars["MaxFootwork"].IntValue - footwork);
        if (missing > 0)
        {
            decimal block = DynamicVars.ComputeDynamicValue("Block", 0m, null);
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BlockPerFootwork"].UpgradeValueBy(1);
    }
}
