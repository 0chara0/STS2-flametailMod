using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;

namespace flametail.Patches;

/// <summary>
/// 让顶部 HUD 使用 flametail 自己的头像场景。
/// RitsuLib 虽然提供了 CharacterIconPathPatch，但它在 0.4.54 里是 internal 且不会自动应用，
/// 因此我们用等价的 Harmony patch 把 CharacterModel.IconPath 重定向到 AssetProfile.Ui.IconPath。
/// </summary>
[HarmonyPatch]
public static class FlametailIconPathPatch
{
    private static readonly MethodInfo IconPathGetter = typeof(CharacterModel)
        .GetProperty("IconPath", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetGetMethod(true)!;

    public static MethodBase TargetMethod() => IconPathGetter;

    public static bool Prefix(CharacterModel __instance, ref string __result)
    {
        if (__instance is flametailCharacter character &&
            character.AssetProfile.Ui?.IconPath is { Length: > 0 } iconPath)
        {
            __result = iconPath;
            return false;
        }

        return true;
    }
}
