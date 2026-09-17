using System;
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
/// 命中特效:在目标身上播放一次 flipbook 动画(带 additive 发光材质)后自动销毁。
/// 场景与 SpriteFrames 由 <c>tools/build_hit_vfx.py</c> 生成,位于
/// <c>flametail/scenes/vfx/hits/&lt;特效id&gt;.tscn</c>。
/// </summary>
public partial class FlipbookHitVfx : Node2D
{
    // 与 flametail/images/vfx/hits/hit/ 下素材一一对应,重跑生成脚本后若增删特效需同步此表。
    private static readonly string[] EffectIds =
    [
        "attack_01_hit_01",
        "attack_01_hit_02a",
        "attack_01_hit_02b",
        "skill_03_attack_01_hit_02a",
        "skill_03_attack_01_hit_02b"
    ];

    private static readonly Random Rng = new();

    // 素材冲击点相对图中心的偏移(图内像素;0 = 图中心即命中点)。命中闪光不在图正中时,
    // 居中摆放会让闪光落在受击点旁边——按 (图中心 − 冲击点) 反向补偿。冲击点取素材
    // md「锚点像素位置」;attack_01_hit_02b 用前几帧白光点燃位实测(帧1 (264,171)/669×342)。
    // 世界偏移 = 此值 × ScaleMultiplier(× 抖动;随机翻转时 X 自动镜像)。
    private static readonly System.Collections.Generic.Dictionary<string, Vector2> ImpactAnchorOffsets = new()
    {
        ["attack_01_hit_01"] = new Vector2(-12.5f, -44f),
        ["attack_01_hit_02a"] = new Vector2(-7.5f, 31f),
        ["attack_01_hit_02b"] = new Vector2(70.5f, 0f),
        ["skill_03_attack_01_hit_02a"] = new Vector2(0f, -10.5f),
        ["skill_03_attack_01_hit_02b"] = new Vector2(49.5f, -67f),
    };

    public static string RandomEffectId() => EffectIds[Rng.Next(EffectIds.Length)];

    /// <summary>命中点偏移,相对目标 hitbox 中心(像素;X 正=右,Y 正=下;随机翻转时 X 自动镜像)。</summary>
    [Export] public Vector2 PositionOffset { get; set; } = Vector2.Zero;

    /// <summary>整体缩放倍率(1.0 = 素材原始像素尺寸)。</summary>
    [Export] public float ScaleMultiplier { get; set; } = 1f;

    /// <summary>随机缩放抖动幅度(± 比例,0.1 = ±10%)。</summary>
    [Export] public float ScaleJitter { get; set; } = 0.1f;

    /// <summary>随机水平翻转概率(0~1)。</summary>
    [Export] public float FlipChance { get; set; } = 0.5f;

    public override void _Ready()
    {
        base._Ready();
        var sprite = GetNode<AnimatedSprite2D>("Sprite");
        sprite.Play("default");
        sprite.AnimationFinished += () => this.QueueFreeSafely();
    }

    /// <summary>
    /// 在目标身上随机播放一个命中特效,挂到目标的 Vfx 容器。
    /// 定位在目标 hitbox 中心,随机水平翻转与 ±10% 缩放抖动以避免重复感。
    /// </summary>
    public static FlipbookHitVfx? Create(Creature target)
    {
        if (TestMode.IsOn || NCombatRoom.Instance == null)
        {
            return null;
        }

        NCreature? node = NCombatRoom.Instance.GetCreatureNode(target);
        if (node == null)
        {
            return null;
        }

        string effectId = RandomEffectId();
        var vfx = PreloadManager.Cache
            .GetScene($"{Entry.ResPath}/scenes/vfx/hits/{effectId}.tscn")
            .Instantiate<FlipbookHitVfx>(PackedScene.GenEditState.Disabled);

        // 命中点 = 目标的 VfxSpawnPosition(游戏美术为每只怪物调好的 %CenterPos 锚点,
        // 与原版 NStabVfx 同款,不同体型怪身上位置才稳定)+ 冲击点修正 + 可调偏移
        // + 随机翻转/抖动。冲击点修正随缩放/抖动同步缩放,翻转时 X 镜像。
        ImpactAnchorOffsets.TryGetValue(effectId, out Vector2 anchorOffset);
        int flip = Rng.NextSingle() < vfx.FlipChance ? -1 : 1;
        float jitter = 1f + (Rng.NextSingle() * 2f - 1f) * Mathf.Max(0f, vfx.ScaleJitter);
        vfx.GlobalPosition = node.VfxSpawnPosition
            + new Vector2(
                (vfx.PositionOffset.X + anchorOffset.X * vfx.ScaleMultiplier) * flip * jitter,
                vfx.PositionOffset.Y + anchorOffset.Y * vfx.ScaleMultiplier * jitter);
        vfx.Scale = new Vector2(vfx.ScaleMultiplier * flip, vfx.ScaleMultiplier) * jitter;

        target.GetVfxContainer()?.AddChildSafely(vfx);
        return vfx;
    }
}
