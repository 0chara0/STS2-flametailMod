using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 隐藏 Buff：下回合开始时获得能量。
/// </summary>
[RegisterPower]
public sealed class flametailEnergyNextTurnPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailEnergyNextTurnPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailEnergyNextTurnPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (Amount > 0)
        {
            await PlayerCmd.GainEnergy(Amount, player);
        }

        await PowerCmd.Remove(this);
    }
}
