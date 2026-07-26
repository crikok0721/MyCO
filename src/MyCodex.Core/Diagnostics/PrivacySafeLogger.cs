using System.Text.Json;

namespace MyCodex.Diagnostics;

public interface IPrivacySafeLogger
{
    void Info(string eventName, IReadOnlyDictionary<string, object?>? properties = null);
    void Error(string eventName, Exception exception);
}

public sealed class PrivacySafeLogger : IPrivacySafeLogger
{
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
            "state"
        };

    private readonly string _logFile;
    private readonly object _gate = new();

    public PrivacySafeLogger(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);
        _logFile = Path.Combine(logsDirectory, $"mycodex-{DateTimeOffset.Now:yyyyMMdd}.jsonl");
    }

    public void Info(
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        var safeProperties = properties?
            .Where(pair => AllowedPropertyNames.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
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
        Write(new
        {
            at = DateTimeOffset.UtcNow,
            level = "error",
            @event = Sanitize(eventName),
            errorType = exception.GetType().Name,
            message = Sanitize(exception.Message),
            stack = Sanitize(exception.StackTrace ?? string.Empty)
        });
    }

    private void Write(object record)
    {
        var line = JsonSerializer.Serialize(record) + Environment.NewLine;
        lock (_gate)
        {
            File.AppendAllText(_logFile, line);
        }
    }

    private static string Sanitize(string value)
    {
        var result = value;
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?i)(authorization|cookie|token|password)\s*[:=]\s*\S+",
            "$1=[redacted]");
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}",
            "[redacted-email]");
        return result.Length <= 2000 ? result : result[..2000];
    }
}
