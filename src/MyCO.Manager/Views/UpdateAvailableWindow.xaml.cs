using System.Windows;
using MyCO.Manager.Localization;
using MyCO.Updates;

namespace MyCO.Manager.Views;

public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow(OfficialRelease release)
    {
        InitializeComponent();
        VersionText = release.Version.ToString();
        DescriptionText = LocalizationService.Format(
            "UpdateDialogDescriptionFormat",
            release.Version,
            release.Summary);
        DataContext = this;
        Loaded += (_, _) => LaterButtonFocus();
    }

    public string VersionText { get; }
    public string DescriptionText { get; }
    public bool Confirmed { get; private set; }

    private void LaterButtonFocus()
    {
        LaterButton.Focus();
    }

    private void Close_Click(object sender, RoutedEventArgs eventArgs) => Later_Click(sender, eventArgs);

    private void Later_Click(object sender, RoutedEventArgs eventArgs)
    {
        Confirmed = false;
        DialogResult = false;
    }

    private void UpdateNow_Click(object sender, RoutedEventArgs eventArgs)
    {
        Confirmed = true;
        DialogResult = true;
    }
}
