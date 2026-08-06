using System.IO;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using MyCO.Startup;

namespace MyCO.Manager.Services;

// Native Windows toast content keeps the large app-logo override used by the
// reference notification. NotifyIcon remains the compatibility fallback.
internal static class TrayToastNotificationService
{
    private const string RelativeInfoIconPath =
        "Assets\\MyCO-notification-info.png";

    public static string BuildToastXml(
        string title,
        string body,
        string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var document = new XmlDocument();
        document.LoadXml(
            "<toast><visual><binding template=\"ToastGeneric\" />" +
            "</visual></toast>");
        var binding = (XmlElement)document.GetElementsByTagName("binding")[0];
        AppendText(document, binding, title);
        AppendText(document, binding, body);

        var image = document.CreateElement("image");
        image.SetAttribute("placement", "appLogoOverride");
        image.SetAttribute("hint-crop", "circle");
        image.SetAttribute(
            "src",
            new System.Uri(
                Path.GetFullPath(imagePath),
                System.UriKind.Absolute).AbsoluteUri);
        binding.AppendChild(image);
        return document.GetXml();
    }

    public static bool TryShow(string title, string body)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return false;
        }

        var imagePath = Path.Combine(
            AppContext.BaseDirectory,
            RelativeInfoIconPath);
        if (!File.Exists(imagePath))
        {
            return false;
        }

        try
        {
            var document = new XmlDocument();
            document.LoadXml(BuildToastXml(title, body, imagePath));
            var toast = new ToastNotification(document);
            ToastNotificationManager.CreateToastNotifier(
                    CodexLaunchAssociationService.AppUserModelId)
                .Show(toast);
            return true;
        }
        catch (Exception)
        {
            // Missing AUMID/Toast service/notification permission falls back to
            // the existing NotifyIcon route without consuming another claim.
            return false;
        }
    }

    private static void AppendText(
        XmlDocument document,
        XmlElement binding,
        string value)
    {
        var text = document.CreateElement("text");
        text.AppendChild(document.CreateTextNode(value));
        binding.AppendChild(text);
    }
}
