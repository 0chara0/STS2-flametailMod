using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 携风：选择一名其他玩家建立连接。每当你获得步法时，该玩家获得与其“携风-盟友”标记相同层数的步法。
/// 多人限定；目标通过 TargetType.AnyAlly 的“选择其他玩家”界面确定（排除自己）。
/// 连接记录在目标身上的 flametailWindbringerLinkPower：同一施放者重复打出叠加层数，不同施放者各自独立。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailWindbringer : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyAlly;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailWindbringer"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    // 仅多人模式可用的卡：需要选择另一名玩家作为共享对象，单人模式没有意义。
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    public flametailWindbringer() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // cardPlay.Target 由 TargetType.AnyAlly 的多人在线目标选择界面给出（排除自己）。
        var targetPlayer = cardPlay.Target?.Player;
        if (targetPlayer == null)
        {
            return;
        }

        // 施放者侧：确保“获得步法”的分享触发能力存在（Amount 仅作累计计数，分享层数由各连接标记决定）。
        var power = await PowerCmd.Apply<flametailWindbringerPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);

        if (power == null)
        {
            return;
        }

        // 在目标盟友身上建立/叠加“携风-盟友”连接（InstancedPerApplier：同一施放者重复打出叠加层数，不同施放者各自独立）。
        // 之后每当施放者获得步法，该连接按自身层数分享给这位盟友。
        await PowerCmd.Apply<flametailWindbringerLinkPower>(
            choiceContext,
            targetPlayer.Creature,
            1,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
