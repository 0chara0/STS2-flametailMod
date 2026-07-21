using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace flametail.Keywords;

/// <summary>
/// 注册 flametail 的自定义卡牌词条。
/// “反制”在战斗中会在卡牌描述前显示为金色标签，并提供悬停说明。
///
/// 注意：[RegisterOwnedCardKeyword] 目前不会被 ModTypeDiscoveryHub 自动处理，
/// 因此 Entry.Initialize 中还需要调用 ModKeywordRegistry.For(...).RegisterCardKeywordOwnedByLocNamespace(...)。
/// </summary>
[RegisterOwnedCardKeyword(
    "Counter",
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(
    "Pain",
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(
    "Footwork",
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(
    "Support",
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(
    "Ephemeral",
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = true)]
public static class FlametailKeywords
{
    /// <summary>
    /// 反制词条的 CardKeyword 值，用于卡牌的 CanonicalKeywords。
    /// 该 ID 由 RegisterOwnedCardKeyword("Counter") 自动生成。
    /// </summary>
    public static readonly CardKeyword Counter =
        ModKeywordRegistry.GetCardKeyword("FLAMETAIL_KEYWORD_COUNTER");

    /// <summary>
    /// 疼痛词条的 CardKeyword 值，用于卡牌的 CanonicalKeywords。
    /// 该 ID 由 RegisterOwnedCardKeyword("Pain") 自动生成。
    /// </summary>
    public static readonly CardKeyword Pain =
        ModKeywordRegistry.GetCardKeyword("FLAMETAIL_KEYWORD_PAIN");

    /// <summary>
    /// 步法词条的 CardKeyword 值，用于卡牌的 CanonicalKeywords。
    /// 该 ID 由 RegisterOwnedCardKeyword("Footwork") 自动生成。
    /// </summary>
    public static readonly CardKeyword Footwork =
        ModKeywordRegistry.GetCardKeyword("FLAMETAIL_KEYWORD_FOOTWORK");

    /// <summary>
    /// 支援词条的 CardKeyword 值，用于卡牌的 CanonicalKeywords。
    /// 该 ID 由 RegisterOwnedCardKeyword("Support") 自动生成。
    /// </summary>
    public static readonly CardKeyword Support =
        ModKeywordRegistry.GetCardKeyword("FLAMETAIL_KEYWORD_SUPPORT");

    /// <summary>
    /// 即逝词条的 CardKeyword 值，用于卡牌的 CanonicalKeywords。
    /// 该 ID 由 RegisterOwnedCardKeyword("Ephemeral") 自动生成。
    /// </summary>
    public static readonly CardKeyword Ephemeral =
        ModKeywordRegistry.GetCardKeyword("FLAMETAIL_KEYWORD_EPHEMERAL");
}
