using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MyCodex.Manager.ViewModels;
using MyCodex.Manager.Services;

// Code-behind is limited to window chrome and orderly asynchronous shutdown.
namespace MyCodex.Manager.Views;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _shutdownInProgress;
    private bool _exitRequested;
    private readonly TrayWindowStateMachine _presentation = new();

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += HandleWindowStateChanged;
    }

    public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }
        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs eventArgs)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs eventArgs)
    {
        Close();
    }

    public void PrepareForBackground()
    {
        _presentation.Hide();
        ShowInTaskbar = false;
        WindowState = WindowState.Normal;
        Hide();
    }

    public void RestoreFromTray()
    {
        _presentation.Restore();
        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        if (!IsVisible)
        {
            Show();
        }
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
            return;
        }
        _presentation.Hide();
        ShowInTaskbar = false;
        Hide();
        WindowState = WindowState.Normal;
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
                $"MyCodex shutdown cleanup failed: {exception}");
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
