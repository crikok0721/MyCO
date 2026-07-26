namespace MyCodex.Applications;

public enum ApplicationLaunchMethod
{
    Executable,
    PackageActivation
}

public sealed record ApplicationCandidate(
    string DisplayName,
    string ProcessName,
    string? ExecutablePath,
    string? LaunchTarget,
    string? PackageIdentity,
    string? Version,
    string? WindowTitle,
    string Architecture,
    ApplicationLaunchMethod LaunchMethod,
    int Score,
    bool IsRunning);
