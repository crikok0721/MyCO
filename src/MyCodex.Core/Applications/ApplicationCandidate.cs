// Describes one desktop application that MyCodex can launch or attach to.
namespace MyCodex.Applications;

// The target may be started as a normal executable or through a packaged app id.
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
