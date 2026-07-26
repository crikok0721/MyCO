using System.Text.Json;
using MyCodex.Compatibility;

// Verifies fail-closed state transitions and structural signature scoring.
namespace MyCodex.Tests;

public sealed class CompatibilityTests
{
    [Theory]
    [InlineData(false, true, 1, 1, .9, CompatibilityState.InjectionBackendUnsupported)]
    [InlineData(true, false, 1, 1, .9, CompatibilityState.SafeMode)]
    [InlineData(true, true, 0, 0, .9, CompatibilityState.SafeMode)]
    [InlineData(true, true, 1, 1, .9, CompatibilityState.Compatible)]
    [InlineData(true, true, 1, 1, .73, CompatibilityState.Degraded)]
    [InlineData(true, true, 1, 1, .4, CompatibilityState.SafeMode)]
    public void CompatibilityStateTransitionsAreFailClosed(
        bool cdp,
        bool handshake,
        int user,
        int assistant,
        double confidence,
        CompatibilityState expected)
    {
        var evidence = new CompatibilityEvidence(
            cdp,
            handshake,
            user,
            assistant,
            confidence);
        Assert.Equal(expected, CompatibilityStateMachine.Evaluate(evidence));
    }

    [Fact]
    public void GeneratedClassFilteringMatchesRuntimePolicy()
    {
        Assert.True(SignatureMatcher.IsLikelyGeneratedClass("css-a8f32c"));
        Assert.True(SignatureMatcher.IsLikelyGeneratedClass("_x7sd92"));
        Assert.False(SignatureMatcher.IsLikelyGeneratedClass("message-row"));
    }

    [Fact]
    public void SignatureScoringSurvivesHashChangesAndSerializesWithoutText()
    {
        var expected = Signature("css-a8f32c", "main");
        var candidate = Signature("_x7sd92", "main");
        var score = SignatureMatcher.Score(expected, candidate);
        var serialized = JsonSerializer.Serialize(expected);

        Assert.True(score >= .85);
        Assert.DoesNotContain("PRIVATE CHAT", serialized);
        Assert.Equal(expected.Fingerprint, candidate.Fingerprint);
    }

    private static ElementSignature Signature(string generatedClass, string ancestor)
    {
        return new ElementSignature
        {
            TagName = "article",
            StableAttributes =
            {
                ["data-message-author-role"] = "assistant"
            },
            StableClasses = [generatedClass, "message-row"],
            AncestorChain =
            [
                new SignatureAncestor { TagName = ancestor, Role = "main" }
            ],
            ChildTagHistogram =
            {
                ["div"] = 1
            },
            Capabilities = new SignatureCapabilities
            {
                HasMarkdown = true,
                HasCode = true,
                HasButtons = false
            },
            Layout = new SignatureLayout
            {
                Alignment = "left",
                WidthRatio = .62
            },
            Fingerprint = "article;assistant;div:1;code"
        };
    }
}
