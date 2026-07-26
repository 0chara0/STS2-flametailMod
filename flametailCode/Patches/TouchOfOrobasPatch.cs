using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using flametail.Characters;
using flametail.Relics;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：让原版先古遗物“奥罗巴斯之触（TouchOfOrobas）”在焰尾角色获得时，
/// 把 <see cref="PinusSylvestrisOrigins"/> 替换为 <see cref="PinusSylvestrisFuture"/>。
/// </summary>
[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.SetupForPlayer))]
public static class TouchOfOrobasPatch
{
    private static bool IsFlametail(Player player)
    {
        return player.Character.Id == ModelDb.Character<flametailCharacter>().Id;
    }

    /// <summary>
    /// 在原版 SetupForPlayer 之前拦截：如果焰尾拥有红松的起点，就把升级目标固定设为红松的未来。
    /// 这样避免原版把焰尾初始遗物误判为 Circlet。
    /// </summary>
    [HarmonyPrefix]
    private static bool Prefix(TouchOfOrobas __instance, Player player, ref bool __result)
    {
        if (!IsFlametail(player))
        {
            return true;
        }

        RelicModel? origins = player.Relics.FirstOrDefault(r => r.Id == ModelDb.Relic<PinusSylvestrisOrigins>().Id);
        if (origins == null)
        {
            return true;
        }

        __instance.StarterRelic = origins.Id;
        __instance.UpgradedRelic = ModelDb.Relic<PinusSylvestrisFuture>().Id;
        __result = true;
        return false;
    }
}
