using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using flametail.Relics;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：让欧洛巴斯先古遗物能把焰尾的初始遗物升级为“红松的未来”。
/// </summary>
public static class StarterRelicUpgradePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
    private static void UpgradeFlametailStarter(RelicModel starterRelic, ref RelicModel __result)
    {
        if (starterRelic is PinusSylvestrisOrigins)
        {
            __result = ModelDb.Relic<PinusSylvestrisFuture>().ToMutable();
        }
    }
}
