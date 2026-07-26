using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MyCodex.Manager.ViewModels;

namespace MyCodex.Manager.Views;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _shutdownInProgress;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
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

    protected override async void OnClosing(CancelEventArgs eventArgs)
    {
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
