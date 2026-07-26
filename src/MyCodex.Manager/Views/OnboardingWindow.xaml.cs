using System.Windows;
using MyCodex.Manager.ViewModels;

// First-run dialog; it shares the main view model so language changes persist immediately.
namespace MyCodex.Manager.Views;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Continue_Click(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = true;
    }
}
