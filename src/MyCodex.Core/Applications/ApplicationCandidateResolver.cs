namespace MyCodex.Applications;

public static class ApplicationCandidateResolver
{
    public static ApplicationCandidate? ResolveCurrent(
        ApplicationCandidate previous,
        IEnumerable<ApplicationCandidate> currentCandidates)
    {
        var candidates = currentCandidates.ToArray();
        var stableKey = StableKey(previous);
        var stableMatches = candidates
            .Where(candidate => string.Equals(
                StableKey(candidate),
                stableKey,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (stableMatches.Length > 0)
        {
            return Best(stableMatches);
        }

        var processMatches = candidates
            .Where(candidate => candidate.ProcessName.Equals(
                previous.ProcessName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return processMatches.Length > 0 ? Best(processMatches) : null;
    }

    public static IReadOnlyList<ApplicationCandidate> CollapseVersions(
        IEnumerable<ApplicationCandidate> candidates)
    {
        return candidates
            .GroupBy(StableKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => Best(group))
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.IsRunning)
            .ThenByDescending(candidate => ParseVersion(candidate.Version))
            .ThenBy(
                candidate => candidate.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string StableKey(ApplicationCandidate candidate)
    {
        if (candidate.LaunchTarget?.StartsWith(
                "shell:AppsFolder\\",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return $"package:{candidate.LaunchTarget}";
        }
        if (!string.IsNullOrWhiteSpace(candidate.PackageIdentity))
        {
            var parts = candidate.PackageIdentity.Split('_');
            if (parts.Length >= 5)
            {
                return $"package:{parts[0]}_{parts[^1]}";
            }
        }
        if (!string.IsNullOrWhiteSpace(candidate.ExecutablePath))
        {
            return $"executable:{Path.GetFullPath(candidate.ExecutablePath)}";
        }
        return $"process:{candidate.ProcessName}";
    }

    private static ApplicationCandidate Best(
        IEnumerable<ApplicationCandidate> candidates)
    {
        return candidates
            .OrderByDescending(candidate =>
                !string.IsNullOrWhiteSpace(candidate.ExecutablePath) &&
                File.Exists(candidate.ExecutablePath))
            .ThenByDescending(candidate => candidate.IsRunning)
            .ThenByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => ParseVersion(candidate.Version))
            .First();
    }

    private static Version ParseVersion(string? value)
    {
        return Version.TryParse(value, out var version)
            ? version
            : new Version(0, 0);
    }
}
