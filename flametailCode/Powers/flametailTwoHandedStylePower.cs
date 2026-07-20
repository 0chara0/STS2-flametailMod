using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

[RegisterPower]
public sealed class flametailTwoHandedStylePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailTwoHandedStylePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailTwoHandedStylePower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    private decimal _pendingBlock;

    /// <summary>
    /// 将本应获得的 Footwork 拦截为待定 Block。
    /// 注意：此实现依赖 <see cref="TryModifyPowerAmountReceived"/> 与
    /// <see cref="AfterModifyingPowerAmountReceived"/> 在同一次 power 修改中成对调用。
    /// 如果 STS2 内部流程改变调用配对方式，这里需要重新设计。
    /// </summary>
    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        if (canonicalPower is flametailFootworkPower && target == Owner && amount > 0)
        {
            _pendingBlock += amount;
            modifiedAmount = 0m;
            return true;
        }

        modifiedAmount = amount;
        return false;
    }

    public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        if (_pendingBlock <= 0)
        {
            return;
        }

        decimal block = _pendingBlock * Amount;
        _pendingBlock = 0m;

        await CreatureCmd.GainBlock(Owner, block, ValueProp.Unpowered, null);
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer != Owner)
        {
            return 1m;
        }

        if (!props.IsPoweredAttack())
        {
            return 1m;
        }

        return 1.5m;
    }
}
