using System.Text.Json;

namespace MyCodex.Injection;

public sealed record RuntimeHostEvent(
    string Type,
    JsonElement Payload,
    int ProtocolVersion,
    DateTimeOffset At);

public sealed record RuntimeHandshake(
    string Version,
    int ProtocolVersion);

public sealed record RuntimeInjectionResult(
    bool Passed,
    string Status,
    RuntimeHandshake? Handshake,
    RuntimeTargetSession? Session,
    string? Error);
