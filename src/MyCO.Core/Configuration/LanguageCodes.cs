// Provides canonical language codes so config and WPF resource names stay in sync.
namespace MyCO.Configuration;

public static class LanguageCodes
{
    public const string English = "en-US";
    public const string SimplifiedChinese = "zh-CN";
    public const string TraditionalChinese = "zh-TW";
    public const string Japanese = "ja-JP";

    private static readonly HashSet<string> Supported =
        new(StringComparer.OrdinalIgnoreCase)
        {
            English,
            SimplifiedChinese,
            TraditionalChinese,
            Japanese
        };

    public static bool IsSupported(string? language)
    {
        return language is not null && Supported.Contains(language);
    }

    public static string Normalize(string? language)
    {
        if (language is null)
        {
            return English;
        }
        return Supported.FirstOrDefault(
                   item => string.Equals(item, language, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException(
                   $"Unsupported interface language: {language}.",
                   nameof(language));
    }
}
