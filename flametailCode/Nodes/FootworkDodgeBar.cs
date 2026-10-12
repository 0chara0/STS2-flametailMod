using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using flametail.Powers;

namespace flametail.Nodes;

/// <summary>
/// 头顶步法/闪避条：战斗中常驻挂在玩家角色上方。
/// 五段白描边格子代表步法层数（第五格永远空心——满 5 层瞬间转为闪避），
/// 红色填充=当前层数；右侧为闪避图标+层数数字。
/// 悬浮格子显示步法介绍、悬浮图标显示闪避介绍（与原状态栏悬浮一致，
/// 走 NCreature.ShowHoverTips 同一通道）。
///
/// 步法/闪避两个 power 已 override IsVisibleInternal => false 从状态栏隐藏，
/// 本部件是它们唯一的 UI 呈现。
///
/// 跟随方案与 FootFlashVfx 相同：每帧锚场景 IntentPos 标记（头顶校准点，
/// X 取 hitbox 中心）、用 Visuals.GlobalScale 缩放（天然包含相机缩放与挤压
/// 缩放，Boss 战不漂移）。
/// 素材说明：格子为程序绘制（2026-10 新修改，先做占位风格），闪避图标复用
/// flametailDodgePower.png，后续有正式素材可替换。
/// </summary>
public partial class FootworkDodgeBar : Control
{
    // ---- 布局常量（本控件局部像素，随全局缩放整体缩放）----
    private const int SegmentCount = 5;
    private const int SegWidth = 24;
    private const int SegGap = 4;
    private static readonly int[] SegHeights = [14, 18, 22, 26, 30];
    private const int BarWidth = SegmentCount * SegWidth + (SegmentCount - 1) * SegGap; // 136
    private const int ControlHeight = 32; // 最高格 30 + 描边
    private const int IconSize = 30;
    private const int IconX = BarWidth + 8;
    private const int IconY = 2;
    private const int TotalWidth = IconX + IconSize + 22; // 图标右侧留数字位

    private static readonly Color FillColor = new("e23745");
    private static readonly Color OutlineColor = new(1f, 1f, 1f, 0.95f);
    private static readonly Color NumberColor = Colors.White;
    private static readonly Color NumberOutlineColor = new(0f, 0f, 0f, 0.85f);

    private readonly Creature _owner;
    private flametailFootworkPower? _footwork;
    private flametailDodgePower? _dodge;
    private Texture2D? _iconTex;
    private NCreature? _ownerNode;

    public FootworkDodgeBar(Creature owner)
    {
        _owner = owner;
    }

    public override void _Ready()
    {
        base._Ready();

        Size = new Vector2(TotalWidth, ControlHeight);
        // 本体不挡鼠标，命中判定交给两个热区子控件。
        MouseFilter = MouseFilterEnum.Ignore;

        AddChild(CreateHotzone(new Rect2(0, 0, BarWidth, ControlHeight),
            () => ShowPowerTips(_footwork, ModelDb.Power<flametailFootworkPower>)));
        AddChild(CreateHotzone(new Rect2(IconX, IconY, IconSize, IconSize),
            () => ShowPowerTips(_dodge, ModelDb.Power<flametailDodgePower>)));

        // PreloadManager.Cache.GetTexture2D 只查缓存不加载（实测 WARN Asset not cached → null），
        // 与 PowerModel.Icon 同样走 ResourceLoader.Load。
        _iconTex = ResourceLoader.Load<Texture2D>($"{Entry.ResPath}/images/powers/flametailDodgePower.png", null, ResourceLoader.CacheMode.Reuse);

        _owner.PowerApplied += OnPowerApplied;
        _owner.PowerRemoved += OnPowerRemoved;
        foreach (PowerModel power in _owner.Powers)
        {
            TryTrackPower(power);
        }

        QueueRedraw();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _owner.PowerApplied -= OnPowerApplied;
        _owner.PowerRemoved -= OnPowerRemoved;
        UntrackPowers();
    }

    /// <summary>隐形鼠标热区：进入时按归属显示对应 power 的悬浮介绍。</summary>
    private Control CreateHotzone(Rect2 rect, System.Action onEnter)
    {
        var zone = new Control
        {
            Position = rect.Position,
            Size = rect.Size,
            MouseFilter = MouseFilterEnum.Stop
        };
        zone.MouseEntered += onEnter;
        zone.MouseExited += () => _ownerNode?.HideHoverTips();
        return zone;
    }

    /// <summary>
    /// 悬浮显示 power 介绍（与原状态栏悬浮同一 ShowHoverTips 通道）。
    /// 实例不存在（0 层/未施加）时用 ModelDb 模板兜底——条与图标是常驻画的，
    /// 任何时候悬浮都应有介绍。
    /// </summary>
    private void ShowPowerTips(PowerModel? instance, Func<PowerModel> fallback)
    {
        _ownerNode ??= NCombatRoom.Instance?.GetCreatureNode(_owner);
        if (_ownerNode == null)
        {
            return;
        }

        PowerModel power = instance ?? fallback();
        _ownerNode.ShowHoverTips(power.HoverTips);
    }

