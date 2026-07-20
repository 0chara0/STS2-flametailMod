using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;

namespace flametail.Characters;

/// <summary>
/// 商店场景中的 flametail 角色动画控制器。
/// 由于原版 NMerchantCharacter 脚本在 mod PCK 中会被报告为缺少依赖项，
/// 使用此 mod 内脚本来驱动 Spine 动画。
/// </summary>
public partial class FlametailMerchant : Node2D
{
    public override void _Ready()
    {
        var visuals = GetChild(0);
        this.RunWhenSpineReady(new MegaSprite(visuals), state => state.SetAnimation("relaxed_loop", loop: true));
    }
}
