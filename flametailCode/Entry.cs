using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Keywords;
using flametail.Cards;
using flametail.Characters;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace flametail;

[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    // ModId 需要和 flametail.json 里的 id 保持一致。
    // res://flametail/... 里的 flametail 是 PCK 资源目录，不是 C# namespace。
    public const string ModId = "flametail";
    public const string ResPath = $"res://{ModId}";

    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 以下示例默认已经在 Entry.Initialize() 中调用了
        // RitsuLibFramework.EnsureGodotScriptsRegistered(...) 和
        // ModTypeDiscoveryHub.RegisterModAssembly(...)，否则自动注册不会生效。
        //
        // Godot C# 脚本注册只负责让 pck 中的脚本类型能被 Godot 找到。
        // 这一步和 RitsuLib 的内容自动注册不是同一件事，两个都需要保留。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);

        // 自动注册扫描会读取当前程序集里的 RegisterCard/RegisterRelic 等 attribute。
        // 新增内容类后，只要 attribute 写对，通常不需要在入口里手动逐个注册。
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 初始卡组顺序：attribute 方式注册不暴露 order 参数（全部按发现顺序，即类型名字母序）排队，
        // 因此这里移除各卡牌上的 [RegisterCharacterStarterCard]，改为显式注册并指定 order：
        // 打击 → 防御 → 闪击 → 迅敏（与其他角色的“打击→防御→特殊攻击→特殊技能”一致）。
        ModContentRegistry registry = ModContentRegistry.For(ModId);
        registry.RegisterCharacterStarterCard(typeof(flametailCharacter), typeof(flametailStrike), 4, 0);
        registry.RegisterCharacterStarterCard(typeof(flametailCharacter), typeof(flametailDefend), 4, 1);
        registry.RegisterCharacterStarterCard(typeof(flametailCharacter), typeof(flametailJab), 1, 2);
        registry.RegisterCharacterStarterCard(typeof(flametailCharacter), typeof(flametailSwift), 1, 3);

        // [RegisterOwnedCardKeyword] 不会被 ModTypeDiscoveryHub 自动处理，
        // 因此必须手动注册“反制”“疼痛”“支援”“即逝”“步法”词条，这样卡牌 CanonicalKeywords 里的 CardKeyword 值才能被识别。
        // 注意：这里才是真正生效的注册（attribute 上的 CardDescriptionPlacement 不生效）。
        // “反制”/“步法”用 None：不向卡牌描述注入金句（描述里已由各卡以 [gold] 手动呈现“反制/反制时：”），
        // 仅保留 IncludeInCardHoverTip=true 的悬停说明。
        ModKeywordRegistry.For(ModId)
            .RegisterCardKeywordOwnedByLocNamespace(
                "Counter",
                iconPath: null,
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: true);
        ModKeywordRegistry.For(ModId)
            .RegisterCardKeywordOwnedByLocNamespace(
                "Pain",
                iconPath: null,
                ModKeywordCardDescriptionPlacement.BeforeCardDescription,
                includeInCardHoverTip: true);
        ModKeywordRegistry.For(ModId)
            .RegisterCardKeywordOwnedByLocNamespace(
                "Support",
                iconPath: null,
                ModKeywordCardDescriptionPlacement.BeforeCardDescription,
                includeInCardHoverTip: true);
        ModKeywordRegistry.For(ModId)
            .RegisterCardKeywordOwnedByLocNamespace(
                "Ephemeral",
                iconPath: null,
                // 即逝用 None：不注入独立行（烛火一闪的描述里已手动内联“即逝。”，
                // 与“固有”合并到同一行展示），仅保留悬停说明。
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: true);
        ModKeywordRegistry.For(ModId)
            .RegisterCardKeywordOwnedByLocNamespace(
                "Footwork",
                iconPath: null,
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: true);

        // 应用 Harmony 补丁（闪避的格挡前缓冲逻辑）。
        new Harmony(ModId).PatchAll(assembly);

        Logger.Info("flametail initialized.");
    }
}
