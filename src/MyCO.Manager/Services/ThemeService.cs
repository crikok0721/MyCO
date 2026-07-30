using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using MyCO.Configuration;

namespace MyCO.Manager.Services;

internal enum EffectiveManagerTheme
{
    Dark,
    Light
}

internal sealed record SystemThemeSnapshot(
    bool IsAvailable,
    EffectiveManagerTheme Theme,
    bool HighContrast);

internal interface ISystemThemeSource : IDisposable
{
    SystemThemeSnapshot Read();
    event EventHandler? Changed;
}

// Resolves Dark/Light/System independently from the renderer bubble theme.
internal sealed class ThemeService : IDisposable
{
    private const string ThemePrefix = "Themes/Theme.";
    private readonly ISystemThemeSource _systemTheme;
    private readonly Dispatcher? _dispatcher;
    private readonly Action<EffectiveManagerTheme> _applyTheme;
    private bool _disposed;

    public ThemeService()
        : this(
            new WindowsSystemThemeSource(),
            System.Windows.Application.Current?.Dispatcher,
            ApplyResourceDictionary)
    {
    }

    internal ThemeService(
        ISystemThemeSource systemTheme,
        Dispatcher? dispatcher,
        Action<EffectiveManagerTheme> applyTheme)
    {
        _systemTheme = systemTheme;
        _dispatcher = dispatcher;
        _applyTheme = applyTheme;
        _systemTheme.Changed += HandleSystemThemeChanged;
    }

    public ManagerThemeMode Mode { get; private set; } = ManagerThemeMode.System;
    public EffectiveManagerTheme EffectiveTheme { get; private set; } =
        EffectiveManagerTheme.Dark;

    public event EventHandler? ThemeChanged;

    public void ApplyMode(ManagerThemeMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Mode = mode;
        ApplyResolvedTheme();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _systemTheme.Changed -= HandleSystemThemeChanged;
        _systemTheme.Dispose();
    }

    private void HandleSystemThemeChanged(object? sender, EventArgs eventArgs)
    {
        if (!_disposed && Mode == ManagerThemeMode.System)
        {
            ApplyResolvedTheme();
        }
    }

    private void ApplyResolvedTheme()
    {
        var effective = Mode switch
        {
            ManagerThemeMode.Dark => EffectiveManagerTheme.Dark,
            ManagerThemeMode.Light => EffectiveManagerTheme.Light,
            _ => ResolveSystemTheme()
        };

        void Apply()
        {
            _applyTheme(effective);
            var changed = EffectiveTheme != effective;
            EffectiveTheme = effective;
            if (changed)
            {
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        if (_dispatcher is not null && !_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    private EffectiveManagerTheme ResolveSystemTheme()
    {
        try
        {
            var snapshot = _systemTheme.Read();
            return snapshot.IsAvailable
                ? snapshot.Theme
                : EffectiveManagerTheme.Dark;
        }
        catch (Exception exception) when (
            exception is IOException or
                InvalidOperationException or
                UnauthorizedAccessException or
                System.Security.SecurityException)
        {
            return EffectiveManagerTheme.Dark;
        }
    }

    private static void ApplyResourceDictionary(EffectiveManagerTheme theme)
    {
        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }
        var dictionaries = resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(
            dictionary => dictionary.Source?.OriginalString.StartsWith(
                ThemePrefix,
                StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary
        {
            Source = new Uri(
                $"Themes/Theme.{theme}.xaml",
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
    }
}

internal sealed class WindowsSystemThemeSource : ISystemThemeSource
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private bool _disposed;
    private bool _subscribed;

    public WindowsSystemThemeSource()
    {
        try
        {
            SystemEvents.UserPreferenceChanged += HandleUserPreferenceChanged;
            _subscribed = true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                System.Runtime.InteropServices.ExternalException or
                System.Security.SecurityException)
        {
            // Registry polling through Read() still works; live updates are unavailable.
        }
    }

    public event EventHandler? Changed;

    public SystemThemeSnapshot Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                PersonalizeKey,
                writable: false);
            var value = key?.GetValue(
                "AppsUseLightTheme",
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value is int number)
            {
                return new SystemThemeSnapshot(
                    true,
                    number != 0
                        ? EffectiveManagerTheme.Light
                        : EffectiveManagerTheme.Dark,
                    SystemParameters.HighContrast);
            }
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                System.Security.SecurityException)
        {
            // The caller applies a stable dark fallback.
        }
        return new SystemThemeSnapshot(
            false,
            EffectiveManagerTheme.Dark,
            SystemParameters.HighContrast);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_subscribed)
        {
            SystemEvents.UserPreferenceChanged -= HandleUserPreferenceChanged;
            _subscribed = false;
        }
    }

    private void HandleUserPreferenceChanged(
        object sender,
        UserPreferenceChangedEventArgs eventArgs)
    {
        if (!_disposed &&
            eventArgs.Category is UserPreferenceCategory.General or
                UserPreferenceCategory.Color or
                UserPreferenceCategory.VisualStyle or
                UserPreferenceCategory.Accessibility)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
