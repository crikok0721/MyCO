using System.Net.Http.Json;
using System.Text.Json;
using MyCodex.Cdp;

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
    private readonly HttpClient _httpClient;

    public TargetDiscoveryService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<IReadOnlyList<CdpTarget>> ListTargetsAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        var targets = await _httpClient.GetFromJsonAsync<CdpTarget[]>(
            $"http://127.0.0.1:{port}/json/list",
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        return targets ?? [];
    }

    public async Task<IReadOnlyList<TargetCandidate>> DiscoverAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TargetCandidate>();
        foreach (var target in await ListTargetsAsync(port, cancellationToken)
                     .ConfigureAwait(false))
        {
            if (target.Type is not ("page" or "webview") ||
                !Uri.TryCreate(target.WebSocketDebuggerUrl, UriKind.Absolute, out var socketUri))
            {
                continue;
            }

            try
            {
                await using var client = new CdpClient();
                await client.ConnectAsync(socketUri, cancellationToken).ConfigureAwait(false);
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
            catch (Exception)
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
