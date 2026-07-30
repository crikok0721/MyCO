using System.Text.Json;

// Writes local JSONL diagnostics with an allow-list and basic secret redaction.
namespace MyCO.Diagnostics;

public interface IPrivacySafeLogger
{
    void Info(string eventName, IReadOnlyDictionary<string, object?>? properties = null);
    void Error(string eventName, Exception exception);
}

public sealed class PrivacySafeLogger : IPrivacySafeLogger
{
    private const long MaximumFileBytes = 2 * 1024 * 1024;
    private const long MaximumTotalBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedPropertyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "appVersion",
            "adapter",
            "cdpPort",
            "targetCount",
            "runtimeVersion",
            "compatibility",
            "matchCount",
            "confidence",
            "state",
            "candidateCount",
            "eligibleCount",
            "conversationTargets",
            "visibleTargets",
            "attempt",
            "stage",
            "outcome"
        };

    private readonly string _logsDirectory;
    private readonly object _gate = new();

    public PrivacySafeLogger(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);
        _logsDirectory = logsDirectory;
        Prune();
    }

    public void Info(
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        // Unknown property names are dropped so callers cannot accidentally log chat data.
        var safeProperties = properties?
            .Where(pair => AllowedPropertyNames.Contains(pair.Key))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value is string value ? Sanitize(value) : pair.Value);
        Write(new
        {
            at = DateTimeOffset.UtcNow,
            level = "info",
            @event = Sanitize(eventName),
            properties = safeProperties
        });
    }

    public void Error(string eventName, Exception exception)
    {
        // Exception messages can contain renderer text or local paths. The event
        // code and exception type are sufficient for public diagnostics.
        Write(new
        {
            at = DateTimeOffset.UtcNow,
            level = "error",
            @event = Sanitize(eventName),
            errorType = exception.GetType().Name
        });
    }

    private void Write(object record)
    {
        var line = JsonSerializer.Serialize(record) + Environment.NewLine;
        lock (_gate)
        {
            try
            {
                var logFile = CurrentLogFile();
                File.AppendAllText(logFile, line);
                Prune();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // Logging is best effort and must never become a crash source.
            }
        }
    }

    private static string Sanitize(string value)
    {
        // Redaction is defense in depth; callers should still pass only technical metadata.
        var result = value;
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?i)(authorization|cookie|token|password)\s*[:=]\s*\S+",
            "$1=[redacted]");
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}",
            "[redacted-email]");
        foreach (var (path, replacement) in new[]
                 {
                     (Environment.GetFolderPath(
                         Environment.SpecialFolder.ApplicationData), "%APPDATA%"),
                     (Environment.GetFolderPath(
                         Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
                     (Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), "%TEMP%"),
                     (Environment.GetFolderPath(
                         Environment.SpecialFolder.UserProfile), "[user-profile]")
                 })
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                result = result.Replace(
                    path,
                    replacement,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?i)(?:[a-z]:\\|\\\\)[^\r\n""']+",
            "[path-redacted]");
        return result.Length <= 2000 ? result : result[..2000];
    }

    private string CurrentLogFile()
    {
        var prefix = $"myco-{DateTimeOffset.Now:yyyyMMdd}";
        for (var index = 0; index < 100; index++)
        {
            var suffix = index == 0 ? string.Empty : $"-{index}";
            var path = Path.Combine(_logsDirectory, $"{prefix}{suffix}.jsonl");
            if (!File.Exists(path) || new FileInfo(path).Length < MaximumFileBytes)
            {
                return path;
            }
        }
        return Path.Combine(_logsDirectory, $"{prefix}-overflow.jsonl");
    }

    private void Prune()
    {
        try
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(7);
            var files = new DirectoryInfo(_logsDirectory)
                .EnumerateFiles("myco-*.jsonl")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();
            foreach (var expired in files
                         .Where(file => file.LastWriteTimeUtc < cutoff.UtcDateTime)
                         .ToArray())
            {
                expired.Delete();
                files.Remove(expired);
            }

            long total = files.Sum(file => file.Exists ? file.Length : 0);
            foreach (var file in files.OrderBy(file => file.LastWriteTimeUtc))
            {
                if (total <= MaximumTotalBytes)
                {
                    break;
                }
                total -= file.Length;
                file.Delete();
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup is best effort.
        }
    }
}
