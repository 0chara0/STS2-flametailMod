using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using flametail.Cards;
using flametail.Characters;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：让原版先古遗物“先古之牙（ArchaicTooth）”在焰尾角色获得时，
/// 把 <see cref="flametailJab"/> 替换为 <see cref="flametailBlisteringCounter"/>。
/// 这里选择 patch 公共生命周期方法，以便其它 mod 也能在同样的生命周期点上做兼容处理。
/// </summary>
public static class ArchaicToothPatch
{
    private static bool IsFlametail(Player player)
    {
        return player.Character.Id == ModelDb.Character<flametailCharacter>().Id;
    }

    private static CardModel CreateBlisteringCounter(CardModel jab)
    {
        CardModel ancientCard = jab.Owner.RunState.CreateCard<flametailBlisteringCounter>(jab.Owner);

        if (jab.IsUpgraded)
        {
            CardCmd.Upgrade(ancientCard);
        }

        if (jab.Enchantment != null)
        {
            EnchantmentModel enchantment = (EnchantmentModel)jab.Enchantment.MutableClone();
            CardCmd.Enchant(enchantment, ancientCard, enchantment.Amount);
        }

        return ancientCard;
    }

    /// <summary>
    /// 当原版 SetupForPlayer 找不到可超越的基础牌时，如果是焰尾且牌组里有 flametailJab，
    /// 就把该牌设置为要被超越的牌，让遗物祝福选项对焰尾可用。
    /// </summary>
    [HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.SetupForPlayer))]
    private static class SetupForPlayerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ArchaicTooth __instance, Player player, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            if (!IsFlametail(player))
            {
                return;
            }

            CardModel? jab = player.Deck.Cards.FirstOrDefault(c => c.Id == ModelDb.Card<flametailJab>().Id);
            if (jab == null)
            {
                return;
            }

            CardModel ancientCard = CreateBlisteringCounter(jab);
            __instance.SetupForTests(jab.ToSerializable(), ancientCard.ToSerializable());
            __result = true;
        }
    }

    /// <summary>
    /// 获得遗物时，如果该遗物是为焰尾的 flametailJab 设置的，就执行实际的替换。
    /// 若其它 mod 把遗物设置成了其它基础牌，这里直接返回 true，让原版/其它 mod 的逻辑继续执行。
    /// </summary>
    [HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.AfterObtained))]
    private static class AfterObtainedPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ArchaicTooth __instance, ref Task __result)
        {
            Player owner = __instance.Owner;
            if (!IsFlametail(owner))
            {
                return true;
            }

            if (__instance.StarterCard?.Id != ModelDb.Card<flametailJab>().Id)
            {
                return true;
            }

            CardModel? jab = owner.Deck.Cards.FirstOrDefault(c => c.Id == ModelDb.Card<flametailJab>().Id);
            if (jab == null)
            {
                return true;
            }

            __result = TransformJabAsync(jab);
            return false;
        }
    }

    private static async Task TransformJabAsync(CardModel jab)
    {
        CardModel ancientCard = CreateBlisteringCounter(jab);
        await CardCmd.Transform(jab, ancientCard);
    }
}
