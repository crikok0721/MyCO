using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using MyCO.Manager.ViewModels;
using MyCO.Manager.Services;

// Code-behind is limited to window chrome and orderly asynchronous shutdown.
namespace MyCO.Manager.Views;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _shutdownInProgress;
    private bool _exitRequested;
    private readonly TrayWindowStateMachine _presentation = new();
    private WindowState _stateBeforeTray = WindowState.Normal;
    private WindowState _lastNonMinimizedState = WindowState.Normal;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += HandleWindowStateChanged;
    }

    public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void Minimize_Click(object sender, RoutedEventArgs eventArgs)
    {
        SystemCommands.MinimizeWindow(this);
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs eventArgs)
    {
        Close();
    }

    public void PrepareForBackground()
    {
        if (!_presentation.Hide())
        {
            return;
        }
        _stateBeforeTray = WindowState == WindowState.Minimized
            ? _lastNonMinimizedState
            : WindowState;
        ShowInTaskbar = false;
        Hide();
    }

    public void RestoreFromTray()
    {
        if (!_presentation.Restore() && IsVisible)
        {
            if (WindowState == WindowState.Minimized)
            {
                SystemCommands.RestoreWindow(this);
            }
            ActivateWindow();
            return;
        }
        ShowInTaskbar = true;
        if (!IsVisible)
        {
            Show();
        }
        WindowState = _stateBeforeTray;
        ActivateWindow();
    }

    private void ActivateWindow()
    {
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void RequestExit()
    {
        _exitRequested = true;
        Close();
    }

    private void HandleWindowStateChanged(object? sender, EventArgs eventArgs)
    {
        if (WindowState != WindowState.Minimized)
        {
            _lastNonMinimizedState = WindowState;
        }
        var maximized = WindowState == WindowState.Maximized;
        MaximizeGlyph.Visibility = maximized
            ? Visibility.Collapsed
            : Visibility.Visible;
        RestoreGlyph.Visibility = maximized
            ? Visibility.Visible
            : Visibility.Collapsed;
        MaximizeRestoreButton.SetResourceReference(
            System.Windows.Controls.ToolTipService.ToolTipProperty,
            maximized ? "RestoreWindow" : "MaximizeWindow");
    }

    protected override async void OnClosing(CancelEventArgs eventArgs)
    {
        // Delay the real close until CDP sessions and their background monitor are disposed.
        if (_allowClose)
        {
            base.OnClosing(eventArgs);
            return;
        }
        eventArgs.Cancel = true;
        if (_shutdownInProgress)
        {
            return;
        }
        if (!_exitRequested)
        {
            var choiceWindow = new CloseChoiceWindow { Owner = this };
            choiceWindow.ShowDialog();
            if (choiceWindow.Choice == CloseChoice.MinimizeToTray)
            {
                PrepareForBackground();
                return;
            }
            if (choiceWindow.Choice != CloseChoice.Exit)
            {
                return;
            }
            _exitRequested = true;
        }
        _shutdownInProgress = true;
        IsEnabled = false;
        try
        {
            await ViewModel.DisposeAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"MyCO shutdown cleanup failed: {exception}");
        }
        finally
        {
            _allowClose = true;
            await Dispatcher.InvokeAsync(
                Close,
                DispatcherPriority.ApplicationIdle);
        }
    }
}
