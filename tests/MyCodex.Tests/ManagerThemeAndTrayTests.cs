using System.Text.RegularExpressions;
using MyCodex.Configuration;
using MyCodex.Manager.Services;

namespace MyCodex.Tests;

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
            "MyCodex.Manager",
            "Themes");
        var dark = ReadKeys(Path.Combine(themes, "Theme.Dark.xaml"));
        var light = ReadKeys(Path.Combine(themes, "Theme.Light.xaml"));
        Assert.True(dark.SetEquals(light));
        Assert.Contains("WindowBrush", dark);
        Assert.Contains("ControlDisabledBrush", dark);
        Assert.Contains("FocusBrush", dark);
        Assert.Contains("SelectionBrush", dark);
    }

    private static HashSet<string> ReadKeys(string path) =>
        KeyRegex()
            .Matches(File.ReadAllText(path))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

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
