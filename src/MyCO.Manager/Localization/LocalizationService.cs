using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using MyCO.Configuration;

// Swaps WPF resource dictionaries at runtime and exposes localized string helpers.
namespace MyCO.Manager.Localization;

public sealed record LanguageOption(string Code, string DisplayName);

public static class LocalizationService
{
    private const string ResourcePrefix = "Resources/Strings.";

    public static IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
    [
        new(LanguageCodes.English, "English"),
        new(LanguageCodes.SimplifiedChinese, "简体中文"),
        new(LanguageCodes.TraditionalChinese, "繁體中文"),
        new(LanguageCodes.Japanese, "日本語")
    ];

    public static string CurrentLocale { get; private set; } =
        LanguageCodes.English;

    // Compatibility name retained for existing callers; it is always canonical.
    public static string CurrentLanguage => CurrentLocale;

    public static event EventHandler? LanguageChanged;

    public static void ApplyLanguage(string language)
    {
        var normalized = LanguageCodes.Normalize(language);
        var profile = LocaleFontCatalog.For(normalized);
        var culture = CultureInfo.GetCultureInfo(normalized);
        CurrentLocale = normalized;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        // Replace exactly one language dictionary while preserving theme resources.
        var dictionaries = resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(
            dictionary => dictionary.Source?.OriginalString.StartsWith(
                ResourcePrefix,
                StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary
        {
            Source = new Uri(
                $"{ResourcePrefix}{normalized}.xaml",
                UriKind.Relative)
        };
        if (current is null)
        {
            dictionaries.Insert(0, replacement);
        }
        else
        {
            dictionaries[dictionaries.IndexOf(current)] = replacement;
        }

        resources["UiFontFamily"] =
            new System.Windows.Media.FontFamily(profile.WpfFontStack);
        resources["UiLanguage"] = XmlLanguage.GetLanguage(normalized);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        return System.Windows.Application.Current?.TryFindResource(key) as string
               ?? key;
    }

    public static string Format(string key, params object?[] arguments)
    {
        return string.Format(
            CultureInfo.CurrentUICulture,
            Get(key),
            arguments);
    }
}
