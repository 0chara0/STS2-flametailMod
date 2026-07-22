using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace flametail.Nodes.Vfx;

/// <summary>
/// 通用“召唤盟友 → 在身侧攻击 → 离场”视觉特效节点。
/// 不创建真实战斗单位，只负责在 DamageCmd 的攻击流程中播放一段临时动画。
/// 命中时设置 <see cref="TaskCompletionSource"/>，供 <c>AttackCommand.BeforeDamage</c> 等待。
/// </summary>
public partial class SummonedAllyVfx : Node2D
{
    // 时间参数，单位为秒。
    private const float EnterDuration = 0.18f;
    private const float LeaveDuration = 0.25f;

    /// <summary>攻击动画播放到多少比例时触发命中信号（0~1）。默认 0.4。</summary>
    [Export] public float ImpactRatio { get; set; } = 0.4f;

    /// <summary>离场动画是否为循环动画。循环动画不会等待播完，只播放 LeaveDuration 时长后淡出。</summary>
    [Export] public bool LeaveLoop { get; set; } = false;

    /// <summary>入场时不进行位移，而是直接在召唤位置逐渐显现（透明度 0→1）。</summary>
    [Export] public bool FadeIn { get; set; } = false;

    /// <summary>命中后是否立即切入离场动画，不再等待攻击动画剩余部分播完。</summary>
    [Export] public bool CutAttackOnImpact { get; set; } = false;

    /// <summary>攻击动画时长覆盖值（秒）。大于 0 时，用此值代替 Spine 动画实际时长来控制命中/离场时机。</summary>
    [Export] public float AttackDurationOverride { get; set; } = 0f;

    // 召唤物站在玩家右侧地面上，脚底与玩家脚底对齐（类似原版 Osty 的右侧站位，
    // 但 Osty 自身较矮，其 -75 的 Y 偏移是为了让头顶与玩家对齐；
    // 我们的召唤物使用与玩家相同的角色资源，因此 Y 偏移为 0）。
    private const float SummonOffsetX = 150f;

    private Vector2 _sourcePosition;
    private Vector2 _summonPosition;
    private TaskCompletionSource? _impactSignal;
    private Node? _spineVisuals;
    private Tween? _sequenceTween;

    /// <summary>召唤物出现瞬间播放的 Spine 动画（如 "cast" / "enter"）。留空则不播放入场动画。</summary>
    [Export] public string EnterAnimationName { get; set; } = "";

    /// <summary>召唤物在攻击位置播放的攻击动画名。默认为 "attack"。</summary>
    [Export] public string AttackAnimationName { get; set; } = "attack";

    /// <summary>召唤物离场前播放的 Spine 动画（如 "die" / "leave"）。留空则直接淡出。</summary>
    [Export] public string LeaveAnimationName { get; set; } = "";

    public override void _ExitTree()
    {
        _sequenceTween?.Kill();
        base._ExitTree();
    }

    /// <summary>
    /// 创建一个召唤特效节点。
    /// </summary>
    /// <param name="source">召唤者（通常是玩家）</param>
    /// <param name="target">攻击目标；AOE 时可传 null</param>
    /// <param name="scenePath">召唤物场景路径，如 <c>res://flametail/scenes/vfx/summons/lance_support_summon.tscn</c></param>
    /// <param name="impactSignal">命中信号，会在攻击动画命中瞬间被 SetResult</param>
    /// <param name="aoe">是否为 AOE（无单体目标，召唤物默认出现在玩家右侧）</param>
    /// <returns>特效节点；测试模式下返回 null 并直接触发信号</returns>
    public static SummonedAllyVfx? Create(
        Creature source,
        Creature? target,
        string scenePath,
        TaskCompletionSource? impactSignal = null,
        bool aoe = false)
    {
        if (TestMode.IsOn)
        {
            impactSignal?.TrySetResult();
            return null;
        }

        if (NCombatRoom.Instance == null)
        {
            impactSignal?.TrySetResult();
            return null;
        }

        NCreature? sourceNode = NCombatRoom.Instance.GetCreatureNode(source);
        if (sourceNode == null)
        {
            impactSignal?.TrySetResult();
            return null;
        }

        Vector2 targetPosition;
        if (target != null)
        {
            NCreature? targetNode = NCombatRoom.Instance.GetCreatureNode(target);
            if (targetNode == null)
            {
                impactSignal?.TrySetResult();
                return null;
            }
            targetPosition = targetNode.GetBottomOfHitbox();
        }
        else if (aoe)
        {
            var opponents = source.CombatState?.GetOpponentsOf(source)
                .Where(c => c.IsAlive)
                .ToList();

            if (opponents == null || opponents.Count == 0)
            {
                impactSignal?.TrySetResult();
                return null;
            }

            targetPosition = opponents
                .Select(c => NCombatRoom.Instance.GetCreatureNode(c)?.GetBottomOfHitbox() ?? Vector2.Zero)
                .Where(p => p != Vector2.Zero)
                .Aggregate(Vector2.Zero, (a, b) => a + b) / opponents.Count;
        }
        else
        {
            impactSignal?.TrySetResult();
            return null;
        }

        var vfx = PreloadManager.Cache.GetScene(scenePath)
            .Instantiate<SummonedAllyVfx>(PackedScene.GenEditState.Disabled);

        vfx._sourcePosition = sourceNode.VfxSpawnPosition;
        vfx._impactSignal = impactSignal;
        vfx._spineVisuals = vfx.GetChildCount() > 0 ? vfx.GetChild(0) : null;

        // 召唤物固定在玩家右侧的地面上，脚底与玩家脚底对齐。
        // 参考原版 Osty：X = 玩家脚底 + Hitbox 半宽 + 右侧间距。
        vfx._summonPosition = sourceNode.GlobalPosition
            + Vector2.Right * (sourceNode.Hitbox.Size.X * 0.5f + SummonOffsetX);

        // 让召唤物始终面向敌人（默认朝右）。
        Vector2 dir = (targetPosition - vfx._sourcePosition).Normalized();
        if (dir.X != 0f)
        {
            Vector2 scale = vfx.Scale;
            scale.X = Math.Abs(scale.X) * MathF.Sign(dir.X);
            vfx.Scale = scale;
        }

        if (vfx.FadeIn)
        {
            // 直接出现在召唤位置，初始透明。
            vfx.GlobalPosition = vfx._summonPosition;
            vfx.Modulate = new Color(1f, 1f, 1f, 0f);
        }
        else
        {
            vfx.GlobalPosition = vfx._sourcePosition;
        }

        return vfx;
    }

