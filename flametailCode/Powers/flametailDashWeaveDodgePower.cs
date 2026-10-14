using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 闪！转！腾！挪！的临时 Buff（修改清单 [14] 重做）：本回合，当你交替打出攻击牌和
/// 技能牌时，每打出一张牌，抽 1 张牌，获得 1 点能量。
/// <para>
/// 「交替」判定：只统计攻击/技能两类牌；本次打出的类型与上一次打出的攻击/技能牌
/// 类型不同即视为交替（抽 1 张牌 + 获得 1 点能量），相同类型不触发但会更新记录。
/// 非攻击/技能牌（能力、状态、诅咒等）会<b>打断</b>交替链：清空记录，下一张攻击/技能牌
/// 重新确立起始类型（不触发奖励）（2026-10-14 用户裁定：状态/能力牌打断，不得让后续
/// 攻击牌读到之前的技能牌）。
/// 施加本能力的这张牌本身（技能）也计入——打出它即确立起始类型，不触发奖励。
/// </para>
/// 回合结束时移除（参考原版 RagePower）。
/// </summary>
[RegisterPower]
public sealed class flametailDashWeaveDodgePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    // 图标复用步法图标（占位，与旧版一致；有正式素材后替换）。
    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailFootworkPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailFootworkPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    /// <summary>
    /// 本回合上一次打出的攻击/技能牌类型；-1 表示尚未打出过。用 [SavedProperty]（int 编码）
    /// 而非普通字段：power 的中途状态靠 SavedProperties 联机同步（参考反制管理器的
    /// CountersTriggeredThisCombat），普通字段在同步/重连后会归零，导致两端能量/抽牌分歧。
    /// </summary>
    [SavedProperty]
    public int LastAlternateTypeRaw { get; set; } = -1;

    private CardType? LastAlternateType => LastAlternateTypeRaw >= 0 ? (CardType)LastAlternateTypeRaw : null;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        CardType type = cardPlay.Card.Type;
        if (type is not (CardType.Attack or CardType.Skill))
        {
            // 状态、能力、诅咒等牌打断交替链：清空记录。
            // 若只跳过不清空，攻击牌会读到打断前的技能牌并错误触发（2026-10-14 用户实测）。
            LastAlternateTypeRaw = -1;
            return;
        }

        if (LastAlternateType.HasValue && LastAlternateType.Value != type)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner.Player!);
            await PlayerCmd.GainEnergy(1, Owner.Player!);
        }

        LastAlternateTypeRaw = (int)type;
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
