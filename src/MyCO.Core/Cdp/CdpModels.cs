using System.Text.Json.Serialization;

// Wire models for the loopback CDP discovery endpoints and feasibility reports.
namespace MyCO.Cdp;

public sealed record CdpVersionInfo(
    [property: JsonPropertyName("Browser")] string? Browser,
    [property: JsonPropertyName("Protocol-Version")] string? ProtocolVersion,
    [property: JsonPropertyName("User-Agent")] string? UserAgent,
    [property: JsonPropertyName("V8-Version")] string? V8Version,
    [property: JsonPropertyName("webSocketDebuggerUrl")] string? WebSocketDebuggerUrl);

public sealed record CdpTarget(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("webSocketDebuggerUrl")] string? WebSocketDebuggerUrl);

public sealed record RendererProbeResult(
    string? TargetId,
    string? Type,
    string? Title,
    string? Url,
    string ReadyState,
    int BodyChildCount,
    int Score,
    bool EvaluationPassed,
    bool DomMutationPassed);

public sealed record CdpFeasibilityResult(
    bool Passed,
    string Status,
    string? FailureReason,
    int Port,
    string BindAddress,
    string? ExecutablePath,
    int? LaunchedProcessId,
    CdpVersionInfo? Version,
    IReadOnlyList<CdpTarget> Targets,
    RendererProbeResult? Renderer,
    DateTimeOffset StartedAt,
    TimeSpan Duration);
