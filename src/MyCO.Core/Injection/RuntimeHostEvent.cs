using System.Text.Json;

// Typed messages exchanged between the WPF host and the injected page runtime.
namespace MyCO.Injection;

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
