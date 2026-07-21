using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Keywords;
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

        // [RegisterOwnedCardKeyword] 不会被 ModTypeDiscoveryHub 自动处理，
        // 因此必须手动注册“反制”“疼痛”“支援”“即逝”“步法”词条，这样卡牌 CanonicalKeywords 里的 CardKeyword 值才能被识别。
        ModKeywordRegistry.For(ModId)
            .RegisterCardKeywordOwnedByLocNamespace(
                "Counter",
                iconPath: null,
                ModKeywordCardDescriptionPlacement.BeforeCardDescription,
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
                ModKeywordCardDescriptionPlacement.BeforeCardDescription,
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
