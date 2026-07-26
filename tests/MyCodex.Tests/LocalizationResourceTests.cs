using System.Text.RegularExpressions;

namespace MyCodex.Tests;

public sealed partial class LocalizationResourceTests
{
    [Fact]
    public void AllLanguageDictionariesHaveTheSameKeys()
    {
        var root = FindRepositoryRoot();
        var resources = Path.Combine(
            root,
            "src",
            "MyCodex.Manager",
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
        Assert.Contains(
            "界面语言",
            File.ReadAllText(Path.Combine(resources, "Strings.zh-CN.xaml")));
        Assert.Contains(
            "介面語言",
            File.ReadAllText(Path.Combine(resources, "Strings.zh-TW.xaml")));
    }

    [Fact]
    public void EveryDynamicResourceUsedByAViewExists()
    {
        var root = FindRepositoryRoot();
        var manager = Path.Combine(root, "src", "MyCodex.Manager");
        var english = ReadKeys(
            Path.Combine(manager, "Resources", "Strings.en-US.xaml"));
        var used = Directory
            .GetFiles(Path.Combine(manager, "Views"), "*.xaml")
            .Append(Path.Combine(manager, "App.xaml"))
            .SelectMany(path => DynamicResourceRegex()
                .Matches(File.ReadAllText(path))
                .Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(used.Except(english, StringComparer.Ordinal));
    }

    private static HashSet<string> ReadKeys(string path)
    {
        return KeyRegex()
            .Matches(File.ReadAllText(path))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyCodex.sln")))
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
}
