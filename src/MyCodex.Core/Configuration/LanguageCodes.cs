namespace MyCodex.Configuration;

public static class LanguageCodes
{
    public const string English = "en-US";
    public const string SimplifiedChinese = "zh-CN";
    public const string TraditionalChinese = "zh-TW";

    private static readonly HashSet<string> Supported =
        new(StringComparer.OrdinalIgnoreCase)
        {
            English,
            SimplifiedChinese,
            TraditionalChinese
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
