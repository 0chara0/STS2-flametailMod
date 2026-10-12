using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;

namespace flametail.Patches;

/// <summary>
/// Use the Flametail frame artwork for Flametail cards while leaving the game's
/// portrait, portrait border, banner, energy icon, cost text, and other layers intact.
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Frame), MethodType.Getter)]
internal static class FlametailCardFramePatch
{
    private static Texture2D? _attackFrame;
    private static Texture2D? _skillFrame;
    private static Texture2D? _powerFrame;

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref Texture2D __result)
    {
        if (__instance.Rarity == CardRarity.Ancient
            || (__instance.VisualCardPool is not flametailCardPool
                && __instance.VisualCardPool is not flametailHiddenCardPool))
        {
            return;
        }

        Texture2D? frameTexture = __instance.Type switch
        {
            CardType.Attack => _attackFrame ??= LoadFrame("attack"),
            CardType.Power => _powerFrame ??= LoadFrame("power"),
            _ => _skillFrame ??= LoadFrame("skill"),
        };

        if (frameTexture != null)
        {
            __result = frameTexture;
        }
    }

    private static Texture2D? LoadFrame(string kind) => ResourceLoader.Load<Texture2D>(
        $"{Entry.ResPath}/images/cards/flametail_card_frame_{kind}.png");
}
