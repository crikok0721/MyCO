using System.Globalization;
using System.Reflection;
using System.Text;
using MyCO.Configuration;
using MyCO.Manager.Localization;

namespace MyCO.Tests;

public sealed class LocaleFontCatalogTests
{
    [Theory]
    [InlineData(
        "en-US",
        "Segoe UI Variable, Segoe UI, Arial, sans-serif",
        "\"Segoe UI Variable\", \"Segoe UI\", Arial, sans-serif")]
    [InlineData(
        "zh-CN",
        "Segoe UI Variable, Microsoft YaHei UI, Microsoft YaHei, Noto Sans CJK SC, Noto Sans SC, sans-serif",
        "\"Segoe UI Variable\", \"Microsoft YaHei UI\", \"Microsoft YaHei\", \"Noto Sans CJK SC\", \"Noto Sans SC\", sans-serif")]
    [InlineData(
        "zh-TW",
        "Segoe UI Variable, Microsoft JhengHei UI, Microsoft JhengHei, Noto Sans CJK TC, Noto Sans TC, sans-serif",
        "\"Segoe UI Variable\", \"Microsoft JhengHei UI\", \"Microsoft JhengHei\", \"Noto Sans CJK TC\", \"Noto Sans TC\", sans-serif")]
    [InlineData(
        "ja-JP",
        "Segoe UI Variable, Yu Gothic UI, Yu Gothic, Meiryo, Noto Sans CJK JP, Noto Sans JP, sans-serif",
        "\"Segoe UI Variable\", \"Yu Gothic UI\", \"Yu Gothic\", Meiryo, \"Noto Sans CJK JP\", \"Noto Sans JP\", sans-serif")]
    public void LocaleProfilesUseTheRequiredOrderedStacks(
        string locale,
        string expectedWpfStack,
        string expectedCssStack)
    {
        var profile = GetProfile(locale);

        Assert.Equal(expectedWpfStack, ReadString(profile, "WpfFontStack"));
        Assert.Equal(expectedCssStack, ReadString(profile, "CssFontStack"));
        Assert.Equal(locale, ReadString(profile, "Locale"));
    }

    [Fact]
    public void LocaleProfilesDoNotCrossPollinateCjkFamilies()
    {
        var stacks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["zh-CN"] = ReadString(GetProfile("zh-CN"), "WpfFontStack"),
            ["zh-TW"] = ReadString(GetProfile("zh-TW"), "WpfFontStack"),
            ["ja-JP"] = ReadString(GetProfile("ja-JP"), "WpfFontStack")
        };

        Assert.DoesNotContain("Yu Gothic", stacks["zh-CN"]);
        Assert.DoesNotContain("Meiryo", stacks["zh-CN"]);
        Assert.DoesNotContain("Microsoft JhengHei", stacks["zh-CN"]);
        Assert.DoesNotContain("Noto Sans CJK JP", stacks["zh-CN"]);
        Assert.DoesNotContain("Microsoft YaHei", stacks["zh-TW"]);
        Assert.DoesNotContain("Yu Gothic", stacks["zh-TW"]);
        Assert.DoesNotContain("Noto Sans CJK SC", stacks["zh-TW"]);
        Assert.DoesNotContain("Microsoft YaHei", stacks["ja-JP"]);
        Assert.DoesNotContain("Microsoft JhengHei", stacks["ja-JP"]);
        Assert.DoesNotContain("Noto Sans CJK TC", stacks["ja-JP"]);
    }

    [Fact]
    public void ApplyingLanguageSynchronizesCurrentCultureAndUiCulture()
    {
        var currentCulture = CultureInfo.CurrentCulture;
        var currentUiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        try
        {
            LocalizationService.ApplyLanguage("ZH-cn");

            Assert.Equal(LanguageCodes.SimplifiedChinese, LocalizationService.CurrentLanguage);
            Assert.Equal(LanguageCodes.SimplifiedChinese, CultureInfo.CurrentCulture.Name);
            Assert.Equal(LanguageCodes.SimplifiedChinese, CultureInfo.CurrentUICulture.Name);
            Assert.Equal(
                LanguageCodes.SimplifiedChinese,
                CultureInfo.DefaultThreadCurrentCulture?.Name);
            Assert.Equal(
                LanguageCodes.SimplifiedChinese,
                CultureInfo.DefaultThreadCurrentUICulture?.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = currentCulture;
            CultureInfo.CurrentUICulture = currentUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
        }
    }

    [Fact]
    public void SharedWpfTypographyIsLocaleDynamicAndWindowLanguageAware()
    {
        var root = FindRepositoryRoot();
        var manager = Path.Combine(root, "src", "MyCO.Manager");
        var styles = File.ReadAllText(
            Path.Combine(manager, "Themes", "SharedStyles.xaml"),
            Encoding.UTF8);
        var tokens = File.ReadAllText(
            Path.Combine(manager, "Themes", "DesignTokens.xaml"),
            Encoding.UTF8);

        Assert.Contains("Value=\"{DynamicResource UiFontFamily}\"", styles);
        Assert.Contains(
            "Property=\"FontFamily\" Value=\"{DynamicResource UiFontFamily}\"",
            styles);
        Assert.Contains(
            "Property=\"Language\" Value=\"{DynamicResource UiLanguage}\"",
            styles);
        Assert.Contains(
            "Property=\"UseLayoutRounding\" Value=\"True\"",
            styles);
        Assert.Contains(
            "Property=\"SnapsToDevicePixels\" Value=\"True\"",
            styles);
        Assert.DoesNotContain(
            "Segoe UI, Yu Gothic UI, Meiryo UI, Microsoft YaHei UI, Microsoft JhengHei UI",
            tokens);
    }

    [Fact]
    public void LanguageSelectionAlsoRefreshesTheConnectedRuntime()
    {
        var root = FindRepositoryRoot();
        var viewModel = File.ReadAllText(
            Path.Combine(root, "src", "MyCO.Manager", "ViewModels", "MainWindowViewModel.cs"),
            Encoding.UTF8);

        Assert.Contains("PersistLanguageAsync(value.Code)", viewModel);
        Assert.Contains("await _controller.ApplyConfigAsync(config)", viewModel);
    }

    private static object GetProfile(string locale)
    {
        var catalogType = typeof(LocalizationService)
            .Assembly
            .GetType("MyCO.Manager.Localization.LocaleFontCatalog");
        Assert.NotNull(catalogType);
        var method = catalogType!.GetMethod(
            "For",
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var profile = method!.Invoke(null, [locale]);
        Assert.NotNull(profile);
        return profile!;
    }

    private static string ReadString(object profile, string propertyName)
    {
        var property = profile.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        var value = property!.GetValue(profile) as string;
        Assert.NotNull(value);
        return value!;
    }

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
}
