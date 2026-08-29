using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 携风：选择一名其他玩家。每当你获得步法时，该玩家获得 1 层步法。
/// 多人限定；目标通过 TargetType.AnyAlly 的“选择其他玩家”界面确定（排除自己）。
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

        var power = await PowerCmd.Apply<flametailWindbringerPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);

        if (power != null)
        {
            power.TargetPlayerNetId = targetPlayer.NetId.ToString();
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
