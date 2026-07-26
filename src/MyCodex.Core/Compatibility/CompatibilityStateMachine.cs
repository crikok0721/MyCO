// Converts runtime evidence into a fail-closed compatibility state.
namespace MyCodex.Compatibility;

public enum CompatibilityState
{
    Unknown,
    Compatible,
    Degraded,
    SafeMode,
    InjectionBackendUnsupported,
    RuntimeProtocolMismatch
}

public sealed record CompatibilityEvidence(
    bool CdpAvailable,
    bool RuntimeHandshakePassed,
    int MatchedUserTurns,
    int MatchedAssistantTurns,
    double AverageConfidence,
    bool RuntimeError = false,
    bool ProtocolMismatch = false);

public static class CompatibilityStateMachine
{
    public static CompatibilityState Evaluate(CompatibilityEvidence evidence)
    {
        // Infrastructure and protocol failures take priority over visual confidence.
        if (!evidence.CdpAvailable)
        {
            return CompatibilityState.InjectionBackendUnsupported;
        }
        if (evidence.ProtocolMismatch)
        {
            return CompatibilityState.RuntimeProtocolMismatch;
        }
        if (evidence.RuntimeError || !evidence.RuntimeHandshakePassed)
        {
            return CompatibilityState.SafeMode;
        }
        if (evidence.MatchedUserTurns + evidence.MatchedAssistantTurns == 0)
        {
            return CompatibilityState.SafeMode;
        }
        if (evidence.AverageConfidence >= 0.85)
        {
            return CompatibilityState.Compatible;
        }
        return evidence.AverageConfidence >= 0.68
            ? CompatibilityState.Degraded
            : CompatibilityState.SafeMode;
    }
}
