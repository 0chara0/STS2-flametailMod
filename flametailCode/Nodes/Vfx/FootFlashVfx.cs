using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace flametail.Nodes.Vfx;

/// <summary>
/// 脚底特效:在生物脚底(角色身后容器,spine 之下)播放 flipbook 动画。
/// SpriteFrames 复用 tools/build_hit_vfx.py 生成的 generated/&lt;id&gt;/&lt;id&gt;_frames.tres,
/// 加色材质与命中特效共用。不随机翻转(FOOT 类素材自带构图方向)。
/// 单次模式(loop=false)播完自毁;循环模式(loop=true)由调用方管理生命周期。
/// </summary>
public partial class FootFlashVfx : Node2D
{
    /// <summary>是否循环播放(循环时不自毁,由调用方 QueueFree)。</summary>
    [Export] public bool Loop { get; set; }

    // 每帧跟随宿主:位置锚 hitbox 底边中点、尺寸随宿主全局缩放。
    // ⚠️ 不能用 GetBottomOfHitbox():它把 hitbox 局部 Size 直接加到全局坐标上,而
    // Size 不含相机缩放(Encounter.GetCameraScaling,Boss 战 0.8~0.9)和挤压缩放
    // (AdjustCreatureScaleForAspectRatio:敌人总宽超屏时 _allyContainer/_enemyContainer
    // 同步缩小)——大遭遇战里会往右下漂移。走 Hitbox.GetGlobalTransform() 变换局部
    // 底边点则天然包含以上全部。缩放同理用 Visuals.GlobalScale(相机×挤压×体型),
    // 并以 GlobalScale 写入:CombatVfxContainer 挂在房间根下(不在 SceneContainer 内,
    // 见 combat_room.tscn),GlobalScale 语义对前后两种容器都正确。
    private NCreature? _ownerNode;
    private Vector2 _positionOffset;
    private float _baseScale = 1f;

    private void UpdateTransform()
    {
        if (_ownerNode == null || !GodotObject.IsInstanceValid(_ownerNode))
        {
            return;
        }

        float s = _ownerNode.Visuals.GlobalScale.X;
        Control hitbox = _ownerNode.Hitbox;
        GlobalPosition = hitbox.GetGlobalTransform()
            * new Vector2(hitbox.Size.X * 0.5f, hitbox.Size.Y)
            + _positionOffset * s;
        GlobalScale = Vector2.One * (_baseScale * s);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_ownerNode == null || !GodotObject.IsInstanceValid(_ownerNode))
        {
            return;
        }

        UpdateTransform();
    }

    public override void _Ready()
    {
        base._Ready();
        var sprite = GetNode<AnimatedSprite2D>("Sprite");
        if (Loop)
        {
            sprite.SpriteFrames?.SetAnimationLoop("default", true);
        }
        sprite.Play("default");
        if (!Loop)
        {
            sprite.AnimationFinished += () => this.QueueFreeSafely();
        }
    }

    /// <summary>
    /// 在生物脚底播放脚底特效。
    /// positionOffset:相对脚底(GetBottomOfHitbox)的偏移,像素;由素材 md 的
    /// 「锚点像素位置」换算:offset = -(锚点 - 图中心) × 素材缩放。
    /// inFront:false = 角色身后容器(spine 之下,可被角色遮挡,FOOT/GROUND 类原版规则);
    /// true = 前方容器(spine 之上,不被遮挡,适合光环类)。
    /// </summary>
    public static FootFlashVfx? Create(Creature owner, string effectId, Vector2 positionOffset, float scale = 1.8f, bool loop = false, bool inFront = false)
    {
        if (TestMode.IsOn || NCombatRoom.Instance == null)
        {
            return null;
        }

        NCreature? node = NCombatRoom.Instance.GetCreatureNode(owner);
        if (node == null)
        {
            return null;
        }

        var vfx = new FootFlashVfx
        {
            Loop = loop,
            _ownerNode = node,
            _positionOffset = positionOffset,
            _baseScale = scale
        };
        var sprite = new AnimatedSprite2D
        {
            // 必须显式命名:AddChildSafely 用 forceReadableName=false,无名节点会被
            // Godot 自动命名为 @AnimatedSprite2D@N,_Ready 里按 "Sprite" 找不到。
            Name = "Sprite",
            SpriteFrames = PreloadManager.Cache.GetAsset<SpriteFrames>(
                $"{Entry.ResPath}/images/vfx/hits/generated/{effectId}/{effectId}_frames.tres"),
            Material = PreloadManager.Cache.GetMaterial($"{Entry.ResPath}/scenes/vfx/hits/hit_vfx_add.tres")
        };
        sprite.AnimationFinished += () => vfx.QueueFreeSafely();
        vfx.AddChildSafely(sprite);
        vfx.Scale = Vector2.One * scale;

        // 前方容器(ZIndex -9,spine 之上)或身后容器(spine 之下),按 inFront 二分。
        Control? container = inFront ? owner.GetVfxContainer() : owner.GetBackVfxContainer();
        container?.AddChildSafely(vfx);
        vfx.UpdateTransform();
        return vfx;
    }
}