    public override void _Ready()
    {
        base._Ready();
        TaskHelper.RunSafely(PlaySequence());
    }

    private async Task PlaySequence()
    {
        // 1) 入场：位移入场或直接淡入。
        _sequenceTween = CreateTween();
        _sequenceTween.SetTrans(Tween.TransitionType.Quad);
        _sequenceTween.SetEase(Tween.EaseType.Out);
        if (FadeIn)
        {
            _sequenceTween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), EnterDuration);
        }
        else
        {
            _sequenceTween.TweenProperty(this, "global_position", _summonPosition, EnterDuration);
        }
        PlaySpineAnimation(EnterAnimationName, loop: false);
        await ToSignal(_sequenceTween, "finished");

        // 2) 在原地播放攻击动画，并在指定比例处触发命中信号。
        float rawAttackDuration = await PlaySpineAnimationAsync(AttackAnimationName, loop: false);
        float attackDuration = AttackDurationOverride > 0.001f ? AttackDurationOverride : rawAttackDuration;

        float impactDelay = attackDuration * Mathf.Clamp(ImpactRatio, 0f, 1f);
        if (impactDelay > 0.001f)
        {
            await ToSignal(GetTree().CreateTimer(impactDelay), "timeout");
        }
        OnImpacted();

        if (CutAttackOnImpact)
        {
            // 命中后立即停止攻击动画，进入离场。
        }
        else
        {
            float remainingAttack = Mathf.Max(0f, attackDuration - impactDelay);
            if (remainingAttack > 0.001f)
            {
                await ToSignal(GetTree().CreateTimer(remainingAttack), "timeout");
            }
        }

        // 3) 播放离场动画并在原地淡出离场。
        float leaveDuration;
        if (LeaveLoop)
        {
            PlaySpineAnimation(LeaveAnimationName, loop: true);
            leaveDuration = LeaveDuration;
        }
        else
        {
            leaveDuration = await PlaySpineAnimationAsync(LeaveAnimationName, loop: false);
            if (leaveDuration <= 0.001f)
            {
                leaveDuration = LeaveDuration;
            }
        }

        _sequenceTween = CreateTween();
        _sequenceTween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), leaveDuration);
        await ToSignal(_sequenceTween, "finished");

        this.QueueFreeSafely();
    }

    private void OnImpacted()
    {
        _impactSignal?.TrySetResult();
    }

    private void PlaySpineAnimation(string animationName, bool loop = false)
    {
        _ = PlaySpineAnimationAsync(animationName, loop);
    }

    private async Task<float> PlaySpineAnimationAsync(string animationName, bool loop = false)
    {
        if (_spineVisuals == null || string.IsNullOrEmpty(animationName))
            return 0f;

        var tcs = new TaskCompletionSource<float>();
        var sprite = new MegaSprite(_spineVisuals);
        this.RunWhenSpineReady(sprite, state =>
        {
            if (!sprite.HasAnimation(animationName))
            {
                GD.PushWarning($"[SummonedAllyVfx] Animation '{animationName}' not found on '{Name}'.");
                tcs.TrySetResult(0f);
                return;
            }
            state.SetAnimation(animationName, loop);
            float duration = state.GetCurrentAnimationDuration(0) ?? 0f;
            tcs.TrySetResult(duration);
        });

        return await tcs.Task;
    }
}
