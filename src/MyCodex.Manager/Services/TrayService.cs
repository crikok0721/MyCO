using System.ComponentModel;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using MyCodex.Manager.Localization;
using MyCodex.Manager.ViewModels;
using MyCodex.Manager.Views;

namespace MyCodex.Manager.Services;

// App-owned notification icon. It survives window hiding and is disposed exactly once on exit.
internal sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly MainWindow _window;
    private readonly MainWindowViewModel _viewModel;
    private readonly ThemeService _themeService;
    private readonly Forms.ToolStripMenuItem _openItem;
    private readonly Forms.ToolStripMenuItem _startItem;
    private readonly Forms.ToolStripMenuItem _restartItem;
    private readonly Forms.ToolStripMenuItem _skinItem;
    private readonly Forms.ToolStripMenuItem _settingsItem;
    private readonly Forms.ToolStripMenuItem _exitItem;
    private bool _disposed;

    public TrayService(
        MainWindow window,
        MainWindowViewModel viewModel,
        ThemeService themeService)
    {
        _window = window;
        _viewModel = viewModel;
        _themeService = themeService;
        _menu = new Forms.ContextMenuStrip();
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

        _icon = new Forms.NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "MyCodex",
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
        _window.Dispatcher.Invoke(RefreshText);

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
        _menu.BackColor = Drawing.ColorTranslator.FromHtml(dark ? "#1B1D21" : "#FFFFFF");
        _menu.ForeColor = Drawing.ColorTranslator.FromHtml(dark ? "#F3F4F6" : "#202124");
        _menu.RenderMode = Forms.ToolStripRenderMode.System;
    }
}

internal static class TrayIconFactory
{
    public static Drawing.Icon Create()
    {
        var resource = System.Windows.Application.GetResourceStream(
                           new Uri(
                               "pack://application:,,,/Assets/mycodex.ico",
                               UriKind.Absolute))
                       ?? throw new InvalidOperationException(
                           "The embedded MyCodex icon is unavailable.");
        using (resource.Stream)
        using (var icon = new Drawing.Icon(resource.Stream))
        {
            return (Drawing.Icon)icon.Clone();
        }
    }
}
