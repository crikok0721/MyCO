using System.Text.RegularExpressions;

namespace MyCodex.Compatibility;

public static partial class SignatureMatcher
{
    public static bool IsLikelyGeneratedClass(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 80)
        {
            return true;
        }
        return GeneratedClassPatterns().Any(pattern => pattern.IsMatch(token));
    }

    public static double Score(ElementSignature expected, ElementSignature candidate)
    {
        var score = 0d;
        var weight = 0d;

        Add(expected.TagName.Equals(candidate.TagName, StringComparison.OrdinalIgnoreCase), 0.13);
        Add(expected.Role == candidate.Role, expected.Role is null ? 0.04 : 0.12);

        if (expected.StableAttributes.Count > 0)
        {
            var matches = expected.StableAttributes.Count(pair =>
                candidate.StableAttributes.TryGetValue(pair.Key, out var value) &&
                value.Equals(pair.Value, StringComparison.OrdinalIgnoreCase));
            AddRatio((double)matches / expected.StableAttributes.Count, 0.25);
        }

        var stableExpected = expected.StableClasses
            .Where(token => !IsLikelyGeneratedClass(token))
            .ToArray();
        if (stableExpected.Length > 0)
        {
            var stableCandidate = candidate.StableClasses
                .Where(token => !IsLikelyGeneratedClass(token))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            AddRatio(
                (double)stableExpected.Count(stableCandidate.Contains) / stableExpected.Length,
                0.06);
        }

        var ancestorCount = Math.Min(
            expected.AncestorChain.Count,
            candidate.AncestorChain.Count);
        if (ancestorCount > 0)
        {
            var ancestorScore = 0d;
            for (var index = 0; index < ancestorCount; index++)
            {
                var left = expected.AncestorChain[index];
                var right = candidate.AncestorChain[index];
                ancestorScore += left.TagName.Equals(
                    right.TagName,
                    StringComparison.OrdinalIgnoreCase)
                    ? 0.7
                    : 0;
                ancestorScore += left.Role == right.Role ? 0.3 : 0;
            }
            AddRatio(ancestorScore / ancestorCount, 0.12);
        }

        AddRatio(
            new[]
            {
                expected.Capabilities.HasMarkdown == candidate.Capabilities.HasMarkdown,
                expected.Capabilities.HasCode == candidate.Capabilities.HasCode,
                expected.Capabilities.HasButtons == candidate.Capabilities.HasButtons
            }.Count(value => value) / 3d,
            0.1);

        if (expected.Layout.Alignment != "unknown")
        {
            Add(
                expected.Layout.Alignment.Equals(
                    candidate.Layout.Alignment,
                    StringComparison.OrdinalIgnoreCase),
                0.08);
        }

        return weight == 0 ? 0 : Math.Round(score / weight, 3);

        void Add(bool matches, double itemWeight)
        {
            weight += itemWeight;
            if (matches)
            {
                score += itemWeight;
            }
        }

        void AddRatio(double ratio, double itemWeight)
        {
            weight += itemWeight;
            score += Math.Clamp(ratio, 0, 1) * itemWeight;
        }
    }

    [GeneratedRegex(
        @"^(css-[a-z0-9]{5,}|_[a-z0-9]{6,}|[a-f0-9]{6,}|[a-z]{1,3}[0-9][a-z0-9_-]{4,}|[a-z0-9_-]{12,})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedClassRegex();

    private static Regex[] GeneratedClassPatterns() => [GeneratedClassRegex()];
}
