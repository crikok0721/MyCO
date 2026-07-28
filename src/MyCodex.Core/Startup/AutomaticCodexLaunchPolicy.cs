namespace MyCodex.Startup;

public enum AutomaticCodexLaunchDecision
{
    Disabled,
    DesktopNotFound,
    AlreadyControlled,
    AlreadyRunningUncontrolled,
    Start
}

public static class AutomaticCodexLaunchPolicy
{
    public static AutomaticCodexLaunchDecision Decide(
        bool enabled,
        bool candidateFound,
        bool desktopIsRunning,
        bool sessionIsConnected)
    {
        if (!enabled)
        {
            return AutomaticCodexLaunchDecision.Disabled;
        }
        if (!candidateFound)
        {
            return AutomaticCodexLaunchDecision.DesktopNotFound;
        }
        if (sessionIsConnected)
        {
            return AutomaticCodexLaunchDecision.AlreadyControlled;
        }
        if (desktopIsRunning)
        {
            return AutomaticCodexLaunchDecision.AlreadyRunningUncontrolled;
        }
        return AutomaticCodexLaunchDecision.Start;
    }
}
