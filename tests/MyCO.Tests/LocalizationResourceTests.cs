using System.Text.RegularExpressions;

// Keeps all language dictionaries structurally identical and free of stale alpha labels.
namespace MyCO.Tests;

public sealed partial class LocalizationResourceTests
{
    [Fact]
    public void AllLanguageDictionariesHaveTheSameKeys()
    {
        var root = FindRepositoryRoot();
        var resources = Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Resources");
        var dictionaries = Directory
            .GetFiles(resources, "Strings.*.xaml")
            .ToDictionary(
                path => Path.GetFileName(path)!,
                path => ReadKeys(path),
                StringComparer.OrdinalIgnoreCase);

        var english = dictionaries["Strings.en-US.xaml"];
        Assert.True(english.SetEquals(dictionaries["Strings.zh-CN.xaml"]));
        Assert.True(english.SetEquals(dictionaries["Strings.zh-TW.xaml"]));
        Assert.True(english.SetEquals(dictionaries["Strings.ja-JP.xaml"]));
        Assert.Contains(
            "界面语言",
            File.ReadAllText(Path.Combine(resources, "Strings.zh-CN.xaml")));
        Assert.Contains(
            "介面語言",
            File.ReadAllText(Path.Combine(resources, "Strings.zh-TW.xaml")));
        var japanese = File.ReadAllText(Path.Combine(resources, "Strings.ja-JP.xaml"));
        Assert.Contains("日本語", japanese);
        Assert.Contains("MyCOへようこそ", japanese);
        Assert.Contains("設定", japanese);
    }

    [Fact]
    public void JapaneseIsExposedGloballyWithSystemFontFallbacks()
    {
        var root = FindRepositoryRoot();
        var manager = Path.Combine(root, "src", "MyCO.Manager");
        var localization = File.ReadAllText(Path.Combine(
            manager,
            "Localization",
            "LocalizationService.cs"));
        var tokens = File.ReadAllText(Path.Combine(
            manager,
            "Themes",
            "DesignTokens.xaml"));

        Assert.Contains("LanguageCodes.Japanese", localization);
        Assert.Contains("日本語", localization);
        Assert.Contains("Yu Gothic UI", tokens);
        Assert.Contains("Meiryo UI", tokens);
    }

    [Fact]
    public void EveryLocalizedFormatKeepsTheEnglishPlaceholders()
    {
        var root = FindRepositoryRoot();
        var resources = Path.Combine(root, "src", "MyCO.Manager", "Resources");
        var english = ReadEntries(Path.Combine(resources, "Strings.en-US.xaml"));

        foreach (var path in Directory.GetFiles(resources, "Strings.*.xaml"))
        {
            var localized = ReadEntries(path);
            foreach (var (key, englishValue) in english)
            {
                var expected = PlaceholderRegex().Matches(englishValue)
                    .Select(match => match.Value)
                    .ToArray();
                var actual = PlaceholderRegex().Matches(localized[key])
                    .Select(match => match.Value)
                    .ToArray();
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void EveryDynamicResourceUsedByAViewExists()
    {
        var root = FindRepositoryRoot();
        var manager = Path.Combine(root, "src", "MyCO.Manager");
        var available = ReadKeys(
            Path.Combine(manager, "Resources", "Strings.en-US.xaml"));
        available.UnionWith(ReadKeys(
            Path.Combine(manager, "Themes", "Theme.Dark.xaml")));
        available.UnionWith(ReadKeys(
            Path.Combine(manager, "Themes", "Theme.Light.xaml")));
        var used = Directory
            .GetFiles(Path.Combine(manager, "Views"), "*.xaml")
            .Concat(Directory.GetFiles(Path.Combine(manager, "Controls"), "*.xaml"))
            .Append(Path.Combine(manager, "App.xaml"))
            .SelectMany(path => DynamicResourceRegex()
                .Matches(File.ReadAllText(path))
                .Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(used.Except(available, StringComparer.Ordinal));
    }

    [Fact]
    public void EveryLocalizedBrandDisplayUsesMyCO()
    {
        var root = FindRepositoryRoot();
        var resources = Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Resources");

        foreach (var path in Directory.GetFiles(resources, "Strings.*.xaml"))
        {
            var values = ValueRegex()
                .Matches(File.ReadAllText(path))
                .Select(match => match.Groups[1].Value)
                .ToArray();

            Assert.Contains("It's MyCO!!!!!", values);
            Assert.DoesNotContain(
                values,
                value => value
                    .Replace(@"%APPDATA%\Myco", string.Empty, StringComparison.Ordinal)
                    .Contains("Myco", StringComparison.Ordinal) &&
                    !value.Contains("MyCodex", StringComparison.Ordinal));
        }
    }

    private static HashSet<string> ReadKeys(string path)
    {
        return KeyRegex()
            .Matches(File.ReadAllText(path))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ReadEntries(string path) =>
        EntryRegex()
            .Matches(File.ReadAllText(path))
            .ToDictionary(
                match => match.Groups[1].Value,
                match => match.Groups[2].Value,
                StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyCO.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    [GeneratedRegex("x:Key=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();

    [GeneratedRegex(
        "DynamicResource\\s+([A-Za-z][A-Za-z0-9]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex DynamicResourceRegex();

    [GeneratedRegex(
        "<sys:String\\s+x:Key=\"[^\"]+\">([^<]*)</sys:String>",
        RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    [GeneratedRegex(
        "<sys:String\\s+x:Key=\"([^\"]+)\">([^<]*)</sys:String>",
        RegexOptions.CultureInvariant)]
    private static partial Regex EntryRegex();

    [GeneratedRegex("\\{[0-9]+(?:[^}]*)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
