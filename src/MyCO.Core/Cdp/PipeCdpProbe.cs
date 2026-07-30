using System.Diagnostics;
using System.Text.Json;

namespace MyCO.Cdp;

// Exercises the private Chromium pipe against an isolated Desktop profile.
public sealed class PipeCdpProbe
{
    public async Task<CdpFeasibilityResult> RunAsync(
        string executablePath,
        string userDataDirectory,
        TimeSpan? startupTimeout = null,
        bool terminateLaunchedProcess = true,
        TimeSpan? holdOpen = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        PipeLaunchedProcess? launched = null;
        PipeCdpConnection? root = null;
        try
        {
            Directory.CreateDirectory(userDataDirectory);
            launched = WindowsPipeProcessLauncher.Launch(
                executablePath,
                Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                [
                    "--remote-debugging-pipe",
                    $"--user-data-dir={userDataDirectory}",
                    "--no-first-run"
                ]);
            root = new PipeCdpConnection(
                launched.BrowserOutput,
                launched.BrowserInput);

            var targets = await WaitForTargetsAsync(
                root,
                startupTimeout ?? TimeSpan.FromSeconds(30),
                cancellationToken).ConfigureAwait(false);
            var versionResponse = await root.SendCommandAsync(
                "Browser.getVersion",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var version = ParseVersion(versionResponse.GetProperty("result"));
            var renderer = await ProbeBestRendererAsync(
                root,
                targets,
                cancellationToken).ConfigureAwait(false);
            var passed = renderer is
            {
                EvaluationPassed: true,
                DomMutationPassed: true
            };
            if (holdOpen is { } hold && hold > TimeSpan.Zero)
            {
                await Task.Delay(hold, cancellationToken).ConfigureAwait(false);
            }
            return new CdpFeasibilityResult(
                passed,
                passed ? "Pass" : "NoUsableRenderer",
                passed ? null : "No pipe renderer passed the safe probe.",
                0,
                "private-pipe",
                executablePath,
                launched.Process.Id,
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
                0,
                "private-pipe",
                executablePath,
                launched?.Process.Id,
                null,
                [],
                null,
                startedAt,
                timer.Elapsed);
        }
        finally
        {
            if (root is not null)
            {
                await root.DisposeAsync().ConfigureAwait(false);
            }
            if (terminateLaunchedProcess &&
                launched?.Process is { HasExited: false } process)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
            launched?.Process.Dispose();
        }
    }


    private static async Task<IReadOnlyList<CdpTarget>> WaitForTargetsAsync(
        PipeCdpConnection connection,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        deadline.CancelAfter(timeout);
        while (!deadline.IsCancellationRequested)
        {
            try
            {
                var targets = await connection.ListTargetsAsync(deadline.Token)
                    .ConfigureAwait(false);
                if (targets.Count > 0)
                {
                    return targets;
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or IOException)
            {
                // Chromium may not have initialized Target discovery yet.
            }
            await Task.Delay(250, deadline.Token).ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"CDP pipe did not expose a renderer within {timeout}.");
    }

    private static async Task<RendererProbeResult?> ProbeBestRendererAsync(
        PipeCdpConnection connection,
        IReadOnlyList<CdpTarget> targets,
        CancellationToken cancellationToken)
    {
        RendererProbeResult? best = null;
        foreach (var target in targets.Where(candidate =>
                     candidate.Type is "page" or "webview"))
        {
            try
            {
                await using var client = await connection.AttachAsync(
                    target,
                    cancellationToken).ConfigureAwait(false);
                var evaluation = await client.SendCommandAsync(
                    "Runtime.evaluate",
                    new
                    {
                        expression =
                            "({value:1+1,readyState:document.readyState,bodyChildCount:document.body?.children.length??0})",
                        returnByValue = true
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                var value = evaluation.GetProperty("result")
                    .GetProperty("result")
                    .GetProperty("value");
                var evaluated = value.GetProperty("value").GetInt32() == 2;
                var mutation = await client.SendCommandAsync(
                    "Runtime.evaluate",
                    new
                    {
                        expression =
                            "(()=>{const b=document.body;if(!b)return false;b.dataset.mycoPipeProbe='true';const ok=b.dataset.mycoPipeProbe==='true';delete b.dataset.mycoPipeProbe;return ok})()",
                        returnByValue = true
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                var mutated = mutation.GetProperty("result")
                    .GetProperty("result")
                    .GetProperty("value")
                    .GetBoolean();
                var childCount = value.GetProperty("bodyChildCount").GetInt32();
                var readyState = value.GetProperty("readyState").GetString() ?? "unknown";
                var result = new RendererProbeResult(
                    target.Id,
                    target.Type,
                    target.Title,
                    target.Url,
                    readyState,
                    childCount,
                    childCount > 0 ? 75 : 55,
                    evaluated,
                    mutated);
                if (best is null || result.Score > best.Score)
                {
                    best = result;
                }
            }
            catch (Exception)
            {
                // Transient and non-document targets are expected.
            }
        }
        return best;
    }

    private static CdpVersionInfo ParseVersion(JsonElement value) =>
        new(
            value.TryGetProperty("product", out var product)
                ? product.GetString()
                : null,
            value.TryGetProperty("protocolVersion", out var protocol)
                ? protocol.GetString()
                : null,
            value.TryGetProperty("userAgent", out var agent)
                ? agent.GetString()
                : null,
            value.TryGetProperty("jsVersion", out var v8)
                ? v8.GetString()
                : null,
            null);
}
