using System.Windows;
using MyCodex.Manager.ViewModels;

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
