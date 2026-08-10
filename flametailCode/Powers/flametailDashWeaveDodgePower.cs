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

    /// <summary>
    /// 施加本能力的来源牌。打出这张牌本身不会触发步法增益。
    /// 在 <see cref="AfterApplied"/> 和 <see cref="AfterPowerAmountChanged"/> 中维护，
    /// 以覆盖“首次施加只触发 AfterApplied”和“后续叠加只触发 AfterPowerAmountChanged”
    /// 两种框架行为，不依赖一次性内部标志。
    /// </summary>
    private CardModel? _sourceCard;

    public override Task AfterApplied(Creature? owner, CardModel? source)
    {
        if (source != null)
        {
            _sourceCard = source;
        }

        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power != this)
        {
            return Task.CompletedTask;
        }

        // 只要是从卡牌来源获得/叠加了本能力，就更新来源牌。
        if (amount > 0 && cardSource != null)
        {
            _sourceCard = cardSource;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        // 打出施加本能力的牌本身不获得步法。
        if (cardPlay.Card == _sourceCard)
        {
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
