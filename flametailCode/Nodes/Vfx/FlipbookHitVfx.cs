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
        // nearl2_skill_03_hits(戍夜威光卡专用,不在随机池):白光实测帧 0-1 点燃核
        // 质心 (239.5,212),与 md 锚 (239,211) 吻合;图 692×436 中心 (346,218)
        // → offset = 中心−冲击点 = (106.5, 6)。
        ["nearl2_skill_03_hits"] = new Vector2(106.5f, 6f),
        // vvana_skill_03_hits(烛火一闪卡专用,不在随机池):HIT_POINT 锚 (374,237),
        // 图 767×393 中心 (383.5,196.5) → offset = (9.5, -40.5)。
        ["vvana_skill_03_hits"] = new Vector2(9.5f, -40.5f),
        // fartth_skill_03_hit_01(光箭支援卡专用,不在随机池):HIT_POINT 锚 (183,266),
        // 图 629×532 中心 (314.5,266) → offset = (131.5, 0)。
        ["fartth_skill_03_hit_01"] = new Vector2(131.5f, 0f),
        // ashlok_attack_01_hit_02(铳炮支援卡专用,不在随机池):md 锚 (164,163) 与
        // 前 9 帧白光质心 (164~184,160~164) 吻合;图 463×423 中心 (231.5,211.5)
        // → offset = (67.5, 48.5)。
        ["ashlok_attack_01_hit_02"] = new Vector2(67.5f, 48.5f),
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
        return Create(target, RandomEffectId());
    }

    /// <summary>指定特效 id 的重载(卡牌专属命中特效,不进随机池)。</summary>
    public static FlipbookHitVfx? Create(Creature target, string effectId)
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
