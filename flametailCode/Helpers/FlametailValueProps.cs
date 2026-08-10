using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace flametail.Helpers;

/// <summary>
/// 自定义伤害属性标志。由于 <see cref="ValueProp"/> 是基游戏枚举，mod 只能使用未占用的位。
/// 这里在运行时动态查找一个未被游戏任何已定义值使用的位，避免硬编码可能与游戏版本或其他 mod 冲突。
/// </summary>
public static class FlametailValueProps
{
    private static readonly ValueProp IgnoreAttackerDamageModifiers = AllocateUnusedValuePropBit();

    static FlametailValueProps()
    {
        Entry.Logger.Info($"FlametailValueProps: allocated IgnoreAttackerDamageModifiers = 0x{(ulong)IgnoreAttackerDamageModifiers:X}.");
    }

    /// <summary>
    /// 当此标志存在时：
    /// 1. 伤害结算会忽略攻击方（dealer）拥有的力量/遗物/药水/球/卡牌等加成，
    ///    但仍会应用目标方（target）的力量/遗物等伤害修正效果（如易伤、虚弱、硬化外壳等）。
    /// 2. 同时会跳过目标方的受击反应类效果，例如荆棘（Thorns）、人工蜂巢（Personal Hive）、
    ///    反射（Reflect）等。
    ///
    /// 此标志目前仅用于“支援伤害”。
    /// </summary>
    public static ValueProp GetIgnoreAttackerDamageModifiers() => IgnoreAttackerDamageModifiers;

    private static ValueProp AllocateUnusedValuePropBit()
    {
        var defined = new HashSet<ulong>();
        foreach (ValueProp value in Enum.GetValues<ValueProp>())
        {
            defined.Add((ulong)value);
        }

        for (int bit = 0; bit < 64; bit++)
        {
            ulong candidate = 1UL << bit;
            bool used = false;
            foreach (ulong v in defined)
            {
                if ((v & candidate) != 0)
                {
                    used = true;
                    break;
                }
            }

            if (!used)
            {
                return (ValueProp)candidate;
            }
        }

        throw new InvalidOperationException("No unused ValueProp bit available.");
    }

    /// <summary>
    /// 判断当前 hook 监听器是否应当被跳过。
    /// 仅当伤害带有 <see cref="IgnoreAttackerDamageModifiers"/> 且监听器为攻击方（dealer）拥有时返回 true。
    /// </summary>
    public static bool ShouldSkipAttackerModifier(AbstractModel model, ValueProp props, Creature? dealer)
    {
        if (!props.HasFlag(IgnoreAttackerDamageModifiers))
        {
            return false;
        }

        if (dealer == null)
        {
            return false;
        }

        Player? dealerPlayer = dealer.Player;

        return model switch
        {
            PowerModel power => power.Owner == dealer,
            RelicModel relic => relic.Owner == dealerPlayer,
            PotionModel potion => potion.Owner == dealerPlayer,
            OrbModel orb => orb.Owner == dealerPlayer,
            CardModel card => card.Owner == dealerPlayer,
            AfflictionModel affliction => affliction.Card?.Owner == dealerPlayer,
            EnchantmentModel enchantment => enchantment.Card?.Owner == dealerPlayer,
            _ => false
        };
    }

    /// <summary>
    /// 判断目标方的受击反应类效果是否应当被跳过。
    /// 用于荆棘（Thorns）、人工蜂巢（Personal Hive）、反射（Reflect）等异步方法的开场守卫。
    /// </summary>
    public static bool ShouldSkipTargetReactiveEffect(ValueProp props, Creature? target, PowerModel power)
    {
        return props.HasFlag(IgnoreAttackerDamageModifiers) && target == power.Owner;
    }
}
