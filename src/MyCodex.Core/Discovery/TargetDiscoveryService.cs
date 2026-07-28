using System.Text.Json;
using MyCodex.Cdp;

// Ranks CDP renderer targets so injection avoids background and DevTools pages.
namespace MyCodex.Discovery;

public sealed record TargetCandidate(
    CdpTarget Target,
    int Score,
    string ReadyState,
    int BodyChildCount,
    bool HasDocument,
    string VisibilityState,
    int ConversationSurfaceCount,
    int TurnCount,
    int UnitCount,
    int UserBubbleCount)
{
    public bool HasConversationEvidence =>
        ConversationSurfaceCount > 0 || TurnCount > 0 ||
        UnitCount > 0 || UserBubbleCount > 0;
}

public sealed class TargetDiscoveryService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
    public async Task<IReadOnlyList<TargetCandidate>> DiscoverAsync(
        IDesktopDebugConnection connection,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TargetCandidate>();
        // Inspect candidate documents instead of trusting target titles alone.
        foreach (var target in await connection.ListTargetsAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            if (target.Type is not ("page" or "webview"))
            {
                continue;
            }

            try
            {
                await using var client = await connection.OpenTargetAsync(
                    target,
                    cancellationToken).ConfigureAwait(false);
                var response = await client.SendCommandAsync(
                    "Runtime.evaluate",
                    new
                    {
                        expression =
                            """
                            (() => {
                              const count = (selector, maximum) =>
                                Math.min(document.querySelectorAll(selector).length, maximum);
                              return {
                                readyState: document.readyState,
                                bodyChildCount: document.body?.children.length ?? 0,
                                hasDocument: !!document.documentElement,
                                visibilityState: document.visibilityState ?? "unknown",
                                conversationSurfaceCount: count(
                                  ".thread-scroll-container,[data-thread-find-composer]," +
                                  "[data-testid*=conversation],textarea,[contenteditable=true]",
                                  20),
                                turnCount: count("[data-content-search-turn-key]", 100),
                                unitCount: count("[data-content-search-unit-key]", 200),
                                userBubbleCount: count("[data-user-message-bubble]", 100)
                              };
                            })()
                            """,
                        returnByValue = true
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                var value = response
                    .GetProperty("result")
                    .GetProperty("result")
                    .GetProperty("value");
                var readyState = value.GetProperty("readyState").GetString() ?? "unknown";
                var childCount = value.GetProperty("bodyChildCount").GetInt32();
                var hasDocument = value.GetProperty("hasDocument").GetBoolean();
                var visibilityState =
                    value.GetProperty("visibilityState").GetString() ?? "unknown";
                var conversationSurfaceCount =
                    value.GetProperty("conversationSurfaceCount").GetInt32();
                var turnCount = value.GetProperty("turnCount").GetInt32();
                var unitCount = value.GetProperty("unitCount").GetInt32();
                var userBubbleCount = value.GetProperty("userBubbleCount").GetInt32();
                results.Add(new TargetCandidate(
                    target,
                    Score(
                        target,
                        readyState,
                        childCount,
                        hasDocument,
                        visibilityState,
                        conversationSurfaceCount,
                        turnCount,
                        unitCount,
                        userBubbleCount),
                    readyState,
                    childCount,
                    hasDocument,
                    visibilityState,
                    conversationSurfaceCount,
                    turnCount,
                    unitCount,
                    userBubbleCount));
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException)
            {
                // Background and transient renderer targets are expected.
            }
        }

        return results.OrderByDescending(candidate => candidate.Score).ToArray();
    }

    private static int Score(
        CdpTarget target,
        string readyState,
        int childCount,
        bool hasDocument,
        string visibilityState,
        int conversationSurfaceCount,
        int turnCount,
        int unitCount,
        int userBubbleCount)
    {
        var score = target.Type == "page" ? 10 : 5;
        score += hasDocument ? 8 : 0;
        score += readyState is "interactive" or "complete" ? 7 : 0;
        score += childCount > 0 ? 5 : 0;
        score += visibilityState == "visible" ? 15 : 0;
        score += target.Url?.StartsWith("app://", StringComparison.OrdinalIgnoreCase) == true
            ? 5
            : 0;
        score += target.Title?.Contains("Codex", StringComparison.OrdinalIgnoreCase) == true
            ? 5
            : 0;
        score += target.Title?.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase) == true
            ? 5
            : 0;
        score += Math.Min(conversationSurfaceCount, 2) * 20;
        score += Math.Min(turnCount, 4) * 5;
        score += Math.Min(unitCount, 8) * 3;
        score += Math.Min(userBubbleCount, 4) * 4;
        return score;
    }
}
