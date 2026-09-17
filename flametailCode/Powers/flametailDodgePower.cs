using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using flametail.Nodes.Vfx;
using flametail.Patches;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 闪避：优先于格挡抵消 1 次攻击伤害；在玩家回合开始时清除。
///
/// 实现上通过 Harmony 补丁拦截 Creature.DamageBlockInternal，使闪避在真实格挡
/// 被扣除之前就把伤害吸收掉，同时不影响敌方意图预览的攻击数字。
/// </summary>
[RegisterPower]
public sealed class flametailDodgePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailDodgePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailDodgePower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    // 脚底循环特效(flamtl_flash_01):持有闪避期间持续播放,层数归零移除 power 时销毁。
    private FootFlashVfx? _footVfx;

    private void EnsureFootVfx()
    {
        if (_footVfx != null && GodotObject.IsInstanceValid(_footVfx))
        {
            return;
        }

        // skill01_buff02 素材 335×324,锚点像素 (168,305) → 相对脚底偏移 = -(168-167.5, 305-162)×0.89 ≈ (-0.4,-127)。
        // 缩放 0.89 = 世界真实尺寸:骨架 setup 高 452.1 spine 单位 × 0.63(场景 SpineSprite scale)
        // = 284.8px 绘制高 = AK 1.25 世界单位 → 1 AK 单位 = 227.8px;227.8÷256(烘焙 px/单位)= 0.89。
        // (旧值 1.215 误用 Bounds 悬停框 389px 当身高,特效放大了 1.365 倍。)
        // inFront:true → 光环走 GetVfxContainer()(ZIndex -9,spine 之上),不被角色遮挡;
        // 播放帧率 45fps(原烘焙 30fps 偏慢)在 build_hit_vfx.py 的 FPS_OVERRIDE 调整。
        _footVfx = FootFlashVfx.Create(Owner, "skill01_buff02", new Vector2(-0.4f, -127.3f), 0.89f, loop: true, inFront: true);
    }

    private void RemoveFootVfx()
    {
        if (_footVfx != null && GodotObject.IsInstanceValid(_footVfx))
        {
            _footVfx.QueueFreeSafely();
        }

        _footVfx = null;
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Amount > 0)
        {
            EnsureFootVfx();
        }

        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        RemoveFootVfx();
        return Task.CompletedTask;
    }

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner)
        {
            return Task.CompletedTask;
        }

        if (!props.IsPoweredAttack())
        {
            return Task.CompletedTask;
        }

        if (dealer == null || !dealer.IsEnemy)
        {
            return Task.CompletedTask;
        }

        if (Amount <= 0 || (int)amount <= 0)
        {
            return Task.CompletedTask;
        }

        return ConsumeDodgeAsync(choiceContext);
    }

    private async Task ConsumeDodgeAsync(PlayerChoiceContext choiceContext)
    {
        // 消耗 1 层闪避，并通知补丁跳过本次格挡消耗。
        await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null);
        if (Owner != null)
        {
            DodgeBlockPatch.RegisterDodge(Owner);
        }
    }

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (Amount > 0)
        {
            await PowerCmd.ModifyAmount(choiceContext, this, -Amount, Owner, null);
        }
    }
}
