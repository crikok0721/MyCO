using System.Diagnostics;
using System.Text.RegularExpressions;

// Development-only safety primitives for the isolated visual-acceptance tool.
namespace MyCodex.VisualAcceptance;

public sealed record VisualAcceptanceRunPaths(
    string RunId,
    string RootDirectory,
    string RunDirectory,
    string ProfileDirectory,
    string StateFile,
    string FinalStateFile)
{
    public static VisualAcceptanceRunPaths Create(string? runId = null)
    {
        var id = runId ?? Guid.NewGuid().ToString("N");
        if (!Regex.IsMatch(
                id,
                "^[a-f0-9]{32}$",
                RegexOptions.CultureInvariant))
        {
            throw new ArgumentException(
                "Visual acceptance run-id must be a 32-character lowercase hexadecimal value.");
        }

        var root = Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "MyCodex",
                "VisualAcceptance"));
        var run = Path.GetFullPath(Path.Combine(root, id));
        EnsureDescendant(root, run);
        return new VisualAcceptanceRunPaths(
            id,
            root,
            run,
            Path.Combine(run, "profile"),
            Path.Combine(run, "state.json"),
            Path.Combine(root, $"{id}.final.json"));
    }

    public void ValidateForRecursiveCleanup(string path)
    {
        var candidate = Path.GetFullPath(path);
        EnsureDescendant(RunDirectory, candidate, allowEqual: true);
    }

    private static void EnsureDescendant(
        string parent,
        string candidate,
        bool allowEqual = false)
    {
        var normalizedParent = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar);
        var normalizedCandidate = Path.GetFullPath(candidate)
            .TrimEnd(Path.DirectorySeparatorChar);
        if (allowEqual &&
            normalizedCandidate.Equals(
                normalizedParent,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (!normalizedCandidate.StartsWith(
                normalizedParent + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Visual acceptance path escaped its isolated run directory.");
        }
    }
}

public sealed record VisualAcceptanceProcessIdentity(
    string RunId,
    int ProcessId,
    string ExecutablePath,
    DateTimeOffset ProcessStartedAt,
    DateTimeOffset RunStartedAt,
    string ProfilePath);

public static class VisualAcceptanceProcessGuard
{
    public static bool IsExactOwnedTarget(
        VisualAcceptanceProcessIdentity expected,
        VisualAcceptanceProcessIdentity actual,
        VisualAcceptanceRunPaths paths)
    {
        return expected.RunId == paths.RunId &&
               actual.RunId == paths.RunId &&
               expected.ProcessId == actual.ProcessId &&
               expected.ExecutablePath.Equals(
                   actual.ExecutablePath,
                   StringComparison.OrdinalIgnoreCase) &&
               Path.GetFullPath(expected.ProfilePath).Equals(
                   Path.GetFullPath(paths.ProfileDirectory),
                   StringComparison.OrdinalIgnoreCase) &&
               Path.GetFullPath(actual.ProfilePath).Equals(
                   Path.GetFullPath(paths.ProfileDirectory),
                   StringComparison.OrdinalIgnoreCase) &&
               actual.ProcessStartedAt >= expected.RunStartedAt &&
               actual.ProcessStartedAt == expected.ProcessStartedAt;
    }

    public static VisualAcceptanceProcessIdentity Snapshot(
        Process process,
        string runId,
        DateTimeOffset runStartedAt,
        string profilePath)
    {
        process.Refresh();
        var executable = process.MainModule?.FileName
                         ?? throw new InvalidOperationException(
                             "Target executable path is unavailable.");
        return new VisualAcceptanceProcessIdentity(
            runId,
            process.Id,
            Path.GetFullPath(executable),
            process.StartTime.ToUniversalTime(),
            runStartedAt,
            Path.GetFullPath(profilePath));
    }
}

public static class VisualAcceptanceLaunchArguments
{
    public static IReadOnlyList<string> Create(string isolatedProfilePath)
    {
        var profile = Path.GetFullPath(isolatedProfilePath);
        return
        [
            "--remote-debugging-pipe",
            $"--user-data-dir={profile}",
            "--no-first-run",
            "--new-window"
        ];
    }
}

public static class VisualAcceptanceLifecycle
{
    private static readonly IReadOnlyDictionary<string, string[]> Allowed =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["host-starting"] = ["host-starting", "launching", "failed"],
            ["launching"] = ["launching", "ready", "failed"],
            ["ready"] = ["ready", "restarting", "disabled", "stopping", "failed"],
            ["restarting"] = ["restarting", "ready", "failed"],
            ["disabled"] = ["disabled", "stopping", "failed"],
            ["stopping"] = ["stopping", "stopped", "failed"],
            ["stopped"] = ["stopped", "cleaned"],
            ["cleaned"] = ["cleaned"],
            ["failed"] = ["failed", "restarting", "stopping"]
        };

    public static bool CanTransition(string current, string next) =>
        Allowed.TryGetValue(current, out var destinations) &&
        destinations.Contains(next, StringComparer.Ordinal);

    public static void EnsureTransition(string current, string next)
    {
        if (!CanTransition(current, next))
        {
            throw new InvalidOperationException(
                $"Invalid visual acceptance lifecycle transition: {current} -> {next}.");
        }
    }
}
