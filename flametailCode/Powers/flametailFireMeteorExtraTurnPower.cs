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
/// 隐藏 Buff：下回合额外行动一回合。
/// </summary>
[RegisterPower]
public sealed class flametailFireMeteorExtraTurnPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailFireMeteorExtraTurnPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailFireMeteorExtraTurnPower.png");

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player.Creature == Owner;
    }

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await PowerCmd.Remove(this);
    }
}
