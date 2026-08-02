using System.ComponentModel;
using Drawing = System.Drawing;
using DrawingText = System.Drawing.Text;
using Forms = System.Windows.Forms;
using MyCO.Manager.Localization;
using MyCO.Manager.ViewModels;
using MyCO.Manager.Views;

namespace MyCO.Manager.Services;

// App-owned notification icon. It survives window hiding and is disposed exactly once on exit.
internal sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly TrayMenuRenderer _menuRenderer;
    private readonly MainWindow _window;
    private readonly MainWindowViewModel _viewModel;
    private readonly ThemeService _themeService;
    private readonly Forms.ToolStripMenuItem _openItem;
    private readonly Forms.ToolStripMenuItem _startItem;
    private readonly Forms.ToolStripMenuItem _restartItem;
    private readonly Forms.ToolStripMenuItem _skinItem;
    private readonly Forms.ToolStripMenuItem _settingsItem;
    private readonly Forms.ToolStripMenuItem _exitItem;
    private Drawing.Font? _menuFont;
    private bool _disposed;

    public TrayService(
        MainWindow window,
        MainWindowViewModel viewModel,
        ThemeService themeService)
    {
        _window = window;
        _viewModel = viewModel;
        _themeService = themeService;
        _menuRenderer = new TrayMenuRenderer();
        _menu = new TrayContextMenuStrip(_menuRenderer);
        _openItem = new Forms.ToolStripMenuItem();
        _startItem = new Forms.ToolStripMenuItem();
        _restartItem = new Forms.ToolStripMenuItem();
        _skinItem = new Forms.ToolStripMenuItem();
        _settingsItem = new Forms.ToolStripMenuItem();
        _exitItem = new Forms.ToolStripMenuItem();

        _openItem.Click += (_, _) => ShowWindow();
        _startItem.Click += (_, _) =>
            _window.Dispatcher.Invoke(() => _viewModel.StartCommand.Execute(null));
        _restartItem.Click += (_, _) =>
            _window.Dispatcher.Invoke(() => _viewModel.RestartCommand.Execute(null));
        _skinItem.Click += (_, _) => _window.Dispatcher.Invoke(ToggleSkin);
        _settingsItem.Click += (_, _) => _window.Dispatcher.Invoke(() =>
        {
            _viewModel.CurrentPage = ManagerPage.Settings;
            ShowWindow();
        });
        _exitItem.Click += (_, _) => _window.Dispatcher.Invoke(_window.RequestExit);
        _menu.Items.AddRange(
        [
            _openItem,
            _startItem,
            _restartItem,
            new Forms.ToolStripSeparator(),
            _skinItem,
            _settingsItem,
            new Forms.ToolStripSeparator(),
            _exitItem
        ]);
        _menu.Opening += (_, _) => _menuRenderer.ApplyLayout(_menu);

        _icon = new Forms.NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "MyCO",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _icon.DoubleClick += HandleDoubleClick;
        _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        _viewModel.StartCommand.CanExecuteChanged += HandleCommandCanExecuteChanged;
        _viewModel.RestartCommand.CanExecuteChanged += HandleCommandCanExecuteChanged;
        LocalizationService.LanguageChanged += HandleLanguageChanged;
        _themeService.ThemeChanged += HandleThemeChanged;
        RefreshText();
        ApplyFont();
        ApplyTheme();
    }

    public void ShowWindow()
    {
        if (_disposed)
        {
            return;
        }
        _window.Dispatcher.Invoke(_window.RestoreFromTray);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        _viewModel.StartCommand.CanExecuteChanged -= HandleCommandCanExecuteChanged;
        _viewModel.RestartCommand.CanExecuteChanged -= HandleCommandCanExecuteChanged;
        LocalizationService.LanguageChanged -= HandleLanguageChanged;
        _themeService.ThemeChanged -= HandleThemeChanged;
        _icon.DoubleClick -= HandleDoubleClick;
        _icon.Visible = false;
        _menuFont?.Dispose();
        _menuFont = null;
        _menu.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }

    private void HandleDoubleClick(object? sender, EventArgs eventArgs) => ShowWindow();

    private void ToggleSkin()
    {
        var command = _viewModel.IsSkinRequested
            ? _viewModel.DisableCommand
            : _viewModel.EnableCommand;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void HandleViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(MainWindowViewModel.SessionState) or
            nameof(MainWindowViewModel.IsSkinRequested))
        {
            _window.Dispatcher.Invoke(RefreshText);
        }
    }

    private void HandleLanguageChanged(object? sender, EventArgs eventArgs) =>
        _window.Dispatcher.Invoke(() =>
        {
            RefreshText();
            ApplyFont();
        });

    private void HandleCommandCanExecuteChanged(object? sender, EventArgs eventArgs) =>
        _window.Dispatcher.Invoke(RefreshText);

    private void HandleThemeChanged(object? sender, EventArgs eventArgs) =>
        _window.Dispatcher.Invoke(ApplyTheme);

    private void RefreshText()
    {
        _openItem.Text = LocalizationService.Get("TrayOpen");
        _startItem.Text = LocalizationService.Get("TrayStartCodex");
        _restartItem.Text = LocalizationService.Get("TrayRestartCodex");
        _skinItem.Text = LocalizationService.Get(
            _viewModel.IsSkinRequested ? "TrayDisableSkin" : "TrayEnableSkin");
        _settingsItem.Text = LocalizationService.Get("TraySettings");
        _exitItem.Text = LocalizationService.Get("TrayExit");
        _startItem.Enabled = _viewModel.StartCommand.CanExecute(null);
        _restartItem.Enabled = _viewModel.RestartCommand.CanExecute(null);
        _skinItem.Enabled =
            (_viewModel.IsSkinRequested
                ? _viewModel.DisableCommand
                : _viewModel.EnableCommand).CanExecute(null);
    }

    private void ApplyTheme()
    {
        var dark = _themeService.EffectiveTheme == EffectiveManagerTheme.Dark;
        _menuRenderer.ApplyTheme(_menu, dark);
        _menuRenderer.ApplyLayout(_menu);
    }

    private void ApplyFont()
    {
        var profile = LocaleFontCatalog.For(LocalizationService.CurrentLocale);
        var systemFont = Drawing.SystemFonts.MessageBoxFont!;
        var familyName = FindInstalledFamily(profile.PreferredTrayFamilies)
                         ?? systemFont.FontFamily.Name;
        Drawing.Font next;
        try
        {
            next = new Drawing.Font(
                familyName,
                systemFont.Size,
                systemFont.Style,
                systemFont.Unit,
                systemFont.GdiCharSet,
                systemFont.GdiVerticalFont);
        }
        catch (ArgumentException)
        {
            next = (Drawing.Font)systemFont.Clone();
        }

        var previous = _menuFont;
        _menuFont = next;
        _menu.Font = next;
        previous?.Dispose();
    }

    private static string? FindInstalledFamily(
        IReadOnlyList<string> preferredFamilies)
    {
        using var installed = new DrawingText.InstalledFontCollection();
        var families = installed.Families;
        return preferredFamilies.FirstOrDefault(preferred =>
            families.Any(installedFamily =>
                string.Equals(
                    installedFamily.Name,
                    preferred,
                    StringComparison.OrdinalIgnoreCase)));
    }
}

internal static class TrayIconFactory
{
    public static Drawing.Icon Create()
    {
        var resource = System.Windows.Application.GetResourceStream(
                           new Uri(
                               "pack://application:,,,/Assets/MyCO.ico",
                               UriKind.Absolute))
                       ?? throw new InvalidOperationException(
                           "The embedded MyCO icon is unavailable.");
        using (resource.Stream)
        using (var icon = new Drawing.Icon(resource.Stream))
        {
            return (Drawing.Icon)icon.Clone();
        }
    }
}
