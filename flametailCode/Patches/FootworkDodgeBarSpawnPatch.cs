using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using flametail.Nodes;
using flametail.Powers;

namespace flametail.Patches;

/// <summary>
/// Harmony 补丁：
/// 1. 战斗房间加入角色节点时，给玩家角色挂头顶步法/闪避条
///    （<see cref="FootworkDodgeBar"/>，战斗中常驻）。延迟一帧执行，确保 NCreature 已就绪。
/// 2. 状态栏（NPowerContainer）对步法/闪避两个 power 跳过，实现「不在状态栏显示」。
/// 3. 角色本体悬浮聚合（Creature.HoverTips）剔除这两个 power 的介绍，避免头顶条之外
///    又悬浮出一份。
/// ⚠ 两处都只拦显示、不动 IsVisible——PowerModel.HoverTips 会对 !IsVisible 返回空，
/// 头顶条的悬浮介绍会一起消失（2026-10-12 实测）。
/// </summary>
public static class FootworkDodgeBarSpawnPatch
{
    private static bool IsHiddenFromUi(PowerModel power)
    {
        return power is flametailFootworkPower or flametailDodgePower;
    }

    [HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
    private static class NCombatRoomAddCreaturePatch
    {
        private static void Postfix(Creature creature)
        {
            if (!creature.IsPlayer)
            {
                return;
            }

            Callable.From(() => FootworkDodgeBar.TryAttach(creature)).CallDeferred();
        }
    }

    [HarmonyPatch(typeof(NPowerContainer), "Add")]
    private static class NPowerContainerAddPatch
    {
        private static bool Prefix(PowerModel power)
        {
            return !IsHiddenFromUi(power);
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.HoverTips), MethodType.Getter)]
    private static class CreatureHoverTipsPatch
    {
        private static void Postfix(ref IEnumerable<IHoverTip> __result)
        {
            __result = __result.Where(t => t.CanonicalModel is not (flametailFootworkPower or flametailDodgePower));
        }
    }
}
