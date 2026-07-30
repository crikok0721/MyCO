using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

// Starts an isolated desktop instance and verifies that CDP can evaluate and edit its DOM.
namespace MyCO.Cdp;

public sealed class CdpProbe
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public CdpProbe(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<CdpFeasibilityResult> RunAsync(
        string executablePath,
        string userDataDirectory,
        TimeSpan? startupTimeout = null,
        bool terminateLaunchedProcess = true,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        var port = PortAllocator.GetRandomLoopbackPort();
        Process? process = null;

        try
        {
            Directory.CreateDirectory(userDataDirectory);
            process = Launch(executablePath, userDataDirectory, port);

            var (version, targets) = await WaitForEndpointAsync(
                port,
                startupTimeout ?? TimeSpan.FromSeconds(30),
                cancellationToken).ConfigureAwait(false);

            // A listening endpoint is not enough; at least one renderer must allow safe DOM work.
            var renderer = await ProbeBestRendererAsync(targets, cancellationToken)
                .ConfigureAwait(false);
            var passed = version is not null &&
                         targets.Count > 0 &&
                         renderer is { EvaluationPassed: true, DomMutationPassed: true };

            return new CdpFeasibilityResult(
                passed,
                passed ? "Pass" : "NoUsableRenderer",
                passed ? null : "CDP responded, but no renderer passed evaluation and DOM mutation.",
                port,
                IPAddress.Loopback.ToString(),
                executablePath,
                process.Id,
                version,
                targets,
                renderer,
                startedAt,
                timer.Elapsed);
        }
        catch (Exception exception)
        {
            return new CdpFeasibilityResult(
                false,
                "Failed",
                $"{exception.GetType().Name}: {exception.Message}",
                port,
                IPAddress.Loopback.ToString(),
                executablePath,
                process?.Id,
                null,
                Array.Empty<CdpTarget>(),
                null,
                startedAt,
                timer.Elapsed);
        }
        finally
        {
            if (terminateLaunchedProcess && process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            process?.Dispose();
        }
    }

    public async Task<(CdpVersionInfo? Version, IReadOnlyList<CdpTarget> Targets)>
        InspectEndpointAsync(int port, CancellationToken cancellationToken = default)
    {
        var baseUri = new Uri($"http://127.0.0.1:{port}/");
        var version = await _httpClient.GetFromJsonAsync<CdpVersionInfo>(
            new Uri(baseUri, "json/version"),
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        var targets = await _httpClient.GetFromJsonAsync<CdpTarget[]>(
            new Uri(baseUri, "json/list"),
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        return (version, targets ?? Array.Empty<CdpTarget>());
    }

    private static Process Launch(string executablePath, string userDataDirectory, int port)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Desktop executable was not found.", executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory
        };
        // Loopback binding prevents other machines on the network from reaching the endpoint.
        startInfo.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
        startInfo.ArgumentList.Add($"--remote-debugging-port={port}");
        startInfo.ArgumentList.Add($"--user-data-dir={userDataDirectory}");
        startInfo.ArgumentList.Add("--no-first-run");

        return Process.Start(startInfo)
               ?? throw new InvalidOperationException("Desktop process did not start.");
    }

    private async Task<(CdpVersionInfo Version, IReadOnlyList<CdpTarget> Targets)>
        WaitForEndpointAsync(
            int port,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Exception? lastError = null;

        // Chromium exposes /json endpoints shortly after process start, so poll briefly.
        while (!timeoutSource.IsCancellationRequested)
        {
            try
            {
                var (version, targets) = await InspectEndpointAsync(
                    port,
                    timeoutSource.Token).ConfigureAwait(false);
                if (version is not null && targets.Count > 0)
                {
                    return (version, targets);
                }
            }
            catch (Exception exception) when (
                exception is HttpRequestException or JsonException or TaskCanceledException)
            {
                lastError = exception;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), timeoutSource.Token)
                .ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"CDP endpoint 127.0.0.1:{port} did not become ready within {timeout}.",
            lastError);
    }

    private static async Task<RendererProbeResult?> ProbeBestRendererAsync(
        IReadOnlyList<CdpTarget> targets,
        CancellationToken cancellationToken)
    {
        RendererProbeResult? best = null;

        // DevTools, extensions, and background pages also appear here; test each defensively.
        foreach (var target in targets
                     .Where(candidate =>
                         candidate.Type is "page" or "webview" &&
                         Uri.TryCreate(
                             candidate.WebSocketDebuggerUrl,
                             UriKind.Absolute,
                             out _)))
        {
            try
            {
                await using var client = new CdpClient();
                await client.ConnectAsync(
                    new Uri(target.WebSocketDebuggerUrl!),
                    cancellationToken).ConfigureAwait(false);
                await client.SendCommandAsync(
                    "Runtime.enable",
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var evaluation = await client.SendCommandAsync(
                    "Runtime.evaluate",
                    new
                    {
                        expression =
                            "({readyState:document.readyState,bodyChildCount:document.body?.children.length??0,sum:1+1})",
                        returnByValue = true,
                        awaitPromise = true
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var value = evaluation
                    .GetProperty("result")
                    .GetProperty("result")
                    .GetProperty("value");
                var readyState = value.GetProperty("readyState").GetString() ?? "unknown";
                var bodyChildCount = value.GetProperty("bodyChildCount").GetInt32();
                var evaluationPassed = value.GetProperty("sum").GetInt32() == 2;
                var score = ScoreTarget(target, readyState, bodyChildCount);

                // The temporary attribute proves that a reversible DOM mutation is possible.
                var mutation = await client.SendCommandAsync(
                    "Runtime.evaluate",
                    new
                    {
                        expression =
                            "(()=>{const b=document.body;if(!b)return false;b.setAttribute('data-myco-probe','true');const installed=b.getAttribute('data-myco-probe')==='true';b.removeAttribute('data-myco-probe');return installed&&!b.hasAttribute('data-myco-probe');})()",
                        returnByValue = true
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                var mutationPassed = mutation
                    .GetProperty("result")
                    .GetProperty("result")
                    .GetProperty("value")
                    .GetBoolean();

                var result = new RendererProbeResult(
                    target.Id,
                    target.Type,
                    target.Title,
                    target.Url,
                    readyState,
                    bodyChildCount,
                    score,
                    evaluationPassed,
                    mutationPassed);
                if (best is null || result.Score > best.Score)
                {
                    best = result;
                }
            }
            catch (Exception)
            {
                // A single devtools/background target must not fail discovery.
            }
        }

        return best;
    }

    private static int ScoreTarget(CdpTarget target, string readyState, int bodyChildCount)
    {
        var score = target.Type == "page" ? 30 : 15;
        score += readyState == "complete" ? 15 : 5;
        score += bodyChildCount > 0 ? 15 : 0;
        score += target.Url?.StartsWith("app://", StringComparison.OrdinalIgnoreCase) == true
            ? 20
            : 0;
        score += target.Title?.Contains("Codex", StringComparison.OrdinalIgnoreCase) == true
            ? 15
            : 0;
        score += target.Title?.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase) == true
            ? 10
            : 0;
        return score;
    }
}
