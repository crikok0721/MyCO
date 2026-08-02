using MyCO.Configuration;

// Locale-specific typography is kept in one table so WPF and the tray use the
// same region-aware ordering; Runtime receives the canonical locale separately.
namespace MyCO.Manager.Localization;

internal sealed record LocaleFontProfile(
    string Locale,
    string WpfFontStack,
    string CssFontStack,
    IReadOnlyList<string> PreferredTrayFamilies);

internal static class LocaleFontCatalog
{
    private static readonly IReadOnlyDictionary<string, LocaleFontProfile> Profiles =
        new Dictionary<string, LocaleFontProfile>(StringComparer.Ordinal)
        {
            [LanguageCodes.English] = new(
                LanguageCodes.English,
                "Segoe UI Variable, Segoe UI, Arial, sans-serif",
                "\"Segoe UI Variable\", \"Segoe UI\", Arial, sans-serif",
                ["Segoe UI Variable", "Segoe UI", "Arial"]),
            [LanguageCodes.SimplifiedChinese] = new(
                LanguageCodes.SimplifiedChinese,
                "Segoe UI Variable, Microsoft YaHei UI, Microsoft YaHei, Noto Sans CJK SC, Noto Sans SC, sans-serif",
                "\"Segoe UI Variable\", \"Microsoft YaHei UI\", \"Microsoft YaHei\", \"Noto Sans CJK SC\", \"Noto Sans SC\", sans-serif",
                ["Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI Variable", "Segoe UI"]),
            [LanguageCodes.TraditionalChinese] = new(
                LanguageCodes.TraditionalChinese,
                "Segoe UI Variable, Microsoft JhengHei UI, Microsoft JhengHei, Noto Sans CJK TC, Noto Sans TC, sans-serif",
                "\"Segoe UI Variable\", \"Microsoft JhengHei UI\", \"Microsoft JhengHei\", \"Noto Sans CJK TC\", \"Noto Sans TC\", sans-serif",
                ["Microsoft JhengHei UI", "Microsoft JhengHei", "Segoe UI Variable", "Segoe UI"]),
            [LanguageCodes.Japanese] = new(
                LanguageCodes.Japanese,
                "Segoe UI Variable, Yu Gothic UI, Yu Gothic, Meiryo, Noto Sans CJK JP, Noto Sans JP, sans-serif",
                "\"Segoe UI Variable\", \"Yu Gothic UI\", \"Yu Gothic\", Meiryo, \"Noto Sans CJK JP\", \"Noto Sans JP\", sans-serif",
                ["Yu Gothic UI", "Yu Gothic", "Meiryo", "Segoe UI Variable", "Segoe UI"])
        };

    public static LocaleFontProfile For(string locale)
    {
        return Profiles[LanguageCodes.Normalize(locale)];
    }
}
