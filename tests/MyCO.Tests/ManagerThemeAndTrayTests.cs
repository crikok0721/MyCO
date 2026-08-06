using System.Text.RegularExpressions;
using System.Xml.Linq;
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
    public void PreviewDefaultsFromTheEffectiveManagerThemeAndCanBeManuallyOverridden()
    {
        var root = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "ViewModels",
            "MainWindowViewModel.cs"));

        Assert.Contains(
            "ToPreviewTheme(_themeService.EffectiveTheme)",
            viewModel);
        Assert.Contains("_previewThemeFollowsManager", viewModel);
        Assert.Contains("_previewThemeFollowsManager = false", viewModel);
        Assert.DoesNotContain(
            "RefreshCodexPreviewThemeOptions(CodexPreviewTheme.Dark)",
            viewModel);
    }

    [Fact]
    public void SettingsPageUsesRoundedSwitchesAndRequestedCopy()
    {
        var root = FindRepositoryRoot();
        var manager = Path.Combine(root, "src", "MyCO.Manager");
        var settings = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "SettingsPage.xaml"));
        var sharedStyles = File.ReadAllText(Path.Combine(
            manager,
            "Themes",
            "SharedStyles.xaml"));
        var simplified = File.ReadAllText(Path.Combine(
            manager,
            "Resources",
            "Strings.zh-CN.xaml"));

        Assert.Equal(3, Regex.Matches(settings, "SwitchCheckBox").Count);
        Assert.DoesNotContain("StartupDescription", settings);
        Assert.DoesNotContain("LaunchAtLoginDescription", settings);
        Assert.DoesNotContain("StartupSafetyNote", settings);
        Assert.Contains("CornerRadius=\"{StaticResource RadiusControl}\"", sharedStyles);
        Assert.DoesNotContain("<Ellipse x:Name=\"SwitchKnob\"", sharedStyles);
        Assert.Contains("仅修改启动器主题，不修改Codex主题", simplified);
        Assert.Contains("MyCO启动后自动启动Codex", simplified);
        Assert.Contains(
            "若Codex已启动，MyCO不会主动修改已启动的Codex",
            simplified);
        Assert.Contains("AssociateCodexLaunches", settings);
        Assert.Contains("将 MyCO 关联到 Codex 启动", simplified);
        Assert.DoesNotContain("StartupSafetyNote", simplified);
    }

    [Fact]
    public void FirstRunDefaultsUseThePackagedAssistantIdentity()
    {
        var root = FindRepositoryRoot();
        var config = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Core",
            "Configuration",
            "AppConfig.cs"));
        var viewModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "ViewModels",
            "MainWindowViewModel.cs"));

        Assert.Contains("Name = \"菲叶子\"", config);
        Assert.Contains("SeedFirstRunAssistantAvatarAsync", viewModel);
        Assert.Contains("pack://application:,,,/Assets/MyCO-logo.png", viewModel);
        Assert.Contains("ImportAsync(resource.Stream)", viewModel);
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
    public void TrayNotificationIsClaimedOnlyOncePerWindowsBoot()
    {
        const string boot = "2026-08-02T05:00:00.0000000+00:00";

        Assert.True(TrayNotificationPolicy.ShouldNotify(true, boot, null));
        Assert.False(TrayNotificationPolicy.ShouldNotify(true, boot, boot));
        Assert.False(TrayNotificationPolicy.ShouldNotify(false, boot, null));
        Assert.True(TrayNotificationPolicy.ShouldNotify(
            true,
            "2026-08-03T05:00:00.0000000+00:00",
            boot));
    }

    [Fact]
    public void MainWindowTitleBarUsesFilledIconAndLocalizedBrandSubtitle()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Views",
            "MainWindow.xaml"));

        Assert.Contains("Stretch=\"UniformToFill\"", xaml);
        Assert.Contains("ClipToBounds=\"True\"", xaml);
        Assert.Contains("BrandTitleMy", xaml);
        Assert.Contains("BrandTitleCo", xaml);
        Assert.Contains("BrandSubtitle", xaml);
    }

    [Fact]
    public void BrandResourcesRemainSynchronizedAcrossFourLocales()
    {
        var root = FindRepositoryRoot();
        foreach (var culture in new[] { "en-US", "zh-CN", "zh-TW", "ja-JP" })
        {
            var resources = File.ReadAllText(Path.Combine(
                root,
                "src",
                "MyCO.Manager",
                "Resources",
                $"Strings.{culture}.xaml"));

            Assert.Contains("x:Key=\"BrandTitleMy\"", resources);
            Assert.Contains("x:Key=\"BrandTitleCo\"", resources);
            Assert.Contains(
                "<sys:String x:Key=\"BrandSubtitle\">It's MyCO!!!!!</sys:String>",
                resources);
        }
    }

    [Fact]
    public void NewlyTouchedUserCopyDoesNotExposeRuntimeImplementationTerms()
    {
        var root = FindRepositoryRoot();
        var touchedKeys = new[]
        {
            "BrandTitleMy",
            "BrandTitleCo",
            "BrandSubtitle",
            "AssociateCodexLaunches",
            "AssociateCodexLaunchesDescription",
            "TrayMinimizedNotification",
            "UpdateTitle",
            "UpdateStatusReady",
            "UpdateStatusChecking",
            "UpdateStatusUpToDate",
            "UpdateStatusAvailableFormat",
            "UpdateStatusOffline",
            "UpdateStatusTimeout",
            "UpdateStatusRateLimited",
            "UpdateStatusInvalid",
            "UpdateDialogTitle",
            "UpdateDialogDescriptionFormat",
            "UpdateNow",
            "UpdateLater",
            "UpdateStatusDownloading",
            "UpdateStatusFailed",
            "UpdateStatusPermission",
            "FactoryResetConfirmTitle",
            "FactoryResetConfirmDescription",
            "FactoryResetDeletesTitle",
            "FactoryResetDeleteAppearance",
            "FactoryResetDeleteCalibration",
            "FactoryResetDeleteDiagnostics",
            "FactoryResetDeleteStartup",
            "FactoryResetKeepsTitle",
            "FactoryResetKeepProgram",
            "FactoryResetKeepCodexProgram",
            "FactoryResetKeepCodexData"
        };
        var forbiddenTerms = new[]
        {
            "Runtime",
            "pipe",
            "lifecycle",
            "schema",
            "signature",
            "protocol"
        };

        foreach (var culture in new[] { "en-US", "zh-CN", "zh-TW", "ja-JP" })
        {
            var document = ReadManagerXaml("Resources", $"Strings.{culture}.xaml");
            foreach (var element in Elements(document, "String")
                         .Where(element => Attribute(element, "Key") is { } key &&
                                           touchedKeys.Contains(
                                               key,
                                               StringComparer.Ordinal)))
            {
                foreach (var forbidden in forbiddenTerms)
                {
                    Assert.DoesNotContain(
                        forbidden,
                        element.Value,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
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
        Assert.Equal(2, Regex.Matches(preview, "PreviewUserBubble").Count);
        Assert.Equal(2, Regex.Matches(preview, "PreviewUserText").Count);
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
    public void PreviewAndSetupSurfacesUseSharedCompactGeometry()
    {
        var root = FindRepositoryRoot();
        var manager = Path.Combine(root, "src", "MyCO.Manager");
        var preview = File.ReadAllText(Path.Combine(
            manager,
            "Controls",
            "ChatPreviewControl.xaml"));
        var previewCode = File.ReadAllText(Path.Combine(
            manager,
            "Controls",
            "ChatPreviewControl.xaml.cs"));
        var settings = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "SettingsPage.xaml"));
        var calibration = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "CalibrationPage.xaml"));
        var onboarding = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "OnboardingWindow.xaml"));

        Assert.Contains("x:Key=\"PreviewBubbleStyle\"", preview);
        Assert.Equal(2, Regex.Matches(preview, "Style=\"\\{StaticResource PreviewBubbleStyle\\}\"").Count);
        Assert.Contains("PreviewBubblePadding", preview);
        Assert.Contains("PreviewBubblePaddingProperty", previewCode);
        Assert.DoesNotContain("BorderThickness=\"1\"\n                                CornerRadius=\"{Binding BubbleRadius}\"", preview);

        Assert.Contains("Margin=\"0,0,0,16\"", settings);
        Assert.Contains("LaunchCodexOnStartDescription", settings);
        Assert.DoesNotContain("Margin=\"24,5,0,0\"", settings);

        Assert.Contains("x:Name=\"CalibrationWorkspace\"", calibration);
        Assert.Equal(2, Regex.Matches(calibration, "CalibrationStepRow").Count);
        Assert.DoesNotContain("ElevatedCardBorder", calibration);
        Assert.DoesNotContain("⌖", calibration);

        Assert.Contains("WindowStyle=\"None\"", onboarding);
        Assert.Contains("WindowChrome.WindowChrome", onboarding);
        Assert.Contains("x:Name=\"OnboardingTitleBar\"", onboarding);
        Assert.Contains("x:Name=\"OnboardingSteps\"", onboarding);
        Assert.DoesNotContain("DropShadowEffect", onboarding);
    }

    [Fact]
    public void SettingsExposeAConfirmableLocalizedFactoryReset()
    {
        var root = FindRepositoryRoot();
        var manager = Path.Combine(root, "src", "MyCO.Manager");
        var settings = File.ReadAllText(Path.Combine(manager, "Views", "SettingsPage.xaml"));
        var confirmation = File.ReadAllText(Path.Combine(
            manager,
            "Views",
            "ResetConfirmationWindow.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(
            manager,
            "ViewModels",
            "MainWindowViewModel.cs"));

        Assert.Contains("FactoryResetTitle", settings);
        Assert.Contains("FactoryResetCommand", settings);
        Assert.Contains("DangerButton", settings);
        Assert.Contains("IsDefault=\"True\"", confirmation);
        Assert.Contains("IsCancel=\"True\"", confirmation);
        Assert.Contains("FactoryResetDeletes", confirmation);
        Assert.Contains("FactoryResetKeeps", confirmation);
        Assert.Contains("FactoryResetService", viewModel);
        Assert.Contains("DisableSkinAsync", viewModel);
        Assert.Contains("SetEnabled(executable, enabled: false)", viewModel);
        Assert.Contains("new OnboardingWindow(this)", viewModel);
    }

    [Fact]
    public void PremiumManagerResourcesUseTheExactVisibleMyCOBrand()
    {
        var root = FindRepositoryRoot();
        foreach (var culture in new[] { "en-US", "zh-CN", "zh-TW", "ja-JP" })
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
        var alpha = ReadPngAlphaSamples(
            Path.Combine(root, "assets", "MyCO-logo.png"),
            (0, 0),
            (255, 255),
            (128, 128));
        Assert.Equal(0, alpha[(0, 0)]);
        Assert.Equal(0, alpha[(255, 255)]);
        Assert.Equal(255, alpha[(128, 128)]);

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
        var app = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "App.xaml.cs"));

        Assert.Contains("MaximizeRestoreButton", xaml);
        Assert.Contains("WindowChrome", xaml);
        Assert.Contains("PrepareForBackground(userInitiated: true)", code);
        Assert.Contains("ShowInTaskbar = false", code);
        Assert.Contains("UserMinimizedToTray", code);
        Assert.DoesNotContain("SystemCommands.MinimizeWindow(this)", code);
        Assert.Contains("SystemCommands.MaximizeWindow(this)", code);
        Assert.Contains("PrepareForBackground()", app);
        Assert.DoesNotContain(
            "WindowState = WindowState.Normal;\n        Hide();",
            code.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void AssociatedLaunchUsesTheExistingSingleInstanceActivationPath()
    {
        var root = FindRepositoryRoot();
        var presentation = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Services",
            "TrayWindowStateMachine.cs"));
        var app = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "App.xaml.cs"));

        Assert.Contains("--codex-launch", presentation);
        Assert.Contains("CodexLaunchEventName", app);
        Assert.Contains("StartFromAssociatedLaunchAsync", app);
        Assert.Contains("MyCOAppIdentity.Apply", app);
        Assert.DoesNotContain("Process.GetProcessesByName", app);
    }

    [Theory]
    [InlineData(new[] { "--background" }, 0)]
    [InlineData(new[] { "--codex-launch" }, 2)]
    [InlineData(new[] { "--background", "--codex-launch" }, 2)]
    [InlineData(new string[0], 1)]
    public void DuplicateInstanceArgumentsRouteWithoutWakingBackgroundLaunches(
        string[] arguments,
        int expected)
    {
        Assert.Equal(
            (DuplicateInstanceAction)expected,
            StartupPresentation.RouteDuplicateInstance(arguments));
    }

    [Fact]
    public void TrayNotificationUsesOneBalloonEventAndLocalizedText()
    {
        var root = FindRepositoryRoot();
        var tray = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Services",
            "TrayService.cs"));
        Assert.Single(Regex.Matches(tray, "ShowBalloonTip").Cast<Match>());
        Assert.Contains("TrayMinimizedNotification", tray);
        Assert.Contains("UserMinimizedToTray", tray);
        Assert.Contains("It's MyCO!!!!!", tray);
        Assert.Contains("BalloonTipClicked", tray);
        Assert.Contains("HandleBalloonTipClicked", tray);
    }

    [Fact]
    public void FactoryResetPersistsTheBootScopedTrayClaimBeforeCommit()
    {
        var root = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "ViewModels",
            "MainWindowViewModel.cs"));

        Assert.Contains("TrayMinimizeNotificationBootId =", viewModel);
        Assert.Contains("await SaveConfigAsync(defaults).ConfigureAwait(true);", viewModel);
        Assert.Contains("transaction.Commit();", viewModel);
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

    private static Dictionary<(int X, int Y), byte> ReadPngAlphaSamples(
        string path,
        params (int X, int Y)[] samples)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            bytes.Take(8).ToArray());

        var idat = new List<byte>();
        var offset = 8;
        var width = 0;
        var height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        byte interlace = 0;
        while (offset < bytes.Length)
        {
            var length = ReadBigEndianInt32(bytes, offset);
            offset += 4;
            var type = System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
            offset += 4;
            var data = bytes.AsSpan(offset, length);
            offset += length + 4; // chunk data plus CRC

            if (type == "IHDR")
            {
                width = ReadBigEndianInt32(data.ToArray(), 0);
                height = ReadBigEndianInt32(data.ToArray(), 4);
                bitDepth = data[8];
                colorType = data[9];
                interlace = data[12];
            }
            else if (type == "IDAT")
            {
                idat.AddRange(data.ToArray());
            }
            else if (type == "IEND")
            {
                break;
            }
        }

        Assert.True(width > 0 && height > 0);
        Assert.Equal(8, bitDepth);
        Assert.Equal(6, colorType); // RGBA
        Assert.Equal(0, interlace);

        using var compressed = new MemoryStream(idat.ToArray());
        using var zlib = new System.IO.Compression.ZLibStream(
            compressed,
            System.IO.Compression.CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        zlib.CopyTo(decoded);
        var scanlines = decoded.ToArray();
        var rowBytes = checked(width * 4);
        var expectedLength = checked(height * (rowBytes + 1));
        Assert.Equal(expectedLength, scanlines.Length);

        var wanted = samples.ToHashSet();
        foreach (var (x, y) in samples)
        {
            Assert.InRange(x, 0, width - 1);
            Assert.InRange(y, 0, height - 1);
        }

        var result = new Dictionary<(int X, int Y), byte>();
        var previous = new byte[rowBytes];
        for (var y = 0; y < height; y++)
        {
            var filter = scanlines[y * (rowBytes + 1)];
            var encoded = scanlines.AsSpan(y * (rowBytes + 1) + 1, rowBytes);
            var current = new byte[rowBytes];
            for (var index = 0; index < rowBytes; index++)
            {
                var left = index >= 4 ? current[index - 4] : (byte)0;
                var up = previous[index];
                var upLeft = index >= 4 ? previous[index - 4] : (byte)0;
                current[index] = filter switch
                {
                    0 => encoded[index],
                    1 => unchecked((byte)(encoded[index] + left)),
                    2 => unchecked((byte)(encoded[index] + up)),
                    3 => unchecked((byte)(encoded[index] + ((left + up) / 2))),
                    4 => unchecked((byte)(encoded[index] + Paeth(left, up, upLeft))),
                    _ => throw new InvalidDataException($"Unsupported PNG filter {filter}.")
                };
            }

            foreach (var sample in wanted.Where(sample => sample.Y == y))
            {
                result[sample] = current[sample.X * 4 + 3];
            }

            previous = current;
        }

        return result;
    }

    private static byte Paeth(byte left, byte up, byte upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upLeftDistance = Math.Abs(estimate - upLeft);
        return leftDistance <= upDistance && leftDistance <= upLeftDistance
            ? left
            : upDistance <= upLeftDistance
                ? up
                : upLeft;
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

    private static XDocument ReadManagerXaml(string directory, string file) =>
        XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MyCO.Manager",
            directory,
            file));

    private static IEnumerable<XElement> Elements(
        XDocument document,
        string localName) =>
        document.Descendants().Where(element =>
            string.Equals(
                element.Name.LocalName,
                localName,
                StringComparison.Ordinal));

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes()
            .FirstOrDefault(attribute =>
                string.Equals(
                    attribute.Name.LocalName,
                    localName,
                    StringComparison.Ordinal))
            ?.Value;

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
