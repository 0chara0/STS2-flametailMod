using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 隐藏 Buff：下一张攻击牌无视格挡。由「虚晃」施加。
/// </summary>
[RegisterPower]
public sealed class flametailIgnoreBlockPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailIgnoreBlockPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailIgnoreBlockPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task BeforeAttack(AttackCommand command)
    {
        if (command.Attacker != Owner)
        {
            return;
        }

        command.DamageProps |= ValueProp.Unblockable;
        await PowerCmd.Remove(this);
    }
}
