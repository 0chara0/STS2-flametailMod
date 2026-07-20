using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.CardTargeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 剑如火舞：以攻击者为单一目标的反制效果改为对所有敌人生效。
/// </summary>
[RegisterPower]
public sealed class flametailFireDancingSwordPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailFireDancingSwordPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailFireDancingSwordPower.png");

    public override async Task BeforeAttack(AttackCommand cmd)
    {
        if (!CounterSystem.IsCounterPlay)
        {
            return;
        }

        if (cmd.Attacker is not { } attacker || attacker != Owner)
        {
            return;
        }

        if (!cmd.IsSingleTargeted)
        {
            return;
        }

        var targets = cmd.GetPossibleTargets();
        if (targets.Count != 1 || targets[0] != CounterSystem.CurrentAttacker)
        {
            return;
        }

        if (Owner.CombatState is not { } combat)
        {
            return;
        }

        RetargetToAllOpponents(cmd, combat, attacker);
        await Task.CompletedTask;
    }

    private static void RetargetToAllOpponents(AttackCommand cmd, ICombatState combat, Creature attacker)
    {
        // 使用 RitsuLib 的公开扩展，把单目标攻击重定向为所有敌人。
        var allEnemies = combat.GetOpponentsOf(attacker);
        cmd.TargetingFiltered(allEnemies);
    }
}
