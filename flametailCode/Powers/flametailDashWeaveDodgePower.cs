using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 闪！转！腾！挪！的临时 Buff：之后每打出一张牌获得一层步法。
/// 参考原版 RagePower 的实现，在回合结束时移除。
/// </summary>
[RegisterPower]
public sealed class flametailDashWeaveDodgePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailFootworkPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailFootworkPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    private class Data
    {
        /// <summary>
        /// 用于跳过打出这张牌本身触发的第一次 AfterCardPlayed，
        /// 避免打出闪！转！腾！挪！时立即获得步法。
        /// </summary>
        public bool AlreadyApplied;
    }

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        if (!data.AlreadyApplied)
        {
            data.AlreadyApplied = true;
            return;
        }

        await PowerCmd.Apply<flametailFootworkPower>(
            choiceContext,
            Owner,
            Amount,
            Owner,
            null);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        await PowerCmd.Remove(this);
    }
}
