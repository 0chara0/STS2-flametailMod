using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 隐藏 Buff：在敌人回合结束时恢复其力量。
/// 用于「以巧化力」的临时力量减益。
/// </summary>
[RegisterPower]
public sealed class flametailRestoreStrengthPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailRestoreStrengthPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailRestoreStrengthPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        if (Amount > 0)
        {
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                Owner,
                Amount,
                Owner,
                null);
        }

        await PowerCmd.Remove(this);
    }
}