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
        ModCardVars.ComputedBlock(
            "Block", 0m,
            (card, target) => ResolveBlock(card),
            ValueProp.Move),
        new IntVar("BlockPerFootwork", 5)
    ];

    public flametailSuddenHalt() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    /// <summary>
    /// 实时计算格挡值：可用步法 × 每点步法格挡。
    /// 卡牌图书馆等非战斗场景渲染的是规范模型，访问 Owner 会抛异常，按无步法显示 0。
    /// </summary>
    private static decimal ResolveBlock(CardModel? card)
    {
        if (card?.CombatState == null)
        {
            return 0m;
        }

        var footwork = card.Owner?.Creature?.GetPower<flametailFootworkPower>();
        if (footwork == null || !footwork.HasUsableFootwork)
        {
            return 0m;
        }

        decimal rate = card.DynamicVars?.GetValueOrDefault("BlockPerFootwork", 5m) ?? 5m;
        return footwork.UsableAmount * rate;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var footwork = Owner.Creature.GetPower<flametailFootworkPower>();
        if (footwork == null || !footwork.HasUsableFootwork)
        {
            return;
        }

        decimal usableFootwork = footwork.UsableAmount;

        // 格挡基于扣减前的可用步法计算，必须先于 ModifyAmount 求值。
        decimal block = DynamicVars.ComputeDynamicValue("Block", 0m, null);
        await PowerCmd.ModifyAmount(choiceContext, footwork, -usableFootwork, Owner.Creature, this);
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BlockPerFootwork"].UpgradeValueBy(2);
    }
}
