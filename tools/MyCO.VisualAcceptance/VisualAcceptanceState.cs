using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyCO.VisualAcceptanceTool;

// Visual observations are explicit human/Computer Use results, not DOM checks.
public sealed record VisualCheckResult(
    string Result,
    string Note,
    DateTimeOffset At);

public sealed record VisualAcceptanceState
{
    // This file is the audit trail shared by Start/Restart/Status/Disable/Stop.
    public string RunId { get; init; } = string.Empty;
    public string Phase { get; init; } = "created";
    public int? ControllerPid { get; init; }
    public int? TargetPid { get; init; }
    public DateTimeOffset? TargetStartedAt { get; init; }
    public string ExecutablePath { get; init; } = string.Empty;
    public string ProfilePath { get; init; } = string.Empty;
    public string RuntimeVersion { get; init; } = string.Empty;
    public int ProtocolVersion { get; init; }
    public string CurrentTheme { get; init; } = "dark";
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int RestartCount { get; init; }
    public Dictionary<string, bool> AutomatedChecks { get; init; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, VisualCheckResult> VisualChecks { get; init; } =
        new(StringComparer.Ordinal);
    public string CleanupResult { get; init; } = "not-started";
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }
    public string PipeName { get; init; } = string.Empty;
}

public sealed record AcceptanceCommand(
    string Command,
    string? Check = null,
    string? Result = null,
    string? Note = null,
    bool PreserveArtifacts = false);

public sealed record AcceptanceCommandResult(
    bool Passed,
    string Status,
    VisualAcceptanceState State,
    string? ErrorCode = null);

public sealed class VisualAcceptanceStateStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

    private readonly string _stateFile;

    public VisualAcceptanceStateStore(string stateFile)
    {
        _stateFile = stateFile;
    }

    public async Task WriteAsync(
        VisualAcceptanceState state,
        CancellationToken cancellationToken = default)
    {
        // Replace through a unique temporary file so readers never see partial JSON.
        var temporary = $"{_stateFile}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
        try
        {
            await File.WriteAllTextAsync(
                temporary,
                JsonSerializer.Serialize(
                    state with { UpdatedAt = DateTimeOffset.UtcNow },
                    JsonOptions),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporary, _stateFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static async Task<VisualAcceptanceState> ReadAsync(
        string stateFile,
        CancellationToken cancellationToken = default)
    {
        // The host atomically replaces this file while `start` polls it.
        // Delete sharing prevents a Windows reader from blocking that rename.
        await using var stream = new FileStream(
            stateFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Deserialize<VisualAcceptanceState>(json, JsonOptions)
               ?? throw new JsonException("Visual acceptance state is invalid.");
    }

    public static string Serialize(object value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    public static string SerializeCompact(object value) =>
        JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
