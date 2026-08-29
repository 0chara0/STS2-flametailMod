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
    /// 可供卡牌效果使用的步法层数。旧「再快一点」的步法下限已移除，故等于当前层数。
    /// </summary>
    public decimal UsableAmount => Amount;

    /// <summary>
    /// 是否拥有可用步法。
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

        decimal remainingFootwork = Amount - dodgeGain * 5m;
        // 统一用 ModifyAmount（触发 AfterPowerAmountChanged 让「看得清吗」等响应失去步法），
        // 归零时由 ShouldRemoveDueToAmount 自动移除本能力。
        await PowerCmd.ModifyAmount(choiceContext, this, remainingFootwork - Amount, Owner, null);

        await PowerCmd.Apply<flametailDodgePower>(choiceContext, Owner, dodgeGain, Owner, null);

        _isConverting = false;
    }
}
