namespace MyCO.Manager.Services;

internal enum TrayWindowPresentation
{
    Visible,
    HiddenToTray
}

// Pure state transition used by the WPF shell and covered without automating a real window.
internal sealed class TrayWindowStateMachine
{
    public TrayWindowPresentation State { get; private set; } =
        TrayWindowPresentation.Visible;

    public bool Hide()
    {
        if (State == TrayWindowPresentation.HiddenToTray)
        {
            return false;
        }
        State = TrayWindowPresentation.HiddenToTray;
        return true;
    }

    public bool Restore()
    {
        if (State == TrayWindowPresentation.Visible)
        {
            return false;
        }
        State = TrayWindowPresentation.Visible;
        return true;
    }
}

internal static class StartupPresentation
{
    public static bool StartsInBackground(IEnumerable<string> arguments) =>
        arguments.Any(argument => argument.Equals(
            "--background",
            StringComparison.OrdinalIgnoreCase));
}
