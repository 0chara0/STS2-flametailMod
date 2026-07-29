using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 步法：拥有 5 层时立即转化为 1 层闪避。
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

    /// <summary>
    /// 当前步法层数的最低值。若拥有“再快一点”，则最低值为 2；否则为 0。
    /// </summary>
    public decimal MinimumAmount => Owner?.HasPower<flametailEvenFasterPower>() == true ? flametailEvenFasterPower.MinimumFootwork : 0m;

    /// <summary>
    /// 超出最低值、可供卡牌效果使用的步法层数。
    /// </summary>
    public decimal UsableAmount => Math.Max(Amount - MinimumAmount, 0m);

    /// <summary>
    /// 是否拥有超出最低值的可用步法。
    /// </summary>
    public bool HasUsableFootwork => UsableAmount > 0m;

    // 防止在 AfterPowerAmountChanged 中修改步法时触发无限递归。
    private bool _isConverting;

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

        int dodgeGain = (int)(Amount / 5m);
        if (dodgeGain <= 0)
        {
            return;
        }

        _isConverting = true;

        decimal minimum = MinimumAmount;
        decimal remainingFootwork = Amount - dodgeGain * 5m;
        decimal finalFootwork = Math.Max(remainingFootwork, minimum);
        if (finalFootwork <= 0)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.ModifyAmount(choiceContext, this, finalFootwork - Amount, Owner, null);
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
            || Owner == null
            || amount >= 0)
        {
            modifiedAmount = amount;
            return false;
        }

        decimal minimum = MinimumAmount;
        if (minimum <= 0m)
        {
            modifiedAmount = amount;
            return false;
        }

        // 再快一点：步法不能低于最低值。
        decimal newAmount = Amount + amount;
        if (newAmount >= minimum)
        {
            modifiedAmount = amount;
            return false;
        }

        modifiedAmount = Math.Max(amount, minimum - Amount);
        return modifiedAmount != amount;
    }
}
