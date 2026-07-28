using System.Text.Json;

namespace MyCodex.Injection;

// Exports only typed technical counters from renderer diagnostics.
public static class RuntimeDiagnosticsValidator
{
    public static JsonElement Normalize(JsonElement payload)
    {
        var version = ShortString(payload, "version", 64);
        var protocol = Int(payload, "protocolVersion", 0, 1000);
        var compatibility = ShortString(payload, "compatibility", 24);
        if (compatibility is not ("compatible" or "degraded" or "safeMode"))
        {
            compatibility = "safeMode";
        }

        var confidence = Double(payload, "averageConfidence", 0, 1);
        return JsonSerializer.SerializeToElement(new
        {
            version,
            protocolVersion = protocol,
            installed = Bool(payload, "installed"),
            compatibility,
            scannedTurns = Int(payload, "scannedTurns", 0, 100_000),
            identifiedUserTurns = Int(payload, "identifiedUserTurns", 0, 100_000),
            decoratedUserTurns = Int(payload, "decoratedUserTurns", 0, 100_000),
            decoratedAssistantTurns = Int(
                payload,
                "decoratedAssistantTurns",
                0,
                100_000),
            assistantBubbleBlocks = Int(
                payload,
                "assistantBubbleBlocks",
                0,
                100_000),
            unknownTurns = Int(payload, "unknownTurns", 0, 100_000),
            averageConfidence = Math.Round(confidence, 3),
            observerActive = Bool(payload, "observerActive"),
            lastRefreshAt = NormalizeTimestamp(payload),
            errors = NormalizeErrors(payload)
        });
    }

    private static object[] NormalizeErrors(JsonElement payload)
    {
        if (!payload.TryGetProperty("errors", out var errors) ||
            errors.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return errors.EnumerateArray()
            .Take(10)
            .Select(error => new
            {
                code = ShortString(error, "code", 48),
                at = NormalizeTimestamp(error)
            })
            .Cast<object>()
            .ToArray();
    }

    private static string? NormalizeTimestamp(JsonElement payload)
    {
        if (!payload.TryGetProperty("lastRefreshAt", out var property) &&
            !payload.TryGetProperty("at", out property))
        {
            return null;
        }
        return property.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParse(property.GetString(), out var parsed)
            ? parsed.ToUniversalTime().ToString("O")
            : null;
    }

    private static bool Bool(JsonElement payload, string name)
    {
        return payload.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.True;
    }

    private static int Int(JsonElement payload, string name, int minimum, int maximum)
    {
        if (!payload.TryGetProperty(name, out var property) ||
            !property.TryGetInt32(out var value))
        {
            return minimum;
        }
        return Math.Clamp(value, minimum, maximum);
    }

    private static double Double(
        JsonElement payload,
        string name,
        double minimum,
        double maximum)
    {
        if (!payload.TryGetProperty(name, out var property) ||
            !property.TryGetDouble(out var value) ||
            !double.IsFinite(value))
        {
            return minimum;
        }
        return Math.Clamp(value, minimum, maximum);
    }

    private static string ShortString(JsonElement payload, string name, int maximum)
    {
        if (!payload.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }
        var value = property.GetString() ?? string.Empty;
        return value.Length <= maximum ? value : value[..maximum];
    }
}
