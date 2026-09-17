using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 携风：每当你获得步法时，所有玩家各获得 2（升级 3）点格挡。
/// 多人限定。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailWindbringer : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailWindbringer"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    // 仅多人模式可用的卡：需要选择另一名玩家作为共享对象，单人模式没有意义。
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    // 格挡量由 BlockVar 驱动（升级 +1）。BlockVar 没有单参构造，与普通格挡牌一致使用 ValueProp.Move。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(2, ValueProp.Move)
    ];

    public flametailWindbringer() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Amount = 格挡量：每当所有者获得步法时，所有玩家各获得 Amount 点格挡。
        await PowerCmd.Apply<flametailWindbringerPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Block.IntValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}
