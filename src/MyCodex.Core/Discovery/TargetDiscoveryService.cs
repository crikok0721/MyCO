using System.Text.Json;
using MyCodex.Cdp;

// Ranks CDP renderer targets so injection avoids background and DevTools pages.
namespace MyCodex.Discovery;

public sealed record TargetCandidate(
    CdpTarget Target,
    int Score,
    string ReadyState,
    int BodyChildCount,
    bool HasDocument);

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
                            "({readyState:document.readyState,bodyChildCount:document.body?.children.length??0,hasDocument:!!document.documentElement})",
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
                results.Add(new TargetCandidate(
                    target,
                    Score(target, readyState, childCount, hasDocument),
                    readyState,
                    childCount,
                    hasDocument));
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
        bool hasDocument)
    {
        var score = target.Type == "page" ? 30 : 15;
        score += hasDocument ? 20 : 0;
        score += readyState is "interactive" or "complete" ? 15 : 0;
        score += childCount > 0 ? 10 : 0;
        score += target.Url?.StartsWith("app://", StringComparison.OrdinalIgnoreCase) == true
            ? 20
            : 0;
        score += target.Title?.Contains("Codex", StringComparison.OrdinalIgnoreCase) == true
            ? 10
            : 0;
        score += target.Title?.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase) == true
            ? 10
            : 0;
        return score;
    }
}
