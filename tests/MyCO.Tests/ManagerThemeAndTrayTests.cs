using System.Text.RegularExpressions;
using MyCO.Configuration;
using MyCO.Manager.Services;

namespace MyCO.Tests;

public sealed partial class ManagerThemeAndTrayTests
{
    [Fact]
    public void ThemeServiceAppliesDarkLightAndSystemIndependently()
    {
        var system = new FakeSystemThemeSource(EffectiveManagerTheme.Light);
        var applied = new List<EffectiveManagerTheme>();
        using var service = new ThemeService(system, null, applied.Add);

        service.ApplyMode(ManagerThemeMode.Dark);
        service.ApplyMode(ManagerThemeMode.Light);
        service.ApplyMode(ManagerThemeMode.System);

        Assert.Equal(
            [
                EffectiveManagerTheme.Dark,
                EffectiveManagerTheme.Light,
                EffectiveManagerTheme.Light
            ],
            applied);
        Assert.Equal(ManagerThemeMode.System, service.Mode);
        Assert.Equal(EffectiveManagerTheme.Light, service.EffectiveTheme);
    }

    [Fact]
    public void SystemThemeEventsApplyOnlyInSystemModeAndDisposeUnsubscribes()
    {
        var system = new FakeSystemThemeSource(EffectiveManagerTheme.Dark);
        var applied = new List<EffectiveManagerTheme>();
        var service = new ThemeService(system, null, applied.Add);
        service.ApplyMode(ManagerThemeMode.System);
        system.Set(EffectiveManagerTheme.Light);
        service.ApplyMode(ManagerThemeMode.Dark);
        system.Set(EffectiveManagerTheme.Light);
        var beforeDispose = applied.Count;

        service.Dispose();
        system.Set(EffectiveManagerTheme.Dark);

        Assert.Equal(
            [
                EffectiveManagerTheme.Dark,
                EffectiveManagerTheme.Light,
                EffectiveManagerTheme.Dark
            ],
            applied);
        Assert.Equal(beforeDispose, applied.Count);
        Assert.True(system.WasDisposed);
    }

    [Fact]
    public void UnavailableSystemThemeFallsBackToDarkWithoutCrashing()
    {
        var system = new ThrowingSystemThemeSource();
        var applied = new List<EffectiveManagerTheme>();
        using var service = new ThemeService(system, null, applied.Add);

        service.ApplyMode(ManagerThemeMode.System);

        Assert.Equal(EffectiveManagerTheme.Dark, service.EffectiveTheme);
        Assert.Equal([EffectiveManagerTheme.Dark], applied);
    }

    [Fact]
    public void TrayStateSurvivesTwentyHideRestoreCyclesWithoutDuplicates()
    {
        var state = new TrayWindowStateMachine();
        for (var index = 0; index < 20; index++)
        {
            Assert.True(state.Hide());
            Assert.False(state.Hide());
            Assert.Equal(TrayWindowPresentation.HiddenToTray, state.State);
            Assert.True(state.Restore());
            Assert.False(state.Restore());
            Assert.Equal(TrayWindowPresentation.Visible, state.State);
        }
    }

    [Fact]
    public void BackgroundArgumentSuppressesInitialWindowPresentation()
    {
        Assert.True(StartupPresentation.StartsInBackground(["--background"]));
        Assert.True(StartupPresentation.StartsInBackground(["--BACKGROUND"]));
        Assert.False(StartupPresentation.StartsInBackground([]));
    }

    [Fact]
    public void LightAndDarkThemeDictionariesExposeTheSameSemanticKeys()
    {
        var root = FindRepositoryRoot();
        var themes = Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Themes");
        var dark = ReadKeys(Path.Combine(themes, "Theme.Dark.xaml"));
        var light = ReadKeys(Path.Combine(themes, "Theme.Light.xaml"));
        Assert.True(dark.SetEquals(light));
        Assert.Contains("WindowBrush", dark);
        Assert.Contains("ControlDisabledBrush", dark);
        Assert.Contains("FocusBrush", dark);
        Assert.Contains("SelectionBrush", dark);
        Assert.Contains("AccentBorderBrush", dark);
        Assert.Contains("WarningPanelBrush", dark);
        Assert.Contains("ErrorPanelBrush", dark);
        Assert.Contains("FloatingShadowEffect", dark);
    }

    [Fact]
    public void PremiumDesignTokensExposeTheCompleteMintAndMotionScales()
    {
        var root = FindRepositoryRoot();
        var tokens = ReadKeys(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Themes",
            "DesignTokens.xaml"));

