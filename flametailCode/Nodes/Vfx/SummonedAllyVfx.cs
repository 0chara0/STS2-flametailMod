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
    private Vector2 _targetPosition;
    private TaskCompletionSource? _impactSignal;
    private Node? _spineVisuals;
    private Node2D? _projectileBoneNode;
    private Tween? _sequenceTween;
    // 世界对齐的骨骼跟随特效:SpineBoneNode 每帧重写自身旋转,必须逐帧反向补偿。
    private readonly System.Collections.Generic.List<(AnimatedSprite2D Sprite, float WorldRotation)>
        _worldAlignedFx = new();

    /// <summary>召唤物出现瞬间播放的 Spine 动画（如 "cast" / "enter"）。留空则不播放入场动画。</summary>
    [Export] public string EnterAnimationName { get; set; } = "";

    /// <summary>召唤物在攻击位置播放的攻击动画名。默认为 "attack"。</summary>
    [Export] public string AttackAnimationName { get; set; } = "attack";

    /// <summary>召唤物离场前播放的 Spine 动画（如 "die" / "leave"）。留空则直接淡出。</summary>
    [Export] public string LeaveAnimationName { get; set; } = "";

    /// <summary>
    /// 入场时同步播放的 flipbook 特效 id(build_hit_vfx.py 生成的 generated/&lt;id&gt;)。
    /// 留空则不播。特效画在召唤物 spine 之下(GROUND 类),跟随入场位移。
    /// </summary>
    [Export] public string EnterEffectId { get; set; } = "";

    /// <summary>
    /// 入场特效的精灵绘制偏移,屏幕像素(已含缩放),相对召唤物原点(骨架原点)。
    /// 换算:offset = 脚底基线像素 − (素材锚点 − 图中心) × EnterEffectScale。
    /// </summary>
    [Export] public Vector2 EnterEffectOffset { get; set; } = Vector2.Zero;

    /// <summary>入场特效缩放。素材按 256px/AK单位 烘焙,此值 = 召唤物屏上 px/AK单位 ÷ 256。</summary>
    [Export] public float EnterEffectScale { get; set; } = 1f;

    /// <summary>入场特效不透明度(1 = 原素材强度;通过 modulate alpha 调淡,不动素材)。</summary>
    [Export] public float EnterEffectOpacity { get; set; } = 1f;

    /// <summary>入场特效 ZIndex(默认 -1 = spine 之下)。z_index 越大越靠前,相同 z 按生成顺序。</summary>
    [Export] public int EnterEffectZIndex { get; set; } = -1;

    /// <summary>
    /// 入场特效触发延迟(秒,自召唤序列 t=0 计;默认 0 = 入场开始即播)。
    /// 设为 EnterDuration(0.18)即攻击动画开始时才播。
    /// </summary>
    [Export] public float EnterEffectDelay { get; set; } = 0f;

    /// <summary>
    /// 攻击动画开始时同步播放的 flipbook 特效 id(generated/&lt;id&gt;)。留空则不播。
    /// </summary>
    [Export] public string AttackEffectId { get; set; } = "";

    /// <summary>
    /// 攻击特效跟随的骨骼名(如 "F_Weapon",原作 SPECIAL_POINT_0 挂点跟随的骨骼)。
    /// 留空则固定在召唤物原点不跟随。
    /// </summary>
    [Export] public string AttackEffectBone { get; set; } = "";

    /// <summary>
    /// 攻击特效精灵绘制偏移,骨骼节点局部像素(会随 SpineSprite 缩放一起生效)。
    /// 换算:offset = (图中心 − 素材锚点) × 目标世界缩放 ÷ SpineSprite scale。
    /// </summary>
    [Export] public Vector2 AttackEffectOffset { get; set; } = Vector2.Zero;

    /// <summary>攻击特效缩放,骨骼节点局部值;总缩放 = SpineSprite scale × 此值。</summary>
    [Export] public float AttackEffectScale { get; set; } = 1f;

    /// <summary>
    /// 主层攻击特效是否世界对齐(旋转不随骨骼)。本作 spine 扩展的 SpineBoneNode
    /// 是全变换跟随(无 follow_rotation 属性,Set 无效),官方 fixed 朝向的挂点特效
    /// (如 fartth 的枪口闪光)骨骼转到哪特效就歪到哪——开启后逐帧把特效的
    /// GlobalRotation 归零,位置仍跟随骨骼。剑痕/拖尾类应保持 false(随武器旋转)。
    /// </summary>
    [Export] public bool AttackEffectWorldAligned { get; set; } = false;

    /// <summary>攻击特效不透明度(1 = 原素材强度;通过 modulate alpha 调淡,不动素材)。</summary>
    [Export] public float AttackEffectOpacity { get; set; } = 1f;

    /// <summary>
    /// 攻击动画开始到攻击特效开播的延迟(秒)。官方挂点特效在挥砍瞬间触发而非
    /// 动画起点——不延迟时,带长驻尾的拖尾素材会在举剑/蓄力阶段就糊在屏上
    /// (如 vvana 的 s_start_01:0.36s 活跃 + 1.1s 长尾)。由官方 on_char 合成
    /// 演示逐帧测量挥砍时刻标定;命中信号时序不受此值影响。
    /// </summary>
    [Export] public float AttackEffectDelay { get; set; } = 0f;

    /// <summary>主层攻击特效 ZIndex(默认 0,与 spine 同层,靠树序画在其上)。</summary>
    [Export] public int AttackEffectZIndex { get; set; } = 0;

    /// <summary>
    /// 攻击特效后层:与主层同时播放,画在召唤物 spine 之下(ZIndex -1)。
    /// 用于官方按渲染顺序拆成 spine 后/前两层的合成素材(如 blemsh 的 buff 光环,
    /// back 层 z_index 低于 spine → spine → front 层)。留空则只有主层。
    /// </summary>
    [Export] public string AttackBackEffectId { get; set; } = "";

    /// <summary>攻击特效后层绘制偏移,语义同 <see cref="AttackEffectOffset"/>。</summary>
    [Export] public Vector2 AttackBackEffectOffset { get; set; } = Vector2.Zero;

    /// <summary>攻击特效后层缩放,语义同 <see cref="AttackEffectScale"/>。</summary>
    [Export] public float AttackBackEffectScale { get; set; } = 1f;

    /// <summary>攻击特效后层不透明度,语义同 <see cref="AttackEffectOpacity"/>。</summary>
    [Export] public float AttackBackEffectOpacity { get; set; } = 1f;

    /// <summary>
    /// 攻击特效后层自己的开播延迟(秒),仅作用于后层。设为 0 时沿用
    /// <see cref="AttackEffectDelay"/>(旧行为)。用于后层与主层触发时刻不同的素材,
    /// 如 vvana 的 s_start_02 弧光:动画首帧自带 0.43s 空白(官方 _delayTime=0.4s
    /// 烘进 APNG),需再延迟使扫过段压在挥砍上,而主层 s_start_01 须在动画起点起播。
    /// </summary>
    [Export] public float AttackBackEffectDelay { get; set; } = 0f;

    /// <summary>
    /// 攻击特效后层跟随的骨骼名(如 wildmn 的枪骨 "F_Hand_L_2"、ashlok 的炮口 "F_Muzzle")。
    /// 留空则固定在召唤物原点(offset/scale 为屏幕像素语义);非空时挂到骨骼下,
    /// offset 为骨骼局部像素、总缩放 = SpineSprite scale × 此值(与主层骨骼跟随同语义)。
    /// </summary>
    [Export] public string AttackBackEffectBone { get; set; } = "";

    /// <summary>
    /// 攻击特效后层旋转(度)。素材按官方竖直方向烘焙而实际应水平指向敌人时用,
    /// 如 wildmn 的突刺光束 +90° 转为朝右。Offset 在旋转前的纹理空间里生效,
    /// 锚点对准时 o = 图中心 − 素材锚点,与旋转无关。
    /// </summary>
    [Export] public float AttackBackEffectRotation { get; set; } = 0f;

    /// <summary>后层 ZIndex(默认 -1 = spine 之下)。</summary>
    [Export] public int AttackBackEffectZIndex { get; set; } = -1;

    /// <summary>
    /// 攻击特效第二后层:与 <see cref="AttackBackEffectId"/> 同为 spine 之下
    /// (ZIndex -1),树序在其之后(画在它之上、spine 之下)。用于官方合成里
    /// 同属 spine 后层的多张素材(如 blemsh 的地面指示环 + 起手光柱)。
    /// 留空则不播。
    /// </summary>
    [Export] public string AttackBackEffect2Id { get; set; } = "";

    /// <summary>攻击特效第二后层绘制偏移,语义同 <see cref="AttackEffectOffset"/>。</summary>
    [Export] public Vector2 AttackBackEffect2Offset { get; set; } = Vector2.Zero;

    /// <summary>攻击特效第二后层缩放,语义同 <see cref="AttackEffectScale"/>。</summary>
    [Export] public float AttackBackEffect2Scale { get; set; } = 1f;

    /// <summary>攻击特效第二后层不透明度,语义同 <see cref="AttackEffectOpacity"/>。</summary>
    [Export] public float AttackBackEffect2Opacity { get; set; } = 1f;

    /// <summary>第二后层 ZIndex(默认 -1 = spine 之下,树序在第一后层之上)。</summary>
    [Export] public int AttackBackEffect2ZIndex { get; set; } = -1;

    /// <summary>
    /// 攻击特效前层:与主层同时播放,画在召唤物 spine 之上(树序在主层之后)。
    /// 用于官方合成的 spine 前层素材(如 vvana 的 buff 光环前层,排序值 &gt; 0)。
    /// 留空则不播。
    /// </summary>
    [Export] public string AttackFrontEffectId { get; set; } = "";

    /// <summary>攻击特效前层绘制偏移,语义同 <see cref="AttackEffectOffset"/>。</summary>
    [Export] public Vector2 AttackFrontEffectOffset { get; set; } = Vector2.Zero;

    /// <summary>攻击特效前层缩放,语义同 <see cref="AttackEffectScale"/>。</summary>
    [Export] public float AttackFrontEffectScale { get; set; } = 1f;

    /// <summary>攻击特效前层不透明度,语义同 <see cref="AttackEffectOpacity"/>。</summary>
    [Export] public float AttackFrontEffectOpacity { get; set; } = 1f;

    /// <summary>
    /// 攻击特效前层旋转(度),语义同 <see cref="AttackBackEffectRotation"/>
    /// (如 wildmn 起手枪气前层的突刺方向)。
    /// </summary>
    [Export] public float AttackFrontEffectRotation { get; set; } = 0f;

    /// <summary>前层 ZIndex(默认 0,树序在主层之上)。</summary>
    [Export] public int AttackFrontEffectZIndex { get; set; } = 0;

    /// <summary>
    /// 射弹特效(如弓箭飞行拖尾):攻击动画开始后 <see cref="ProjectileDelay"/> 秒,
    /// 从 <see cref="ProjectileBone"/> 骨骼位置起飞,沿直线飞行
    /// <see cref="ProjectileDuration"/> 秒到达目标命中点,到达瞬间触发命中信号
    /// (命中时序由射弹接管,ImpactRatio 失效)。留空则不派射弹。
    /// </summary>
    [Export] public string ProjectileEffectId { get; set; } = "";

    /// <summary>射弹起飞跟随的骨骼名(如弓/枪口的 "F_Muzzle")。留空则从召唤物原点起飞。</summary>
    [Export] public string ProjectileBone { get; set; } = "";

    /// <summary>射弹起飞点追加偏移,屏幕像素;X 按召唤物朝向镜像。</summary>
    [Export] public Vector2 ProjectileOffset { get; set; } = Vector2.Zero;

    /// <summary>
    /// 射弹精灵绘制偏移,把素材锚点(弹头)对到节点原点的像素补偿:
    /// offset = 图中心 − 素材锚点(图内像素)。旋转以节点原点为轴,
    /// 弹头才能恰好落在目标命中点。
    /// </summary>
    [Export] public Vector2 ProjectileDrawOffset { get; set; } = Vector2.Zero;

    /// <summary>攻击动画开始到射弹起飞的延迟(秒)。</summary>
    [Export] public float ProjectileDelay { get; set; } = 0f;

    /// <summary>射弹飞行时长(秒);命中信号在到达瞬间触发。</summary>
    [Export] public float ProjectileDuration { get; set; } = 0.35f;

    /// <summary>射弹整体缩放(1.0 = 素材原始像素尺寸)。</summary>
    [Export] public float ProjectileScale { get; set; } = 1f;

    // 与 tools/build_hit_vfx.py 的 PREMULTIPLIED_EFFECTS 保持一致:含 AlphaBlend 成分的
    // 预乘烘焙素材必须用预乘混合,加色会丢暗色半透明内容;纯亮辉光素材用加色。
    private static readonly System.Collections.Generic.HashSet<string> PremultipliedEffects = new()
    {
        "nearl2_birth_01_start",
        "blemsh_skill_02_buffs_front",
        "blemsh_skill_02_buffs_back",
        "blemsh_skill_02_start_front",
        "blemsh_skill_02_start_back",
        "blemsh_skill_02_range",
        "ashlok_skill_02_start",
        "wildmn_skill_02_start_back",
        "wildmn_skill_02_start_front",
        "vvana_skill_03_buffs_front_nocandle",
        "vvana_skill_03_s_start_01",
        "vvana_skill_03_s_start_02",
        "fartth_skill_03_start",
        "fartth_skill_03_trail_02",
    };

    public override void _ExitTree()
    {
        _sequenceTween?.Kill();
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        // 世界对齐特效逐帧补偿:骨骼跟随节点每帧重写旋转,只在生成时设置一次会被冲掉。
        for (int i = _worldAlignedFx.Count - 1; i >= 0; i--)
        {
            var (sprite, worldRotation) = _worldAlignedFx[i];
            if (!GodotObject.IsInstanceValid(sprite))
            {
                _worldAlignedFx.RemoveAt(i);
                continue;
            }
            sprite.GlobalRotation = worldRotation;
        }
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
        vfx._targetPosition = targetPosition;
        // 射弹落点优先取目标命中点(美术调好的受击锚点,与命中特效同锚),退化为脚底。
        if (target != null)
        {
            vfx._targetPosition = NCombatRoom.Instance.GetCreatureNode(target)?.VfxSpawnPosition
                ?? targetPosition;
        }
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
        // 射弹起飞骨骼跟随节点提前建好,起飞时(至少一个 ProjectileDelay 之后)
        // 骨骼已完成一帧更新,GlobalPosition 才是弹头离弦的真实位置。
        if (!string.IsNullOrEmpty(ProjectileBone) && _spineVisuals != null && !TestMode.IsOn)
        {
            if (ClassDB.Instantiate("SpineBoneNode").As<Node2D>() is { } boneNode)
            {
                boneNode.Name = "ProjectileBone";
                boneNode.Set("bone_name", ProjectileBone);
                boneNode.Set("follow_rotation", false);
                boneNode.Set("follow_scale", false);
                // 扩展方法不能隐式经 this 调用,必须写显式接收者。
                _spineVisuals.AddChildSafely(boneNode);
                _projectileBoneNode = boneNode;
            }
        }
        TaskHelper.RunSafely(PlaySequence());
    }

    private async Task PlaySequence()
    {
        // 1) 入场：位移入场或直接淡入(位移时长特意保持 0.18s 的快速入场)。
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
        // 延迟触发(EnterEffectDelay>0 时走独立异步分支,如 range 环延迟到攻击起点)。
        SpawnLayerWithDelay(EnterEffectDelay, PlayEnterEffect);
        await ToSignal(_sequenceTween, "finished");

        // 2) 在原地播放攻击动画，并在指定比例处触发命中信号。
        // 攻击特效延迟不影响命中信号时序:延迟走独立异步分支,伤害结算仍按
        // attackDuration × ImpactRatio(或射弹时长)推进。
        PlayAttackEffect();
        float rawAttackDuration = await PlaySpineAnimationAsync(AttackAnimationName, loop: false);
        float attackDuration = AttackDurationOverride > 0.001f ? AttackDurationOverride : rawAttackDuration;

        float impactDelay;
        if (!string.IsNullOrEmpty(ProjectileEffectId))
        {
            // 射弹模式:命中时机由射弹接管 = 起飞延迟 + 飞行时长,弹头到达瞬间伤害才落地。
            impactDelay = ProjectileDelay + ProjectileDuration;
            _ = SpawnProjectile();
        }
        else
        {
            impactDelay = attackDuration * Mathf.Clamp(ImpactRatio, 0f, 1f);
        }
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

    /// <summary>
    /// 入场时在召唤物脚下播放 flipbook 特效(如 nearl2 的 birth 降临地爆)。
    /// ZIndex -1 画在 spine 之下(GROUND 类前后关系);作为召唤物子节点跟随入场位移,
    /// 朝向翻转由父节点 scale.X 镜像继承(素材构图近对称,可接受)。
    /// 材质必须用预乘混合:烘焙素材 RGB 为预乘色且含 AlphaBlend 成分(烟尘/碎石),
    /// 复用加色材质会丢暗色半透明内容。
    /// </summary>
    private void PlayEnterEffect()
    {
        if (string.IsNullOrEmpty(EnterEffectId) || TestMode.IsOn)
        {
            return;
        }

        var sprite = new AnimatedSprite2D
        {
            // 必须显式命名:AddChildSafely 不强制可读名,无名节点会被 Godot 自动命名。
            Name = "EnterEffect",
            SpriteFrames = PreloadManager.Cache.GetAsset<SpriteFrames>(
                $"{Entry.ResPath}/images/vfx/hits/generated/{EnterEffectId}/{EnterEffectId}_frames.tres"),
            Material = PreloadManager.Cache.GetMaterial($"{Entry.ResPath}/scenes/vfx/hits/hit_vfx_premul.tres"),
            Offset = EnterEffectOffset,
            Scale = Vector2.One * EnterEffectScale,
            Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(EnterEffectOpacity, 0f, 1f)),
            ZIndex = EnterEffectZIndex,
        };
        sprite.AnimationFinished += () => sprite.QueueFreeSafely();
        // 扩展方法不能隐式经 this 调用,必须写显式接收者。
        this.AddChildSafely(sprite);
        sprite.Play("default");
    }

    /// <summary>延迟 delay 秒后执行 spawn(独立异步分支,延迟后校验召唤物仍在场)。</summary>
    private async Task DelayedSpawn(Action spawn, float delay)
    {
        await ToSignal(GetTree().CreateTimer(delay), "timeout");
        // 延迟期间召唤物可能已离场(命中即切离场等),释放后不可再访问场景树。
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }
        spawn();
    }

    /// <summary>按延迟生成一层特效:delay ≤ 0 立即生成,否则走独立异步分支。</summary>
    private void SpawnLayerWithDelay(float delay, Action spawn)
    {
        if (delay > 0.001f)
        {
            _ = DelayedSpawn(spawn, delay);
        }
        else
        {
            spawn();
        }
    }

    /// <summary>
    /// 攻击动画开始时播放 flipbook 特效:后层(spine 之下)+ 主层(默认在上)。
    /// 各层按自己的延迟起播:后层优先用 <see cref="AttackBackEffectDelay"/>,
    /// 未设(0)则沿用 <see cref="AttackEffectDelay"/>;主/前/第二后层用
    /// <see cref="AttackEffectDelay"/>。主层可选骨骼跟随(如 nearl2 的 Skill_3
    /// 蓄力光刃跟 F_Weapon);后层骨骼跟随见 <see cref="AttackBackEffectBone"/>。
    /// 骨骼跟随经 ClassDB 实例化 SpineBoneNode(spine addon 为 GDExtension,无 C# 绑定,
    /// 避开编译期依赖),原作特效朝向 fixed,故关闭 follow_rotation / follow_scale 只跟位置。
    /// 朝向翻转由召唤物根节点 scale.X 镜像继承,与骨骼镜像一致。
    /// </summary>
    private void PlayAttackEffect()
    {
        if (TestMode.IsOn || _spineVisuals == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(AttackBackEffectId))
        {
            float backDelay = AttackBackEffectDelay > 0.001f ? AttackBackEffectDelay : AttackEffectDelay;
            SpawnLayerWithDelay(backDelay, () => SpawnAttackLayer("AttackEffectBack", AttackBackEffectId,
                AttackBackEffectOffset, AttackBackEffectScale, AttackBackEffectOpacity, zIndex: AttackBackEffectZIndex,
                bone: AttackBackEffectBone, rotationDeg: AttackBackEffectRotation));
        }

        if (!string.IsNullOrEmpty(AttackBackEffect2Id))
        {
            // 第二后层在第一后层之后生成:同 ZIndex 下树序靠后,画在其上、spine 之下。
            SpawnLayerWithDelay(AttackEffectDelay, () => SpawnAttackLayer("AttackEffectBack2",
                AttackBackEffect2Id, AttackBackEffect2Offset, AttackBackEffect2Scale,
                AttackBackEffect2Opacity, zIndex: AttackBackEffect2ZIndex, bone: ""));
        }

        if (!string.IsNullOrEmpty(AttackEffectId))
        {
            SpawnLayerWithDelay(AttackEffectDelay, () => SpawnAttackLayer("AttackEffect", AttackEffectId,
                AttackEffectOffset, AttackEffectScale, AttackEffectOpacity, zIndex: AttackEffectZIndex,
                bone: AttackEffectBone,
                worldAligned: AttackEffectWorldAligned));
        }

        if (!string.IsNullOrEmpty(AttackFrontEffectId))
        {
            // 前层最后生成:同 ZIndex 下树序在主层之后,画在 spine 与主层之上。
            SpawnLayerWithDelay(AttackEffectDelay, () => SpawnAttackLayer("AttackEffectFront",
                AttackFrontEffectId, AttackFrontEffectOffset, AttackFrontEffectScale,
                AttackFrontEffectOpacity, zIndex: AttackFrontEffectZIndex, bone: "", rotationDeg: AttackFrontEffectRotation));
        }
    }

    /// <summary>
    /// 生成一层攻击特效 sprite 并自动释放;bone 非空时经 SpineBoneNode 跟随骨骼。
    /// worldAligned 仅在骨骼跟随时生效:逐帧强制特效 GlobalRotation(本作 SpineBoneNode
    /// 为全变换跟随,旋转无法单独关掉)。
    /// </summary>
    private void SpawnAttackLayer(string nodeName, string effectId, Vector2 offset, float scale,
        float opacity, int zIndex, string bone, bool worldAligned = false, float rotationDeg = 0f)
    {
        Node2D attachTo = this;
        Node2D? boneNode = null;
        if (!string.IsNullOrEmpty(bone))
        {
            boneNode = ClassDB.Instantiate("SpineBoneNode").As<Node2D>();
            if (boneNode != null)
            {
                boneNode.Name = nodeName + "Bone";
                boneNode.Set("bone_name", bone);
                boneNode.Set("follow_rotation", false);
                boneNode.Set("follow_scale", false);
                // 扩展方法不能隐式经 this 调用,必须写显式接收者。
                _spineVisuals.AddChildSafely(boneNode);
                attachTo = boneNode;
            }
        }

        var sprite = new AnimatedSprite2D
        {
            // 必须显式命名:AddChildSafely 不强制可读名,无名节点会被 Godot 自动命名。
            Name = nodeName,
            SpriteFrames = PreloadManager.Cache.GetAsset<SpriteFrames>(
                $"{Entry.ResPath}/images/vfx/hits/generated/{effectId}/{effectId}_frames.tres"),
            Material = PreloadManager.Cache.GetMaterial($"{Entry.ResPath}/scenes/vfx/hits/"
                + (PremultipliedEffects.Contains(effectId) ? "hit_vfx_premul.tres" : "hit_vfx_add.tres")),
            Offset = offset,
            Scale = Vector2.One * scale,
            Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(opacity, 0f, 1f)),
            ZIndex = zIndex,
            Rotation = Mathf.DegToRad(rotationDeg),
        };
        sprite.AnimationFinished += () =>
        {
            sprite.QueueFreeSafely();
            boneNode?.QueueFreeSafely();
        };
        attachTo.AddChildSafely(sprite);
        if (worldAligned && boneNode != null)
        {
            _worldAlignedFx.Add((sprite, 0f));
        }
        sprite.Play("default");
    }

    /// <summary>
    /// 射弹:延迟 <see cref="ProjectileDelay"/> 秒后从挂点骨骼(或召唤物原点)起飞,
    /// 沿直线飞向目标命中点,旋转对齐飞行方向,到达后自毁。独立挂到战斗房间节点,
    /// 不随召唤物离场消失。素材锚点即弹头,经 <see cref="ProjectileDrawOffset"/>
    /// 把锚点对到节点原点——旋转以锚点为轴,弹头恰好落在目标命中点。
    /// </summary>
    private async Task SpawnProjectile()
    {
        if (TestMode.IsOn || NCombatRoom.Instance == null)
        {
            return;
        }

        if (ProjectileDelay > 0.001f)
        {
            await ToSignal(GetTree().CreateTimer(ProjectileDelay), "timeout");
        }

        Vector2 start = _projectileBoneNode != null
            ? _projectileBoneNode.GlobalPosition
            : _summonPosition;
        // 手动追加偏移按召唤物朝向镜像(骨骼位置已含镜像,只需翻转偏移的 X)。
        start += new Vector2(ProjectileOffset.X * (Scale.X >= 0f ? 1f : -1f), ProjectileOffset.Y);

        var sprite = new AnimatedSprite2D
        {
            // 必须显式命名:AddChildSafely 不强制可读名,无名节点会被 Godot 自动命名。
            Name = "ProjectileEffect",
            SpriteFrames = PreloadManager.Cache.GetAsset<SpriteFrames>(
                $"{Entry.ResPath}/images/vfx/hits/generated/{ProjectileEffectId}/{ProjectileEffectId}_frames.tres"),
            Material = PreloadManager.Cache.GetMaterial($"{Entry.ResPath}/scenes/vfx/hits/"
                + (PremultipliedEffects.Contains(ProjectileEffectId) ? "hit_vfx_premul.tres" : "hit_vfx_add.tres")),
            Offset = ProjectileDrawOffset,
            Scale = Vector2.One * ProjectileScale,
            // 高于双方角色与召唤物(默认 0),保证箭矢横穿战场时始终可见。
            ZIndex = 2,
        };
        sprite.AnimationFinished += () => sprite.QueueFreeSafely();
        NCombatRoom.Instance.AddChildSafely(sprite);
        sprite.GlobalPosition = start;

        Vector2 end = _targetPosition;
        if ((end - start).LengthSquared() > 1f)
        {
            sprite.Rotation = (end - start).Angle();
            var tween = CreateTween();
            tween.TweenProperty(sprite, "global_position", end, ProjectileDuration);
            await ToSignal(tween, "finished");
        }
        sprite.QueueFreeSafely();
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
