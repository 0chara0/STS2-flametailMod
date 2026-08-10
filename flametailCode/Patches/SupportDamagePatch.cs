using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：让带有 <see cref="FlametailValueProps.GetIgnoreAttackerDamageModifiers"/> 的伤害
/// 1) 在伤害修正阶段跳过攻击方（dealer）拥有的力量/遗物加成，但仍受目标方（target）的易伤/虚弱等影响；
/// 2) 跳过目标方的受击反应类效果，例如荆棘（Thorns）、人工蜂巢（Personal Hive）、反射（Reflect）。
/// </summary>
public static class SupportDamagePatch
{
    [HarmonyPatch(typeof(Hook), "ModifyDamageInternal")]
    private static class ModifyDamageInternalPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);
            var skipMethod = AccessTools.Method(
                typeof(Helpers.FlametailValueProps),
                nameof(Helpers.FlametailValueProps.ShouldSkipAttackerModifier));

            if (skipMethod == null)
            {
                throw new InvalidOperationException(
                    "SupportDamagePatch: could not resolve FlametailValueProps.ShouldSkipAttackerModifier.");
            }

            // 记录是否实际插入了守卫。如果游戏更新后 IL 结构变化导致模式匹配失败，
            // 宁可大声报错，也不能静默失效（否则支援伤害会在无人察觉时恢复受攻击方加成）。
            bool matched = false;

            for (int i = 0; i < codes.Count - 2; i++)
            {
                // 定位 foreach 循环体开头：callvirt get_Current(); stloc item; ldloc item
                if (!IsCallvirt(codes[i], "get_Current")
                    || !IsStloc(codes[i + 1])
                    || !IsLdloc(codes[i + 2]))
                {
                    continue;
                }

                var bodyLoadInstr = codes[i + 2];

                // 向前找到 MoveNext，定位循环条件入口（ldloc enumerator）
                int moveNextIndex = -1;
                for (int j = i + 3; j < codes.Count; j++)
                {
                    if (IsCallvirt(codes[j], "MoveNext"))
                    {
                        moveNextIndex = j;
                        break;
                    }
                }

                if (moveNextIndex <= 0)
                {
                    throw new InvalidOperationException(
                        "SupportDamagePatch: could not find MoveNext after get_Current.");
                }

                var loopConditionInstr = codes[moveNextIndex - 1];
                var loopConditionLabel = il.DefineLabel();
                loopConditionInstr.labels.Add(loopConditionLabel);

                var continueLabel = il.DefineLabel();
                bodyLoadInstr.labels.Add(continueLabel);

                // 在循环体开头插入守卫：
                // if (ShouldSkipAttackerModifier(item, props, dealer)) continue;
                var loadClone = bodyLoadInstr.Clone();
                loadClone.labels.Clear();

                var guard = new[]
                {
                    loadClone,
                    new CodeInstruction(OpCodes.Ldarg_S, (byte)5), // props
                    new CodeInstruction(OpCodes.Ldarg_S, (byte)3), // dealer
                    new CodeInstruction(OpCodes.Call, skipMethod),
                    new CodeInstruction(OpCodes.Brfalse_S, continueLabel),
                    new CodeInstruction(OpCodes.Br, loopConditionLabel)
                };

                codes.InsertRange(i + 2, guard);

                matched = true;

                // 跳过本循环已处理区域：2 条原指令 + 6 条插入指令 + 原 ldloc
                i += 7;
            }

            if (!matched)
            {
                throw new InvalidOperationException(
                    "SupportDamagePatch: no matching foreach pattern found in Hook.ModifyDamageInternal. "
                    + "The game IL structure has likely changed; update this transpiler.");
            }

            return codes;
        }

        private static bool IsCallvirt(CodeInstruction instr, string methodName)
        {
            return instr.opcode == OpCodes.Callvirt
                && instr.operand is MethodInfo mi
                && mi.Name == methodName;
        }

        private static bool IsStloc(CodeInstruction instr)
        {
            return instr.opcode == OpCodes.Stloc
                || instr.opcode == OpCodes.Stloc_S
                || instr.opcode == OpCodes.Stloc_0
                || instr.opcode == OpCodes.Stloc_1
                || instr.opcode == OpCodes.Stloc_2
                || instr.opcode == OpCodes.Stloc_3;
        }

        private static bool IsLdloc(CodeInstruction instr)
        {
            return instr.opcode == OpCodes.Ldloc
                || instr.opcode == OpCodes.Ldloc_S
                || instr.opcode == OpCodes.Ldloc_0
                || instr.opcode == OpCodes.Ldloc_1
                || instr.opcode == OpCodes.Ldloc_2
                || instr.opcode == OpCodes.Ldloc_3;
        }
    }

    /// <summary>
    /// 当伤害带有 <see cref="FlametailValueProps.GetIgnoreAttackerDamageModifiers"/> 时，
    /// 跳过荆棘（Thorns）的反击伤害。
    /// 使用 Prefix + ref Task __result 跳过异步原方法，避免状态机挂起。
    /// </summary>
    [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.ThornsPower), nameof(MegaCrit.Sts2.Core.Models.Powers.ThornsPower.BeforeDamageReceived))]
    private static class ThornsPowerPatch
    {
        private static bool Prefix(Creature target, ValueProp props, PowerModel __instance, ref Task __result)
        {
            if (!props.HasFlag(Helpers.FlametailValueProps.GetIgnoreAttackerDamageModifiers()))
            {
                return true;
            }

            if (target != __instance.Owner)
            {
                return true;
            }

            __result = Task.CompletedTask;
            return false;
        }
    }

    /// <summary>
    /// 当伤害带有 <see cref="FlametailValueProps.GetIgnoreAttackerDamageModifiers"/> 时，
    /// 跳过人工蜂巢（Personal Hive）向抽牌堆添加眩晕牌的效果。
    /// </summary>
    [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.PersonalHivePower), nameof(MegaCrit.Sts2.Core.Models.Powers.PersonalHivePower.AfterDamageReceived))]
    private static class PersonalHivePowerPatch
    {
        private static bool Prefix(Creature target, ValueProp props, PowerModel __instance, ref Task __result)
        {
            if (!props.HasFlag(Helpers.FlametailValueProps.GetIgnoreAttackerDamageModifiers()))
            {
                return true;
            }

            if (target != __instance.Owner)
            {
                return true;
            }

            __result = Task.CompletedTask;
            return false;
        }
    }

    /// <summary>
    /// 当伤害带有 <see cref="FlametailValueProps.GetIgnoreAttackerDamageModifiers"/> 时，
    /// 跳过反射（Reflect）将格挡伤害返还给攻击者的效果。
    /// </summary>
    [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.ReflectPower), nameof(MegaCrit.Sts2.Core.Models.Powers.ReflectPower.AfterDamageReceived))]
    private static class ReflectPowerPatch
    {
        private static bool Prefix(Creature target, ValueProp props, PowerModel __instance, ref Task __result)
        {
            if (!props.HasFlag(Helpers.FlametailValueProps.GetIgnoreAttackerDamageModifiers()))
            {
                return true;
            }

            if (target != __instance.Owner)
            {
                return true;
            }

            __result = Task.CompletedTask;
            return false;
        }
    }
}