    private void TryTrackPower(PowerModel power)
    {
        switch (power)
        {
            case flametailFootworkPower footwork when _footwork != footwork:
                _footwork = footwork;
                footwork.DisplayAmountChanged += Refresh;
                footwork.Removed += OnFootworkRemoved;
                Refresh();
                break;
            case flametailDodgePower dodge when _dodge != dodge:
                _dodge = dodge;
                dodge.DisplayAmountChanged += Refresh;
                dodge.Removed += OnDodgeRemoved;
                Refresh();
                break;
        }
    }

    private void OnFootworkRemoved()
    {
        if (_footwork != null)
        {
            _footwork.DisplayAmountChanged -= Refresh;
            _footwork.Removed -= OnFootworkRemoved;
            _footwork = null;
        }
        Refresh();
    }

    private void OnDodgeRemoved()
    {
        if (_dodge != null)
        {
            _dodge.DisplayAmountChanged -= Refresh;
            _dodge.Removed -= OnDodgeRemoved;
            _dodge = null;
        }
        Refresh();
    }

    private void UntrackPowers()
    {
        OnFootworkRemoved();
        OnDodgeRemoved();
    }

    private void OnPowerApplied(PowerModel power)
    {
        TryTrackPower(power);
    }

    private void OnPowerRemoved(PowerModel power)
    {
        if (power == _footwork || power == _dodge)
        {
            UntrackPowers();
        }
    }

    private void Refresh()
    {
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_ownerNode == null || !GodotObject.IsInstanceValid(_ownerNode))
        {
            _ownerNode = NCombatRoom.Instance?.GetCreatureNode(_owner);
            if (_ownerNode == null)
            {
                return;
            }
        }

        // 锚定：Y 用场景自身的 IntentPos 标记（每个角色头顶校准点，原版意图图标同款
        // NCreatureVisuals.IntentPosition）；X 仍取 hitbox 中心。焰尾场景原 IntentPos
        // (42,-426.4) 从未校准过（玩家不显示意图，错位无人发现），已在 tscn 改为 (0,-330)
        // ——头顶绘制顶约 -303.8（Visuals -19 − 骨架 284.8px）。缩放取 Visuals.GlobalScale
        // （相机缩放×挤压缩放），与 FootFlashVfx 同一套，避免 Boss 战漂移。
        Control hitbox = _ownerNode.Hitbox;
        float s = _ownerNode.Visuals.GlobalScale.X;
        Vector2 topCenter = hitbox.GetGlobalTransform() * new Vector2(hitbox.Size.X * 0.5f, 0f);
        Marker2D? anchor = _ownerNode.Visuals.IntentPosition;
        float anchorY = anchor != null ? anchor.GlobalPosition.Y : topCenter.Y;
        GlobalPosition = new Vector2(topCenter.X, anchorY) + new Vector2(-TotalWidth * 0.5f, -ControlHeight * 0.5f) * s;
        // Control 没有全局缩放属性：局部 Scale = 目标全局缩放 ÷ 父容器全局缩放，
        // 缩放围绕原点（PivotPoint 归零）使绘制与两个热区的命中范围一致。
        PivotOffset = Vector2.Zero;
        float parentScale = GetParent() is CanvasItem p ? p.GetGlobalTransform().Scale.X : 1f;
        Scale = Vector2.One * (s / parentScale);
    }

    public override void _Draw()
    {
        base._Draw();

        int footwork = _footwork?.DisplayAmount ?? 0;
        for (int i = 0; i < SegmentCount; i++)
        {
            var rect = new Rect2(i * (SegWidth + SegGap), ControlHeight - SegHeights[i], SegWidth, SegHeights[i]);
            if (footwork > i)
            {
                DrawRect(rect, FillColor);
            }
            DrawRect(rect, OutlineColor, false, 2f);
        }

        if (_iconTex != null)
        {
            DrawTextureRect(_iconTex, new Rect2(IconX, IconY, IconSize, IconSize), false, Colors.White);
        }

        int dodge = _dodge?.DisplayAmount ?? 0;
        Font font = ThemeDB.FallbackFont;
        Vector2 pos = new(IconX + IconSize + 4, ControlHeight - 6);
        DrawStringOutline(font, pos, dodge.ToString(), HorizontalAlignment.Left, -1, 16, 3, NumberOutlineColor);
        DrawString(font, pos, dodge.ToString(), HorizontalAlignment.Left, -1, 16, NumberColor);
    }

    /// <summary>
    /// 战斗房间加入玩家角色节点时挂条（由 FootworkDodgeBarSpawnPatch 调用）。
    /// 挂在 NCreature 节点本体下而不是 CombatVfxContainer：悬浮热区依赖 Godot 的
    /// GUI 拾取（按场景树序，不看 ZIndex），角色节点内的悬浮 UI（意图图标等）是
    /// 验证可用的通道；测试模式不挂。
    /// </summary>
    public static void TryAttach(Creature creature)
    {
        if (TestMode.IsOn || NCombatRoom.Instance == null)
        {
            return;
        }

        NCreature? node = NCombatRoom.Instance.GetCreatureNode(creature);
        node?.AddChildSafely(new FootworkDodgeBar(creature));
    }
}
