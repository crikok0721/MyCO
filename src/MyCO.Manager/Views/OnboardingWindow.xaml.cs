using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using MyCO.Manager.ViewModels;

// First-run dialog; it shares the main view model so language changes persist immediately.
namespace MyCO.Manager.Views;

public partial class OnboardingWindow : Window
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerRound = 2;

    public OnboardingWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Continue_Click(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = false;
    }

    protected override void OnSourceInitialized(EventArgs eventArgs)
    {
        base.OnSourceInitialized(eventArgs);
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
            // Older Windows builds keep the square fallback.
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows builds keep the square fallback.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int value,
        int valueSize);
}
