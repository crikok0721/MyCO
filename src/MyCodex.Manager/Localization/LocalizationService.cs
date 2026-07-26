using System.Globalization;
using System.Windows;
using MyCodex.Configuration;

// Swaps WPF resource dictionaries at runtime and exposes localized string helpers.
namespace MyCodex.Manager.Localization;

public sealed record LanguageOption(string Code, string DisplayName);

public static class LocalizationService
{
    private const string ResourcePrefix = "Resources/Strings.";

    public static IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
    [
        new(LanguageCodes.English, "English"),
        new(LanguageCodes.SimplifiedChinese, "简体中文"),
        new(LanguageCodes.TraditionalChinese, "繁體中文")
    ];

    public static string CurrentLanguage { get; private set; } =
        LanguageCodes.English;

    public static event EventHandler? LanguageChanged;

    public static void ApplyLanguage(string language)
    {
        var normalized = LanguageCodes.Normalize(language);
        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null)
        {
            CurrentLanguage = normalized;
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

        CurrentLanguage = normalized;
        var culture = CultureInfo.GetCultureInfo(normalized);
        CultureInfo.CurrentUICulture = culture;
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
