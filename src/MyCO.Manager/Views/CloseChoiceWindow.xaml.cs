using System.Windows;

namespace MyCO.Manager.Views;

public enum CloseChoice
{
    Cancel,
    Exit,
    MinimizeToTray
}

public partial class CloseChoiceWindow : Window
{
    public CloseChoiceWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => MinimizeButton.Focus();
    }

    public CloseChoice Choice { get; private set; } = CloseChoice.Cancel;

    private void Exit_Click(object sender, RoutedEventArgs eventArgs)
    {
        Choice = CloseChoice.Exit;
        DialogResult = true;
    }

    private void Minimize_Click(object sender, RoutedEventArgs eventArgs)
    {
        Choice = CloseChoice.MinimizeToTray;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs)
    {
        Choice = CloseChoice.Cancel;
        DialogResult = false;
    }
}
