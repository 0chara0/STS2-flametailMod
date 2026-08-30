using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 领导者：你存活时，生命值高于一半的玩家获得 2（3）点力量，生命值不高于一半的玩家获得 2（3）点敏捷。
/// 光环由 flametailLeaderPower 打出时建立连接，玩家生命值跨过半血线（或死亡）时由各自身上的 flametailLeaderAuraPower 实时切换力量/敏捷，无需等待回合开始。多人限定。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailLeader : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailLeader"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Amount", 2)
    ];

    // 仅多人模式可用的卡：对全体玩家按血量发放增益，在多人合作中才有完整价值。
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    public flametailLeader() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int amount = DynamicVars["Amount"].IntValue;

        var power = await PowerCmd.Apply<flametailLeaderPower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);

        if (power != null)
        {
            // 立刻按当前血量评估一次，不用等下一个回合开始。
            // 可叠加：Amount 随每次打出累加，Evaluate 按 Amount 授予力量/敏捷。
            await power.Evaluate(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Amount"].UpgradeValueBy(1);
    }
}
