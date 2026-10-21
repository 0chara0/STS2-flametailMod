using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.TestSupport;

namespace flametail.Helpers;

/// <summary>
/// 为“打出选中的牌”类效果（故技重施/借过一下）解析 AutoPlay 的目标：
/// 目标随选中的牌走——选中要敌人/队友/玩家的单一目标型牌时，弹原生选目标 UI；
/// 取消选择或其余目标类型（Self/全体/随机等）传 null，由引擎在打出时自动兜底。
/// 仅卡牌主人的本地客户端弹 UI；测试模式与远端不弹（随机兜底）。
/// </summary>
public static class AutoPlayTargetResolver
{
    /// <summary>
    /// 依据选中牌的 TargetType 解析打出目标。返回 null 表示交给引擎自行取目标。
    /// </summary>
    public static async Task<Creature?> ResolveTargetAsync(CardModel chosen, CardModel source)
    {
        TargetType targetType = chosen.TargetType;
        if (targetType is not (TargetType.AnyEnemy or TargetType.AnyAlly or TargetType.AnyPlayer))
        {
            // 非玩家选择的单一目标：AutoPlay 只认 AnyEnemy/AnyPlayer/AnyAlly 的传入目标，其余忽略，直接传 null。
            return null;
        }

        if (TestMode.IsOn || !LocalContext.IsMe(source.Owner))
        {
            return null;
        }

        // 起点取卡牌主人的生物节点位置（无卡牌节点可用时的稳定锚点）。
        Vector2 startPosition = NCombatRoom.Instance?.GetCreatureNode(source.Owner.Creature)?.GlobalPosition ?? Vector2.Zero;
        bool usingController = NControllerManager.Instance.IsUsingDirectionalNavigation;

        NTargetManager targetManager = NTargetManager.Instance;
        targetManager.StartTargeting(
            targetType,
            startPosition,
            usingController ? TargetMode.Controller : TargetMode.ClickMouseToTarget,
            ShouldCancelTargeting,
            null);

        try
        {
            return NodeToCreature(await targetManager.SelectionFinished());
        }
        finally
        {
            if (usingController)
            {
                NCombatRoom.Instance?.EnableControllerNavigation();
            }
        }
    }

    /// <summary>
    /// 有界面被打开（覆盖层/高潮卡）时中断选目标（与药水定位的取消条件一致）。
    /// </summary>
    private static bool ShouldCancelTargeting()
    {
        if (NOverlayStack.Instance.ScreenCount > 0)
        {
            return true;
        }

        return NCapstoneContainer.Instance.InUse;
    }

    private static Creature? NodeToCreature(Node? node)
    {
        return node switch
        {
            NCreature nCreature => nCreature.Entity,
            NMultiplayerPlayerState nState => nState.Player.Creature,
            _ => null,
        };
    }
}
