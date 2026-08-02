using System.Windows;

namespace MyCO.Manager.Views;

public partial class ResetConfirmationWindow : Window
{
    public ResetConfirmationWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => CancelButton.Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = false;
    }
}
