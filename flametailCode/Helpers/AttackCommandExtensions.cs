using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.ValueProps;

namespace flametail.Helpers;

/// <summary>
/// 给 <see cref="AttackCommand"/> 补充无法通过公开 API 设置的自定义伤害属性。
/// </summary>
public static class AttackCommandExtensions
{
    private static readonly Action<AttackCommand, ValueProp>? _damagePropsSetter;

    static AttackCommandExtensions()
    {
        var property = typeof(AttackCommand).GetProperty(
            nameof(AttackCommand.DamageProps),
            BindingFlags.Public | BindingFlags.Instance);

        var setter = property?.GetSetMethod(nonPublic: true);
        if (setter != null)
        {
            _damagePropsSetter = (Action<AttackCommand, ValueProp>)Delegate.CreateDelegate(
                typeof(Action<AttackCommand, ValueProp>),
                setter);
        }
    }

    /// <summary>
    /// 标记本次攻击应忽略攻击方（dealer）自身的力量/遗物/药水/球/卡牌等加成，
    /// 但仍会应用目标方（target）的力量/遗物等效果（如易伤、夹击等）。
    /// </summary>
    public static AttackCommand IgnoreAttackerModifiers(this AttackCommand command)
    {
        if (_damagePropsSetter == null)
        {
            return command;
        }

        _damagePropsSetter(
            command,
            command.DamageProps | FlametailValueProps.IgnoreAttackerDamageModifiers);

        return command;
    }
}
