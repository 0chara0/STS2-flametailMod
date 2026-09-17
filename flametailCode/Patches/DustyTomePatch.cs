using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using flametail.Cards;
using flametail.Characters;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：让原版先古遗物“尘封魔典（DustyTome）”在焰尾角色获得时，
/// 固定提供 <see cref="flametailTwoHandedStyle"/> 而不是从角色卡池中随机挑选。
/// <para>
/// 背景：原版逻辑是随机选取卡池中任意一张 <c>CardRarity.Ancient</c> 的卡。
/// 焰尾卡池中存在两张先古卡——<see cref="flametailBlisteringCounter"/>（先古之牙给闪击的超越版）
/// 与双手剑法。不固定的话尘封魔典会随机二选一，经常给出凌厉反击。
/// </para>
/// </summary>
[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.SetupForPlayer))]
public static class DustyTomePatch
{
    private static bool IsFlametail(Player player)
    {
        return player.Character.Id == ModelDb.Character<flametailCharacter>().Id;
    }

    /// <summary>
    /// 在原版 SetupForPlayer 选完卡后，如果是焰尾角色，把 AncientCard 固定设为 flametailTwoHandedStyle。
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(DustyTome __instance, Player player)
    {
        if (!IsFlametail(player))
        {
            return;
        }

        CardModel twoHandedStyle = ModelDb.Card<flametailTwoHandedStyle>();
        __instance.AncientCard = twoHandedStyle.Id;
    }
}
