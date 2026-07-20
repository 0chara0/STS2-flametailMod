using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 步法：拥有 5 层时立即转化为 1 层闪避；回合开始前减少 1 层。
/// </summary>
[RegisterPower]
public sealed class flametailFootworkPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailFootworkPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailFootworkPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    // 防止在 AfterPowerAmountChanged 中修改步法时触发无限递归。
    private bool _isConverting;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 只对自己生效
        if (player.Creature != Owner)
        {
            return;
        }

        // 回合开始时 -1；转化的逻辑在 AfterPowerAmountChanged 中随时处理
        if (Owner.HasPower<flametailSteadyStepPower>())
        {
            return;
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (_isConverting || power != this)
        {
            return;
        }

        if (Owner == null)
        {
            return;
        }

        // 若步法被外部效果归零则移除
        if (amount <= 0)
        {
            if (Amount <= 0)
            {
                await PowerCmd.Remove(this);
            }
            return;
        }

        int dodgeGain = Amount / 5;
        if (dodgeGain <= 0)
        {
            return;
        }

        _isConverting = true;

        int remainingFootwork = Amount - dodgeGain * 5;
        if (remainingFootwork <= 0)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.ModifyAmount(choiceContext, this, remainingFootwork - Amount, Owner, null);
        }

        await PowerCmd.Apply<flametailDodgePower>(choiceContext, Owner, dodgeGain, Owner, null);

        _isConverting = false;
    }

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        if (canonicalPower is not flametailFootworkPower
            || target != Owner
            || amount >= 0
            || !Owner.HasPower<flametailEvenFasterPower>())
        {
            modifiedAmount = amount;
            return false;
        }

        // 再快一点：步法不能低于 2。
        decimal newAmount = Amount + amount;
        if (newAmount >= 2m)
        {
            modifiedAmount = amount;
            return false;
        }

        modifiedAmount = Math.Max(amount, 2m - Amount);
        return modifiedAmount != amount;
    }
}
