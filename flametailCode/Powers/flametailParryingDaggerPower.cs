using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 招架匕首（修改文档：触发时机提前至敌人行动前，不再随每张反制牌的打出触发）：
/// <para>① 拥有先发制人时，若有敌人行动且有能反制的牌，则在敌人行动前触发一次；</para>
/// <para>② 若敌人意图为攻击且手牌中有能反制的牌，则在敌人行动前触发一次；</para>
/// <para>③ 由百战先锋/骑士对决打出反制牌前触发一次。</para>
/// 效果：获得 Amount 点格挡并对该敌人造成 Amount 点伤害。
/// <para>
/// ①②由 <see cref="PreemptTakeTurnPatch"/>（敌人 TakeTurn 前）统一判定并触发，
/// 两者条件重叠时该次行动只触发一次；③由百战先锋/骑士对决在重放前显式调用。
/// 触发入口见 <see cref="TriggerForOwner"/>；“能反制的牌”与先发制人的选牌条件一致
/// （ICounterCard + CanAutoPlayAsCounter + CanBePlayedBySupportEffects）。
/// </para>
/// </summary>
[RegisterPower]
public sealed class flametailParryingDaggerPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailParryingDaggerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailParryingDaggerPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    /// <summary>
    /// 对 owner 触发一次招架匕首（先格挡后对 attacker 伤害）；owner 未持有本能力时无事发生。
    /// </summary>
    public static async Task TriggerForOwner(
        PlayerChoiceContext choiceContext,
        Creature owner,
        Creature? attacker)
    {
        if (owner.GetPower<flametailParryingDaggerPower>() is not { } dagger)
        {
            return;
        }

        await dagger.Trigger(choiceContext, attacker);
    }

    /// <summary>
    /// 手牌中是否有能反制 enemy 的牌（与先发制人的选牌条件一致）。
    /// </summary>
    public static bool HasCounterableCard(Player player, Creature enemy)
    {
        CardPile? hand = player.PlayerCombatState?.Hand;
        if (hand == null)
        {
            return false;
        }

        foreach (CardModel card in hand.Cards)
        {
            if (card is ICounterCard counter
                && counter.CanAutoPlayAsCounter(enemy)
                && counter.CanBePlayedBySupportEffects)
            {
                return true;
            }
        }

        return false;
    }

    private async Task Trigger(PlayerChoiceContext choiceContext, Creature? attacker)
    {
        // 先格挡后伤害，保证格挡能在随后的敌人攻击前生效。
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);

        // 直接伤害（Thorns/Thunder 同款 CreatureCmd.Damage）：①② 时机没有卡牌来源，
        // 且原实现为 Unpowered 攻击，本就不吃力量/易伤类修正，语义等价。
        if (attacker is { IsDead: false })
        {
            Flash();
            await CreatureCmd.Damage(choiceContext, attacker, Amount, ValueProp.Unpowered, Owner);
        }
    }
}
