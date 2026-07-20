using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 剑如火舞：以攻击者为单一目标的反制效果改为对所有敌人生效。
/// </summary>
[RegisterPower]
public sealed class flametailFireDancingSwordPower : ModPowerTemplate
{
    private static readonly FieldInfo? SingleTargetField = typeof(AttackCommand).GetField(
        "_singleTarget",
        BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? CombatStateField = typeof(AttackCommand).GetField(
        "_combatState",
        BindingFlags.NonPublic | BindingFlags.Instance);

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

        if (cmd.Attacker != Owner)
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

        RetargetToAllOpponents(cmd, combat);
        await Task.CompletedTask;
    }

    private static void RetargetToAllOpponents(AttackCommand cmd, ICombatState combat)
    {
        // AttackCommand 只允许在目标未设置时切换为 AOE。这里通过反射清空已设置的单目标，
        // 使其能够安全地切换到对所有敌人，从而保留卡牌的其它效果逻辑。
        // 这是当前公开 API 下实现该效果的最小侵入方式；STS2 内部字段变化时需要重新验证。
        if (SingleTargetField == null || CombatStateField == null)
        {
            Godot.GD.PushWarning(
                "flametailFireDancingSwordPower: required AttackCommand fields are missing; retargeting disabled.");
            return;
        }

        SingleTargetField.SetValue(cmd, null);
        CombatStateField.SetValue(cmd, null);

        cmd.TargetingAllOpponents(combat);
    }
}
