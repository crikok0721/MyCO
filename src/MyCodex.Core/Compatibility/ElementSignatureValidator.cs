using System.Text;
using System.Text.RegularExpressions;

namespace MyCodex.Compatibility;

// Treats renderer-generated calibration data as untrusted before it reaches disk.
public static partial class ElementSignatureValidator
{
    private static readonly HashSet<string> AllowedAttributes =
        new(StringComparer.Ordinal)
        {
            "role",
            "data-message-author-role",
            "data-testid",
            "data-role",
            "data-author",
            "data-content-type",
            "data-content-search-turn-key",
            "data-content-search-unit-key",
            "data-user-message-bubble",
            "data-virtualized-turn-content"
        };

    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.Ordinal)
        {
            "article", "button", "dialog", "document", "feed", "group", "list",
            "listitem", "log", "main", "none", "presentation", "region", "status"
        };

    private static readonly string[] SemanticTokens =
    [
        "assistant", "user", "message", "turn", "thread", "conversation",
        "markdown", "prose", "tool", "action", "status"
    ];

    public static ElementSignature Normalize(ElementSignature signature)
    {
        if (signature.SchemaVersion != BuildInfo.CalibrationSchemaVersion)
        {
            throw new ArgumentException("Calibration schema is not supported.");
        }

        var tag = NormalizeTag(signature.TagName);
        var role = NormalizeRole(signature.Role);
        var attributes = (signature.StableAttributes ?? [])
            .Take(16)
            .Where(pair => AllowedAttributes.Contains(pair.Key))
            .Where(pair => IsSafeToken(pair.Value, 80))
            .Where(pair =>
                pair.Key == "role"
                    ? NormalizeRole(pair.Value) is not null
                    : SemanticTokens.Any(token =>
                        pair.Value.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);

        var classes = (signature.StableClasses ?? [])
            .Where(value => IsSafeToken(value, 80))
            .Distinct(StringComparer.Ordinal)
            .Take(32)
            .Order(StringComparer.Ordinal)
            .ToList();

        var ancestors = (signature.AncestorChain ?? [])
            .Take(5)
            .Select(ancestor => new SignatureAncestor
            {
                TagName = NormalizeTag(ancestor.TagName),
                Role = NormalizeRole(ancestor.Role)
            })
            .ToList();

        var histogram = (signature.ChildTagHistogram ?? [])
            .Take(32)
            .Select(pair => (
                Tag: NormalizeTag(pair.Key),
                Count: Math.Clamp(pair.Value, 0, 10_000)))
            .GroupBy(pair => pair.Tag, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => Math.Clamp(group.Sum(pair => pair.Count), 0, 10_000),
                StringComparer.Ordinal);

        var layout = signature.Layout ?? new SignatureLayout();
        var capabilities = signature.Capabilities ?? new SignatureCapabilities();
        var alignment = layout.Alignment is "left" or "center" or "right"
            ? layout.Alignment
            : "unknown";
        var widthRatio = double.IsFinite(layout.WidthRatio)
            ? Math.Clamp(layout.WidthRatio, 0, 1)
            : 0;

        var normalized = signature with
        {
            SampleCount = Math.Clamp(signature.SampleCount, 0, 16),
            ContextFingerprint = NormalizeContextFingerprint(
                signature.ContextFingerprint),
            TagName = tag,
            Role = role,
            StableAttributes = attributes,
            StableClasses = classes,
            AncestorChain = ancestors,
            ChildTagHistogram = histogram,
            Layout = new SignatureLayout
            {
                Alignment = alignment,
                WidthRatio = Math.Round(widthRatio, 3)
            },
            Capabilities = capabilities,
            Fingerprint = string.Empty
        };
        return normalized with { Fingerprint = BuildFingerprint(normalized) };
    }

    public static bool AreDistinctRoles(
        ElementSignature? user,
        ElementSignature? assistant)
    {
        if (user is null || assistant is null)
        {
            return true;
        }
        if (string.Equals(
                user.Fingerprint,
                assistant.Fingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }

        var userToAssistant = SignatureMatcher.Score(user, assistant);
        var assistantToUser = SignatureMatcher.Score(assistant, user);
        return userToAssistant < 0.86 || assistantToUser < 0.86;
    }

    public static bool IsValidatedMultiSample(ElementSignature? signature)
    {
        return signature is not null &&
               signature.SampleCount >= 3 &&
               !string.IsNullOrWhiteSpace(signature.ContextFingerprint);
    }

    private static string NormalizeTag(string value)
    {
        var tag = value.Trim().ToLowerInvariant();
        if (!TagPattern().IsMatch(tag))
        {
            throw new ArgumentException("Calibration contains an invalid HTML tag.");
        }
        return tag;
    }

    private static string? NormalizeRole(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var role = value.Trim().ToLowerInvariant();
        if (!AllowedRoles.Contains(role))
        {
            throw new ArgumentException("Calibration contains an unsupported semantic role.");
        }
        return role;
    }

    private static bool IsSafeToken(string? value, int maximumLength)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length <= maximumLength &&
               TokenPattern().IsMatch(value);
    }

    private static string NormalizeContextFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        var normalized = value.Trim();
        if (normalized.Length > 512 || !ContextFingerprintPattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Calibration contains an invalid context fingerprint.");
        }
        return normalized;
    }

    private static string BuildFingerprint(ElementSignature signature)
    {
        var builder = new StringBuilder();
        builder.Append(signature.TagName).Append(';').Append(signature.Role).Append(';');
        builder.Append(signature.SampleCount)
            .Append(';')
            .Append(signature.ContextFingerprint)
            .Append(';');
        foreach (var pair in signature.StableAttributes.OrderBy(pair => pair.Key))
        {
            builder.Append(pair.Key).Append('=').Append(pair.Value).Append('|');
        }
        builder.Append(';');
        foreach (var pair in signature.ChildTagHistogram.OrderBy(pair => pair.Key))
        {
            builder.Append(pair.Key).Append(':').Append(pair.Value).Append(',');
        }
        builder.Append(';')
            .Append(signature.Capabilities.HasCode ? "code" : string.Empty)
            .Append(';')
            .Append(signature.Capabilities.HasButtons ? "buttons" : string.Empty);
        return builder.ToString();
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    [GeneratedRegex("^[A-Za-z0-9_.:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex("^[A-Za-z0-9_.:;=|/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ContextFingerprintPattern();
}
