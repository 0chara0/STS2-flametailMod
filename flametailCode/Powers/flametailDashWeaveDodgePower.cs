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
/// 闪！转！腾！挪！的临时 Buff：本回合每打出一张牌获得一层步法。
/// 打出施加本能力的牌自身也算“打出一张牌”，但**不触发本次打出自己刚叠上去的层**：
/// 例：buff 已有 1 层时再打出闪转腾挪，本次打出只获得 1 层步法（按叠层前的旧层数），
/// 而不是 2 层；首次打出（buff 0→1）不获得步法。
/// 被故技重施/釜底抽薪等效果重放时同理（重放也是一次打出 + 叠一层，按旧层数结算）。
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
    /// 施加/叠加本能力的来源牌，用于在 <see cref="AfterCardPlayed"/> 中识别
    /// “本次打出自己刚添加的层”并在结算时排除。
    /// 在 <see cref="AfterApplied"/> 和 <see cref="AfterPowerAmountChanged"/> 中维护，
    /// 覆盖“首次施加只触发 AfterApplied”和“后续叠加只触发 AfterPowerAmountChanged”两种框架行为。
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

        // 只要是从卡牌来源获得/叠加了本能力，就更新来源牌（每次打出闪转腾挪都会重新记录）。
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

        // 打出施加本能力的牌自身：排除本次打出刚叠上去的 1 层，按叠层前的旧层数结算。
        // （OnPlay 先施加/叠层、AfterCardPlayed 后触发，因此此时 Amount 已含本次新增的 1 层。）
        int grant = (int)Amount;
        if (cardPlay.Card == _sourceCard)
        {
            grant -= 1;
        }

        // 排除只作用于施加它的那一次打出；之后同一次打出结算即清空，
        // 同一实例后续的每次打出（含重放）都会通过叠层重新记录来源。
        _sourceCard = null;

        if (grant > 0)
        {
            await PowerCmd.Apply<flametailFootworkPower>(
                choiceContext,
                Owner,
                grant,
                Owner,
                null);
        }
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
