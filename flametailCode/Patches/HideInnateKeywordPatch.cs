using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using flametail.Cards;

namespace flametail.Patches;

/// <summary>
/// 烛火一闪保留原生“固有”(Innate) 关键字以维持开局进入手牌的功能，
/// 但按修改要求卡面上不显示引擎自动注入的“固有。”独立行，
/// 改由描述文本内联“[gold]固有[/gold]。[gold]即逝[/gold]。”高亮呈现。
/// <para>
/// 原版 <see cref="CardModel"/> 生成卡牌描述时，会按 CardKeywordOrder 把 Innate 等
/// 关键字自动注入为描述前的独立行。此补丁在描述生成后，仅对烛火一闪剥除该行
/// （2 参数公开重载委托给 3 参数私有重载，两个重载都命中，Replace 幂等）。
/// 只影响本地渲染文本，不改变任何游戏状态，联机安全。
/// </para>
/// <para>
/// 注意：不能用 [HarmonyPatch(typeof, "GetDescriptionForPile")] 按名字匹配——
/// 该方法有两个重载，Harmony 会抛 AmbiguousMatchException 导致补丁静默失效
/// （需用 <see cref="HarmonyTargetMethods"/> 显式枚举全部重载）。
/// </para>
/// </summary>
[HarmonyPatch]
public static class HideInnateKeywordPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(CardModel))
            .Where(m => m.Name == "GetDescriptionForPile");
    }

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref string __result)
    {
        if (__instance is not flametailCandleFlash || string.IsNullOrEmpty(__result))
        {
            return;
        }

        string innateLine = CardKeyword.Innate.GetCardText();
        __result = __result.Replace(innateLine + "\n", string.Empty);
    }
}
