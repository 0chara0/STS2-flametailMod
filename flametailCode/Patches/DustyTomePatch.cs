using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using flametail.Cards;
using flametail.Characters;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：让原版先古遗物“尘封之书（DustyTome）”在焰尾角色获得时，
/// 固定提供 <see cref="flametailStepGambit"/> 而不是从角色卡池中随机挑选。
/// </summary>
[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.SetupForPlayer))]
public static class DustyTomePatch
{
    private static bool IsFlametail(Player player)
    {
        return player.Character.Id == ModelDb.Character<flametailCharacter>().Id;
    }

    /// <summary>
    /// 在原版 SetupForPlayer 选完卡后，如果是焰尾角色，把 AncientCard 固定设为 flametailStepGambit。
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(DustyTome __instance, Player player)
    {
        if (!IsFlametail(player))
        {
            return;
        }

        CardModel stepGambit = ModelDb.Card<flametailStepGambit>();
        __instance.AncientCard = stepGambit.Id;
    }
}
