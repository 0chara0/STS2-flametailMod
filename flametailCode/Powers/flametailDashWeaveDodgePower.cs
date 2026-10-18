using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 闪！转！腾！挪！的临时管理能力（修改清单 [14] 重做，2026-10-18 削弱）：
/// 本回合，当你交替打出攻击牌和技能牌时，打出攻击牌后抽 1 张牌，打出技能牌后获得 1 点能量。
/// <para>
/// 「上一张打出的攻击/技能牌类型」物化为 BUFF 栏可见的两个标记能力（2026-10-18 用户裁定）：
/// 打完攻击牌挂 <see cref="flametailDashWeaveAttackMarkPower"/>，打完技能牌挂
/// <see cref="flametailDashWeaveSkillMarkPower"/>；触发奖励以「对面类型标记存在」判定，
/// 打完即换标记。状态=标记在不在，天然随 power 存在性联机同步（替代旧 SavedProperty）。
/// 非攻击/技能牌（能力、状态、诅咒等）会<b>打断</b>交替链：清空两个标记，下一张攻击/技能牌
/// 重新确立起始类型（不触发奖励）（2026-10-14 用户裁定：状态/能力牌打断，不得让后续
/// 攻击牌读到之前的技能牌）。
/// 施加本能力的这张牌本身（技能）也计入——打出它即确立起始类型，不触发奖励。
/// </para>
/// 本能力自身对玩家隐藏（<see cref="IsVisibleInternal"/> = false），BUFF 栏只显示标记。
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

    // 隐藏管理能力：状态展示交给两个标记，BUFF 栏不出现第三个条目。
    protected override bool IsVisibleInternal => false;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        CardType type = cardPlay.Card.Type;
        if (type is not (CardType.Attack or CardType.Skill))
        {
            // 状态、能力、诅咒等牌打断交替链：清空两个标记。
            // 若只跳过不清空，攻击牌会读到打断前的技能牌并错误触发（2026-10-14 用户实测）。
            await PowerCmd.Remove<flametailDashWeaveAttackMarkPower>(Owner);
            await PowerCmd.Remove<flametailDashWeaveSkillMarkPower>(Owner);
            return;
        }

        bool previousWasAttack = Owner.GetPower<flametailDashWeaveAttackMarkPower>() != null;
        bool previousWasSkill = Owner.GetPower<flametailDashWeaveSkillMarkPower>() != null;

        // 打完即换标记：无论是否触发，先清掉两个旧标记，再挂上本次类型的标记。
        await PowerCmd.Remove<flametailDashWeaveAttackMarkPower>(Owner);
        await PowerCmd.Remove<flametailDashWeaveSkillMarkPower>(Owner);

        if (type == CardType.Attack)
        {
            // 打出攻击牌（与上一张技能牌交替）：只抽 1 张牌，不给能量（2026-10-18 削弱）。
            if (previousWasSkill)
            {
                await CardPileCmd.Draw(choiceContext, 1, Owner.Player!);
            }

            await PowerCmd.Apply<flametailDashWeaveAttackMarkPower>(
                choiceContext,
                Owner,
                1,
                Owner,
                cardPlay.Card);
        }
        else
        {
            // 打出技能牌（与上一张攻击牌交替）：只获得 1 点能量，不抽牌（2026-10-18 削弱）。
            if (previousWasAttack)
            {
                await PlayerCmd.GainEnergy(1, Owner.Player!);
            }

            await PowerCmd.Apply<flametailDashWeaveSkillMarkPower>(
                choiceContext,
                Owner,
                1,
                Owner,
                cardPlay.Card);
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

        await PowerCmd.Remove<flametailDashWeaveAttackMarkPower>(Owner);
        await PowerCmd.Remove<flametailDashWeaveSkillMarkPower>(Owner);
        await PowerCmd.Remove(this);
    }
}
