using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MyCodex.Applications;

public sealed class WindowsApplicationLocator : IApplicationLocator
{
    private const string PackageRepository =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    public Task<IReadOnlyList<ApplicationCandidate>> FindCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = new Dictionary<string, ApplicationCandidate>(
            StringComparer.OrdinalIgnoreCase);

        AddRunningCandidates(candidates);
        AddMsixCandidates(candidates);
        AddWin32Candidates(candidates);

        return Task.FromResult<IReadOnlyList<ApplicationCandidate>>(
            ApplicationCandidateResolver.CollapseVersions(candidates.Values));
    }

    private static void AddRunningCandidates(
        IDictionary<string, ApplicationCandidate> candidates)
    {
        foreach (var processName in new[] { "ChatGPT", "Codex" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    string? path = null;
                    string? title = null;
                    string? version = null;

                    try
                    {
                        path = process.MainModule?.FileName;
                        version = process.MainModule?.FileVersionInfo.ProductVersion;
                        title = string.IsNullOrWhiteSpace(process.MainWindowTitle)
                            ? null
                            : process.MainWindowTitle;
                    }
                    catch (Exception)
                    {
                        // A protected child process is not an application candidate.
                    }

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }
                    if (processName.Equals("Codex", StringComparison.OrdinalIgnoreCase) &&
                        path.Contains(
                            $"{Path.DirectorySeparatorChar}resources{Path.DirectorySeparatorChar}",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var key = Path.GetFullPath(path);
                    var score = ScoreCandidate(processName, path, null, true);
                    candidates[key] = new ApplicationCandidate(
                        processName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase)
                            ? "ChatGPT / Codex"
                            : "Legacy Codex Desktop",
                        processName,
                        key,
                        key,
                        ExtractPackageIdentity(path),
                        version,
                        title,
                        RuntimeInformation.ProcessArchitecture.ToString(),
                        ApplicationLaunchMethod.Executable,
                        score,
                        true);
                }
            }
        }
    }

    private static void AddMsixCandidates(
        IDictionary<string, ApplicationCandidate> candidates)
    {
        using var repository = Registry.CurrentUser.OpenSubKey(PackageRepository);
        if (repository is null)
        {
            return;
        }

        foreach (var packageName in repository.GetSubKeyNames())
        {
            if (!packageName.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) &&
                !packageName.Contains("Codex", StringComparison.OrdinalIgnoreCase) &&
                !packageName.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var package = repository.OpenSubKey(packageName);
            var packageRoot = package?.GetValue("PackageRootFolder") as string;
            if (string.IsNullOrWhiteSpace(packageRoot) || !Directory.Exists(packageRoot))
            {
                continue;
            }

            var executable = FindDesktopExecutable(packageRoot);
            if (executable is null)
            {
                continue;
            }

            var displayName = package?.GetValue("DisplayName") as string;
            var key = Path.GetFullPath(executable);
            var running = candidates.TryGetValue(key, out var existing) && existing.IsRunning;
            var version = ParsePackageVersion(packageName);
            var processName = Path.GetFileNameWithoutExtension(executable);

            candidates[key] = new ApplicationCandidate(
                string.IsNullOrWhiteSpace(displayName) ? "ChatGPT / Codex" : displayName,
                processName,
                key,
                "shell:AppsFolder\\" + PackageFamilyName(packageName) + "!App",
                packageName,
                version,
                existing?.WindowTitle,
                ParseArchitecture(packageName),
                ApplicationLaunchMethod.Executable,
                ScoreCandidate(processName, executable, packageName, running),
                running);
        }
    }

    private static void AddWin32Candidates(
        IDictionary<string, ApplicationCandidate> candidates)
    {
        var roots = new[]
        {
            (RegistryHive.CurrentUser, RegistryView.Default),
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32)
        };

        foreach (var (hive, view) in roots)
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null)
            {
                continue;
            }

            foreach (var childName in uninstall.GetSubKeyNames())
            {
                using var child = uninstall.OpenSubKey(childName);
                var displayName = child?.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName) ||
                    (!displayName.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase) &&
                     !displayName.Contains("Codex", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var installLocation = child?.GetValue("InstallLocation") as string;
                var executable = FindDesktopExecutable(installLocation);
                if (executable is null)
                {
                    continue;
                }

                var key = Path.GetFullPath(executable);
                if (candidates.ContainsKey(key))
                {
                    continue;
                }

                var processName = Path.GetFileNameWithoutExtension(executable);
                candidates[key] = new ApplicationCandidate(
                    displayName,
                    processName,
                    key,
                    key,
                    null,
                    child?.GetValue("DisplayVersion") as string,
                    null,
                    RuntimeInformation.OSArchitecture.ToString(),
                    ApplicationLaunchMethod.Executable,
                    ScoreCandidate(processName, executable, null, false),
                    false);
            }
        }
    }

    private static string? FindDesktopExecutable(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var preferred = new[]
        {
            Path.Combine(directory, "app", "ChatGPT.exe"),
            Path.Combine(directory, "ChatGPT.exe"),
            Path.Combine(directory, "Codex.exe")
        };

        return preferred.FirstOrDefault(File.Exists);
    }

    private static int ScoreCandidate(
        string processName,
        string executable,
        string? packageIdentity,
        bool running)
    {
        var score = 0;
        score += processName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) ? 30 : 22;
        score += executable.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) ? 25 : 0;
        score += executable.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase) ? 15 : 0;
        score += packageIdentity?.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) == true
            ? 25
            : 0;
        score += running ? 5 : 0;
        return score;
    }

    private static string? ExtractPackageIdentity(string path)
    {
        return path.Split(Path.DirectorySeparatorChar)
            .FirstOrDefault(part =>
                part.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ParsePackageVersion(string packageName)
    {
        var parts = packageName.Split('_');
        return parts.Length > 1 ? parts[1] : null;
    }

    private static string ParseArchitecture(string packageName)
    {
        var parts = packageName.Split('_');
        return parts.Length > 2 ? parts[2] : RuntimeInformation.OSArchitecture.ToString();
    }

    private static string PackageFamilyName(string packageName)
    {
        var parts = packageName.Split('_');
        return parts.Length >= 5 ? $"{parts[0]}_{parts[^1]}" : packageName;
    }
}