        foreach (var key in new[]
                 {
                     "Mint50Color",
                     "Mint100Color",
                     "Mint200Color",
                     "Mint300Color",
                     "Mint400Color",
                     "Mint500Color",
                     "Mint600Color",
                     "Mint700Color",
                     "RadiusControl",
                     "RadiusChip",
                     "RadiusCard",
                     "RadiusFloating",
                     "RadiusWindow",
                     "MotionFastDuration",
                     "MotionStandardDuration"
                 })
        {
            Assert.Contains(key, tokens);
        }
    }

    [Fact]
    public void PremiumHomeUsesOneCompactSharedPreviewAndExistingCommands()
    {
        var root = FindRepositoryRoot();
        var manager = Path.Combine(root, "src", "MyCO.Manager");
        var shell = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "MainWindow.xaml"));
        var home = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "HomePage.xaml"));
        var preview = File.ReadAllText(Path.Combine(
            manager,
            "Controls",
            "ChatPreviewControl.xaml"));
        var appearance = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "AppearancePage.xaml"));
        var calibration = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "CalibrationPage.xaml"));
        var about = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "AboutPage.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(
            manager,
            "ViewModels",
            "MainWindowViewModel.cs"));
        var app = File.ReadAllText(Path.Combine(manager, "App.xaml.cs"));
        var windowCode = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "MainWindow.xaml.cs"));

        Assert.Contains("<views:HomePage", shell);
        Assert.Contains("SelectHomeCommand", shell);
        Assert.Contains("StartCommand", shell);
        Assert.Contains("RestartCommand", shell);
        Assert.Contains("EnableCommand", shell);
        Assert.Contains("DisableCommand", shell);
        Assert.Contains("SidebarDockBorder", shell);
        Assert.DoesNotContain("StartCommand", home);
        Assert.Contains("CharacterButton", home);
        Assert.Contains("SelectAppearanceCommand", home);
        Assert.Contains("AppearanceStatus", home);
        Assert.Contains("CornerRadius=\"16\"", shell);
        Assert.Contains("RadiusWindow", shell);
        Assert.Contains("DwmSetWindowAttribute", windowCode);
        Assert.Contains("PreviewAssistantMessage", preview);
        Assert.Contains("PreviewUserMessage", preview);
        Assert.Contains("PreviewAssistantBubble", preview);
        Assert.Contains("PreviewUserBubble", preview);
        Assert.Contains("CodexPreviewThemeSelector", preview);
        Assert.DoesNotContain("<ScrollViewer", preview);
        Assert.DoesNotContain("CodeSurfaceBrush", preview);
        Assert.DoesNotContain("PreviewGenerating", preview);
        Assert.DoesNotContain("PreviewErrorState", preview);
        Assert.Single(Regex.Matches(home, "ChatPreviewControl").Cast<Match>());
        Assert.Single(Regex.Matches(appearance, "ChatPreviewControl").Cast<Match>());
        Assert.DoesNotContain("ProductTypeShort", shell);
        Assert.DoesNotContain("NavMain", shell);
        Assert.DoesNotContain("AppearanceSubtitle", appearance);
        Assert.DoesNotContain("AppearanceCharactersHint", appearance);
        Assert.DoesNotContain("CalibrationSubtitle", calibration);
        Assert.DoesNotContain("SignatureDescription", calibration);
        Assert.DoesNotContain("FailClosed", calibration);
        Assert.Contains("Assets/MyCO-logo.png", about);
        Assert.Contains("SelectedCodexPreviewThemeOption", viewModel);
        Assert.Contains("RaisePreviewPalette", viewModel);
        Assert.Contains("SystemParameters.ClientAreaAnimation", app);
        Assert.Contains("TimeSpan.Zero", app);
    }

    [Fact]
    public void PremiumManagerResourcesUseTheExactVisibleMyCOBrand()
    {
        var root = FindRepositoryRoot();
        foreach (var culture in new[] { "en-US", "zh-CN", "zh-TW" })
        {
            var resources = File.ReadAllText(Path.Combine(
                root,
                "src",
                "MyCO.Manager",
                "Resources",
                $"Strings.{culture}.xaml"));

            Assert.Contains("<sys:String x:Key=\"HomeTheme\">MyCO", resources);
            Assert.Contains(
                "<sys:String x:Key=\"PreviewUserMessage\">It's...MyCO!!!!!</sys:String>",
                resources);
            Assert.DoesNotContain(">MYCO<", resources);
            Assert.DoesNotContain(">MYCO ", resources);
        }
    }

    [Fact]
    public void ReleaseIconIsMultiResolutionAndUsedByEveryManagerEntryPoint()
    {
        var root = FindRepositoryRoot();
        var iconPath = Path.Combine(root, "assets", "MyCO.ico");
        var bytes = File.ReadAllBytes(iconPath);
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));
        var count = BitConverter.ToUInt16(bytes, 4);
        var sizes = Enumerable.Range(0, count)
            .Select(index =>
            {
                var value = bytes[6 + 16 * index];
                return value == 0 ? 256 : value;
            })
            .ToArray();
        Assert.Equal([16, 20, 24, 32, 40, 48, 64, 128, 256], sizes);
        var png = File.ReadAllBytes(Path.Combine(
            root,
            "assets",
            "MyCO-logo.png"));
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            png.Take(8).ToArray());
        Assert.Equal(256, ReadBigEndianInt32(png, 16));
        Assert.Equal(256, ReadBigEndianInt32(png, 20));
        using (var bitmap = new System.Drawing.Bitmap(Path.Combine(
                   root,
                   "assets",
                   "MyCO-logo.png")))
        {
            Assert.Equal(0, bitmap.GetPixel(0, 0).A);
            Assert.Equal(0, bitmap.GetPixel(255, 255).A);
            Assert.Equal(255, bitmap.GetPixel(128, 128).A);
        }

        var project = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "MyCO.Manager.csproj"));
        Assert.Contains("<ApplicationIcon>..\\..\\assets\\MyCO.ico</ApplicationIcon>", project);
        Assert.Contains("MyCO-logo.png", project);
        foreach (var view in new[]
                 {
                     "MainWindow.xaml",
                     "OnboardingWindow.xaml",
                     "CloseChoiceWindow.xaml"
                 })
        {
            Assert.Contains(
                "Icon=\"/MyCO;component/Assets/MyCO.ico\"",
                File.ReadAllText(Path.Combine(
                    root,
                    "src",
                    "MyCO.Manager",
                    "Views",
                    view)));
        }
        foreach (var view in new[]
                 {
                     "MainWindow.xaml",
                     "OnboardingWindow.xaml"
                 })
        {
            Assert.Contains(
                "Source=\"/MyCO;component/Assets/MyCO-logo.png\"",
                File.ReadAllText(Path.Combine(
                    root,
                    "src",
                    "MyCO.Manager",
                    "Views",
                    view)));
        }
        var tray = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Services",
            "TrayService.cs"));
        Assert.Contains("pack://application:,,,/Assets/MyCO.ico", tray);
    }

    [Fact]
    public void TaskbarMinimizeAndExplicitTrayHideUseSeparateWindowPaths()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Views",
            "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Views",
            "MainWindow.xaml.cs"));

        Assert.Contains("MaximizeRestoreButton", xaml);
        Assert.Contains("WindowChrome", xaml);
        Assert.Contains("SystemCommands.MinimizeWindow(this)", code);
        Assert.Contains("SystemCommands.MaximizeWindow(this)", code);
        Assert.Contains("PrepareForBackground()", code);
        Assert.DoesNotContain(
            "WindowState = WindowState.Normal;\n        Hide();",
            code.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void CloseChoiceKeepsOnePromptAndKeyboardSafeActions()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Views",
            "CloseChoiceWindow.xaml"));

        Assert.Single(
            Regex.Matches(xaml, "CloseChoiceDescription").Cast<Match>());
        Assert.DoesNotContain("<Image", xaml);
        Assert.Contains("IsDefault=\"True\"", xaml);
        Assert.Contains("IsCancel=\"True\"", xaml);
        Assert.Contains("SizeToContent=\"WidthAndHeight\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", xaml);
    }

    private static HashSet<string> ReadKeys(string path) =>
        KeyRegex()
            .Matches(File.ReadAllText(path))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) |
        (bytes[offset + 1] << 16) |
        (bytes[offset + 2] << 8) |
        bytes[offset + 3];

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

    private sealed class FakeSystemThemeSource(
        EffectiveManagerTheme initialTheme) : ISystemThemeSource
    {
        private EffectiveManagerTheme _theme = initialTheme;

        public bool WasDisposed { get; private set; }
        public event EventHandler? Changed;

        public SystemThemeSnapshot Read() =>
            new(true, _theme, HighContrast: false);

        public void Set(EffectiveManagerTheme theme)
        {
            _theme = theme;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            WasDisposed = true;
            Changed = null;
        }
    }

    private sealed class ThrowingSystemThemeSource : ISystemThemeSource
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public SystemThemeSnapshot Read() =>
            throw new IOException("System theme registry is unavailable.");

        public void Dispose()
        {
        }
    }
}
