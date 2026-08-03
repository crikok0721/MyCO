using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
    private bool _transitioningToTray;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerRound = 2;

    public event EventHandler? UserMinimizedToTray;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += HandleWindowStateChanged;
    }

    public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void Minimize_Click(object sender, RoutedEventArgs eventArgs)
    {
        PrepareForBackground(userInitiated: true);
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

    public void PrepareForBackground(bool userInitiated = false)
    {
        if (!_presentation.Hide())
        {
            return;
        }
        _stateBeforeTray = WindowState == WindowState.Minimized
            ? _lastNonMinimizedState
            : WindowState;
        _transitioningToTray = true;
        try
        {
            ShowInTaskbar = false;
            Hide();
        }
        finally
        {
            _transitioningToTray = false;
        }

        if (userInitiated && ViewModel.TryClaimTrayMinimizeNotification())
        {
            UserMinimizedToTray?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RestoreFromTray()
    {
        var restoredFromTray = _presentation.Restore();
        ShowInTaskbar = true;
        if (!IsVisible)
        {
            Show();
        }
        if (restoredFromTray || WindowState == WindowState.Minimized)
        {
            WindowState = _stateBeforeTray;
        }
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
        if (WindowState == WindowState.Minimized &&
            IsVisible &&
            !_transitioningToTray)
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    if (WindowState == WindowState.Minimized && IsVisible)
                    {
                        PrepareForBackground(userInitiated: true);
                    }
                },
                DispatcherPriority.Background);
        }

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
        if (maximized)
        {
            RootShell.CornerRadius = new CornerRadius(0);
        }
        else
        {
            RootShell.SetResourceReference(
                System.Windows.Controls.Border.CornerRadiusProperty,
                "RadiusWindow");
            RequestNativeRoundedCorners();
        }
    }

    protected override void OnSourceInitialized(EventArgs eventArgs)
    {
        base.OnSourceInitialized(eventArgs);
        RequestNativeRoundedCorners();
    }

    private void RequestNativeRoundedCorners()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        try
        {
            var preference = DwmWindowCornerRound;
            _ = DwmSetWindowAttribute(
                new WindowInteropHelper(this).Handle,
                DwmWindowCornerPreference,
                ref preference,
                Marshal.SizeOf<int>());
        }
        catch (DllNotFoundException)
        {
            // WindowChrome remains the compatibility fallback.
        }
        catch (EntryPointNotFoundException)
        {
            // WindowChrome remains the compatibility fallback.
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

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
                PrepareForBackground(userInitiated: true);
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
