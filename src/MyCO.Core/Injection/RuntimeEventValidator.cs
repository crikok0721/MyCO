using System.Text.Json;
using System.Text.RegularExpressions;
using MyCO.Compatibility;

namespace MyCO.Injection;

// Normalizes the small event surface exposed by the untrusted renderer page.
public static partial class RuntimeEventValidator
{
    public const int MaximumBindingPayloadBytes = 64 * 1024;

    public static RuntimeHostEvent Normalize(RuntimeHostEvent hostEvent)
    {
        if (hostEvent.ProtocolVersion != BuildInfo.ProtocolVersion)
        {
            throw new ArgumentException("Runtime event protocol is not supported.");
        }

        var payload = hostEvent.Type switch
        {
            "runtimeReady" => NormalizeRuntimeReady(hostEvent.Payload),
            "compatibilityChanged" => NormalizeCompatibility(hostEvent.Payload),
            "calibrationResult" => NormalizeCalibration(hostEvent.Payload),
            "diagnostics" => RuntimeDiagnosticsValidator.Normalize(hostEvent.Payload),
            "error" => NormalizeError(hostEvent.Payload),
            _ => throw new ArgumentException("Runtime event type is not allowed.")
        };

        return new RuntimeHostEvent(
            hostEvent.Type,
            payload,
            BuildInfo.ProtocolVersion,
            DateTimeOffset.UtcNow);
    }

    private static JsonElement NormalizeRuntimeReady(JsonElement payload)
    {
        var version = RequiredShortString(payload, "version", 64);
        var protocol = payload.GetProperty("protocolVersion").GetInt32();
        if (protocol != BuildInfo.ProtocolVersion)
        {
            throw new ArgumentException("Runtime ready event has the wrong protocol.");
        }
        return JsonSerializer.SerializeToElement(new
        {
            version,
            protocolVersion = protocol
        });
    }

    private static JsonElement NormalizeCompatibility(JsonElement payload)
    {
        var state = RequiredShortString(payload, "state", 24);
        if (state is not ("compatible" or "degraded" or "safeMode"))
        {
            throw new ArgumentException("Runtime compatibility state is invalid.");
        }
        var confidence = payload.GetProperty("averageConfidence").GetDouble();
        if (!double.IsFinite(confidence) || confidence is < 0 or > 1)
        {
            throw new ArgumentException("Runtime confidence is invalid.");
        }
        return JsonSerializer.SerializeToElement(new
        {
            state,
            averageConfidence = Math.Round(confidence, 3)
        });
    }

    private static JsonElement NormalizeCalibration(JsonElement payload)
    {
        var role = RequiredShortString(payload, "role", 16);
        if (role is not ("assistant" or "user"))
        {
            throw new ArgumentException("Calibration role is invalid.");
        }
        var signature = payload.GetProperty("signature")
            .Deserialize<ElementSignature>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ArgumentException("Calibration signature is missing.");
        var normalized = ElementSignatureValidator.Normalize(signature);
        return JsonSerializer.SerializeToElement(
            new { role, signature = normalized },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static JsonElement NormalizeError(JsonElement payload)
    {
        var code = RequiredShortString(payload, "code", 48);
        if (!ErrorCodePattern().IsMatch(code))
        {
            throw new ArgumentException("Runtime error code is invalid.");
        }
        return JsonSerializer.SerializeToElement(new { code });
    }

    private static string RequiredShortString(
        JsonElement payload,
        string propertyName,
        int maximumLength)
    {
        var value = payload.GetProperty(propertyName).GetString();
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new ArgumentException($"Runtime field '{propertyName}' is invalid.");
        }
        return value;
    }

    [GeneratedRegex("^[a-z][a-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ErrorCodePattern();
}
