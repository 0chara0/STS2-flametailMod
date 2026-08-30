using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 携风：每当所有者（Owner）获得步法时，将每个“携风-盟友”连接上记录的各层步法分别分享给对应盟友。
/// 连接本身（层数、施放者）记录在各盟友身上的 flametailWindbringerLinkPower（InstancedPerApplier：同一施放者对同一盟友叠加层数，不同施放者各自独立）。
/// 因此“给盟友A一次、给盟友B一次”后，A、B 各获得自己连接的层数，互不影响；反复打出可为同一盟友叠加层数。
/// 防环：同一操作轮（一次根事件派生的嵌套触发链）内每个玩家的携风只分享一次，互连时 A→B→A 在第二次 A 处停止，
/// 链条完全展开后清空记录，下一张牌重新结算（详见 _activeShareDepth）。
/// </summary>
[RegisterPower]
public sealed class flametailWindbringerPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 防环用的“当前触发链”状态（进程内全局，多人本地模拟共享同一链）：
    // _activeShareDepth 记录分享链的嵌套深度；_sharedThisRound 记录本轮已分享过的玩家 NetId。
    // 互连时 A 获得步法→分享给 B（触发 B）→B 分享给 A（本轮 A 已分享过，跳过）→链终止。
    // try/finally 保证深度归零时清空记录，下一张牌（或回合开始加步法）按同样逻辑重新结算。
    private static int _activeShareDepth;
    private static readonly HashSet<ulong> _sharedThisRound = new();

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailWindbringerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailWindbringerPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not flametailFootworkPower)
        {
            return;
        }

        if (power.Owner != Owner)
        {
            return;
        }

        // 只在“获得步法”（正增量）时分享；步法转化/扣除产生的负增量忽略。
        if (amount <= 0)
        {
            return;
        }

        if (Owner?.CombatState is not { } combat)
        {
            return;
        }

        if (Owner.Player is not { } ownerPlayer)
        {
            return;
        }

        // 防环：本轮触发链内每个玩家的携风只分享一次。深度 > 0 说明正处在由分享派生的嵌套调用中，
        // 若该玩家本轮已分享过则直接跳过，从而在互连场景终止 A→B→A→B… 的无限往返。
        if (_activeShareDepth > 0 && _sharedThisRound.Contains(ownerPlayer.NetId))
        {
            return;
        }

        _activeShareDepth++;
        _sharedThisRound.Add(ownerPlayer.NetId);
        try
        {
            // 先收集全部（目标，分享层数）快照，再统一 Apply。
            // GetPowerInstances 是对生物 Powers 列表的惰性枚举（_powers.OfType<T>()）；
            // 若在枚举期间给同一生物 Apply 步法并新建/移除步法实例，会修改 Powers 集合，
            // 导致 "Collection was modified; enumeration operation may not execute"（日志已证实）。
            var shares = new List<(Creature target, int share)>();
            foreach (Player p in combat.Players)
            {
                if (p.Creature == null || p.Creature.IsDead)
                {
                    continue;
                }

                foreach (flametailWindbringerLinkPower link in p.Creature.GetPowerInstances<flametailWindbringerLinkPower>())
                {
                    if (!ulong.TryParse(link.ApplierNetId, out ulong applierNetId) || applierNetId != ownerPlayer.NetId)
                    {
                        continue;
                    }

                    int share = (int)link.Amount;
                    if (share <= 0)
                    {
                        continue;
                    }

                    shares.Add((p.Creature, share));
                }
            }

            foreach ((Creature target, int share) in shares)
            {
                await PowerCmd.Apply<flametailFootworkPower>(choiceContext, target, share, Owner, cardSource);
            }
        }
        finally
        {
            _activeShareDepth--;
            if (_activeShareDepth == 0)
            {
                // 触发链完全展开，本轮结束：清空记录，下一张牌重新结算。
                _sharedThisRound.Clear();
            }
        }
    }
}
