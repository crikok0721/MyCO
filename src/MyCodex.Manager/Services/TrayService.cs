using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using MyCodex.Manager.ViewModels;
using MyCodex.Manager.Views;

namespace MyCodex.Manager.Services;

internal sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly MainWindow _window;
    private readonly MainWindowViewModel _viewModel;

    public TrayService(MainWindow window, MainWindowViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open MyCodex", null, (_, _) => ShowWindow());
        menu.Items.Add("Start Codex", null, (_, _) => _viewModel.StartCommand.Execute(null));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Enable Skin", null, (_, _) => _viewModel.EnableCommand.Execute(null));
        menu.Items.Add("Disable Skin", null, (_, _) => _viewModel.DisableCommand.Execute(null));
        menu.Items.Add("Recalibrate", null, (_, _) =>
        {
            _viewModel.CurrentPage = ManagerPage.Calibration;
            ShowWindow();
        });
        menu.Items.Add("Diagnostics", null, (_, _) =>
        {
            _viewModel.RefreshDiagnosticsCommand.Execute(null);
            ShowWindow();
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _window.Dispatcher.Invoke(_window.Close));

        _icon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "MyCodex",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ShowWindow();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    private void ShowWindow()
    {
        _window.Dispatcher.Invoke(() =>
        {
            if (!_window.IsVisible)
            {
                _window.Show();
            }
            if (_window.WindowState == System.Windows.WindowState.Minimized)
            {
                _window.WindowState = System.Windows.WindowState.Normal;
            }
            _window.Activate();
            _window.Topmost = true;
            _window.Topmost = false;
            _window.Focus();
        });
    }
}
