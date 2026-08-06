using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using MyCO.Configuration;
using MyCO.Cdp;
using MyCO.Discovery;
using MyCO.Injection;

// Verifies CDP reply correlation and safe runtime bootstrap source generation.
namespace MyCO.Tests;

public sealed class CdpTests
{
    [Fact]
    public async Task JsonResponsesCorrelateConcurrentRequestsOutOfOrder()
    {
        var correlator = new CdpMessageCorrelator();
        var first = correlator.Register(101);
        var second = correlator.Register(202);
        using var secondDocument = JsonDocument.Parse("""{"id":202,"result":{"value":2}}""");
        using var firstDocument = JsonDocument.Parse("""{"id":101,"result":{"value":1}}""");

        Assert.True(correlator.TryHandle(secondDocument.RootElement));
        Assert.False(first.IsCompleted);
        Assert.True(correlator.TryHandle(firstDocument.RootElement));

        Assert.Equal(
            1,
            (await first).GetProperty("result").GetProperty("value").GetInt32());
        Assert.Equal(
            2,
            (await second).GetProperty("result").GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task JsonCorrelationPropagatesCdpErrors()
    {
        var correlator = new CdpMessageCorrelator();
        var pending = correlator.Register(9);
        using var document = JsonDocument.Parse(
            """{"id":9,"error":{"code":-32601,"message":"missing"}}""");

        Assert.True(correlator.TryHandle(document.RootElement));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
    }

    [Fact]
    public void PortAllocatorReturnsDynamicRangePort()
    {
        var port = PortAllocator.GetRandomLoopbackPort();
        Assert.InRange(port, 49152, 65535);
    }

    [Fact]
    public async Task RuntimeSessionRehydratesWhenHealthApiIsMissing()
    {
        var client = new FakeCdpClient(
            """{"result":{"result":{"type":"object","subtype":"null","value":null}}}""",
            DiagnosticsResponse(),
            """{"result":{"result":{"type":"object","value":{"active":true}}}}""");
        var session = new RuntimeTargetSession(
            new CdpTarget("target", "page", "Codex", "app://-/index.html", null),
            client,
            "__mc_test",
            "globalThis.__runtimeReloaded=true;",
            "{}");

        Assert.True(await session.EnsureActiveAsync());
        Assert.Equal(3, client.Calls.Count);
        Assert.All(client.Calls, call => Assert.Equal("Runtime.evaluate", call.Method));
        Assert.Contains(
            "globalThis.__runtimeReloaded=true;",
            client.Calls[1].Parameters.GetProperty("expression").GetString());
        Assert.Contains(
            "applyConfig({})",
            client.Calls[1].Parameters.GetProperty("expression").GetString());
    }

    [Fact]
    public async Task RuntimeSessionRejectsUndefinedApplyConfigResult()
    {
        var client = new FakeCdpClient(
            """{"result":{"result":{"type":"undefined"}}}""",
            """{"result":{"identifier":"new-document"}}""");
        var session = CreateRuntimeSession(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ApplyConfigAsync(AppConfig.Default));

        Assert.Contains("diagnostics", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(session.NewDocumentScriptId);
    }

    [Fact]
    public async Task RuntimeSessionRejectsCurrentDocumentEvaluationException()
    {
        var client = new FakeCdpClient(
            """{"result":{"result":{"type":"undefined"},"exceptionDetails":{"text":"Uncaught"}}}""");
        var session = CreateRuntimeSession(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ApplyConfigAsync(AppConfig.Default));

        Assert.Contains("evaluation failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(session.NewDocumentScriptId);
    }

    [Fact]
    public async Task RuntimeSessionRejectsApplyConfigDiagnosticsWithErrors()
    {
        var client = new FakeCdpClient(
            DiagnosticsResponse("""[{"code":"refresh","at":"2026-08-06T00:00:00Z"}]"""),
            """{"result":{"identifier":"new-document"}}""");
        var session = CreateRuntimeSession(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ApplyConfigAsync(AppConfig.Default));

        Assert.Contains("errors", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(session.NewDocumentScriptId);
    }

    [Fact]
    public async Task RuntimeSessionRegistersReplacementBeforeRemovingPreviousNewDocumentSource()
    {
        var client = new FakeCdpClient(
            DiagnosticsResponse(),
            """{"result":{"identifier":"latest-source"}}""",
            """{"result":{}}""");
        var session = CreateRuntimeSession(client);
        session.NewDocumentScriptId = "previous-source";

        await session.ApplyConfigAsync(AppConfig.Default);

        Assert.Equal("latest-source", session.NewDocumentScriptId);
        Assert.Equal("Runtime.evaluate", client.Calls[0].Method);
        Assert.Equal("Page.addScriptToEvaluateOnNewDocument", client.Calls[1].Method);
        Assert.Equal("Page.removeScriptToEvaluateOnNewDocument", client.Calls[2].Method);
        Assert.Equal(
            "previous-source",
            client.Calls[2].Parameters.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task RuntimeSessionKeepsPreviousRegistrationWhenReplacementAddFails()
    {
        var client = new ScriptReplacementCdpClient { FailAdd = true };
        var session = CreateRuntimeSession(client);
        session.NewDocumentScriptId = "previous-source";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ApplyConfigAsync(AppConfig.Default));

        Assert.Equal("previous-source", session.NewDocumentScriptId);
        Assert.Empty(client.RemovedIdentifiers);
    }

    [Fact]
    public async Task RuntimeSessionKeepsPreviousRegistrationWhenReplacementAddIsCanceled()
    {
        var client = new ScriptReplacementCdpClient { CancelAdd = true };
        var session = CreateRuntimeSession(client);
        session.NewDocumentScriptId = "previous-source";

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.ApplyConfigAsync(AppConfig.Default));

        Assert.Equal("previous-source", session.NewDocumentScriptId);
        Assert.Empty(client.RemovedIdentifiers);
    }

    [Fact]
    public async Task RuntimeSessionKeepsSuccessfulReplacementWhenPreviousRemovalFails()
    {
        var client = new ScriptReplacementCdpClient { FailRemove = true };
        var session = CreateRuntimeSession(client);
        session.NewDocumentScriptId = "previous-source";

        await session.ApplyConfigAsync(AppConfig.Default);

        Assert.Equal("latest-source", session.NewDocumentScriptId);
        Assert.Equal(["previous-source"], client.RemovedIdentifiers);
    }

    [Fact]
    public async Task RuntimeSessionRetriesEveryUnremovedRegistrationOnNextApply()
    {
        var client = new ScriptReplacementCdpClient();
        client.RegistrationIdentifiers.Enqueue("latest-source");
        client.RegistrationIdentifiers.Enqueue("next-source");
        client.RemoveFailuresRemaining["previous-source"] = 1;
        var session = CreateRuntimeSession(client);
        session.NewDocumentScriptId = "previous-source";

        await session.ApplyConfigAsync(AppConfig.Default);
        await session.ApplyConfigAsync(AppConfig.Default);

        Assert.Equal("next-source", session.NewDocumentScriptId);
        Assert.Equal(
            ["previous-source", "previous-source", "latest-source"],
            client.RemovedIdentifiers);
    }

    [Fact]
    public async Task RuntimeSessionDestroyAttemptsEveryTrackedRegistrationAfterOneFails()
    {
        var client = new ScriptReplacementCdpClient();
        client.RegistrationIdentifiers.Enqueue("latest-source");
        client.RemoveFailuresRemaining["previous-source"] = 2;
        var session = CreateRuntimeSession(client);
        session.NewDocumentScriptId = "previous-source";
        await session.ApplyConfigAsync(AppConfig.Default);

        await session.DestroyAsync();

        Assert.Equal(
            ["previous-source", "previous-source", "latest-source"],
            client.RemovedIdentifiers);
        Assert.Null(session.NewDocumentScriptId);
    }

    [Fact]
    public async Task ApplyConfigReturnsNotFullyAppliedWhenNoRendererSessionsExist()
    {
        await using var controller = new DesktopSessionController("runtime");

        var result = await controller.ApplyConfigAsync(AppConfig.Default);

        Assert.Equal(0, result.SessionCount);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.IsFullyApplied);
    }

    [Fact]
    public async Task ApplyConfigAttemptsEveryRendererAfterOneRendererFails()
    {
        await using var controller = new DesktopSessionController("runtime");
        var failedClient = new RuntimeSessionCdpClient { ApplyHasErrors = true };
        var appliedClient = new RuntimeSessionCdpClient();
        AttachSession(controller, CreateRuntimeSession(failedClient, "failed"));
        AttachSession(controller, CreateRuntimeSession(appliedClient, "applied"));

        var result = await controller.ApplyConfigAsync(AppConfig.Default);

        Assert.Equal(2, result.SessionCount);
        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.False(result.IsFullyApplied);
        Assert.Equal(1, failedClient.ApplyCount);
        Assert.Equal(1, appliedClient.ApplyCount);
    }

    [Fact]
    public async Task ApplyConfigSerializesTransactionsSoTheLatestCallWinsEveryRenderer()
    {
        await using var controller = new DesktopSessionController("runtime");
        var firstClient = new RuntimeSessionCdpClient();
        var secondClient = new RuntimeSessionCdpClient();
        AttachSession(controller, CreateRuntimeSession(firstClient, "one"));
        AttachSession(controller, CreateRuntimeSession(secondClient, "two"));
        var orderedClients = ControllerSessions(controller).Values
            .Select(session => session.Target.Id == "one" ? firstClient : secondClient)
            .ToArray();
        orderedClients[0].BlockFirstDiagnostics = true;

        var firstConfig = AppConfig.Default with
        {
            Assistant = AppConfig.Default.Assistant with { Name = "first-config" }
        };
        var secondConfig = AppConfig.Default with
        {
            Assistant = AppConfig.Default.Assistant with { Name = "second-config" }
        };
        var firstApply = controller.ApplyConfigAsync(firstConfig);
        await orderedClients[0].DiagnosticsBlocked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var secondApply = controller.ApplyConfigAsync(secondConfig);
        var earlySecondApply = await Task.WhenAny(
            orderedClients[0].SecondConfigApplied.Task,
            Task.Delay(150));
        Assert.NotSame(orderedClients[0].SecondConfigApplied.Task, earlySecondApply);

        orderedClients[0].ReleaseDiagnostics.TrySetResult();
        var results = await Task.WhenAll(firstApply, secondApply);

        Assert.All(results, result => Assert.True(result.IsFullyApplied));
        Assert.All(
            new[] { firstClient, secondClient },
            client => Assert.Equal("second-config", client.LastAppliedName));
    }

    [Fact]
    public async Task ApplyConfigFansOutToRendererSessionsConcurrently()
    {
        await using var controller = new DesktopSessionController("runtime");
        var entered = 0;
        var bothEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Barrier(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                bothEntered.TrySetResult();
            }
            await release.Task.WaitAsync(cancellationToken);
        }

        var firstClient = new RuntimeSessionCdpClient { BeforeApplyAsync = Barrier };
        var secondClient = new RuntimeSessionCdpClient { BeforeApplyAsync = Barrier };
        AttachSession(controller, CreateRuntimeSession(firstClient, "one"));
        AttachSession(controller, CreateRuntimeSession(secondClient, "two"));

        var apply = controller.ApplyConfigAsync(AppConfig.Default);
        try
        {
            await bothEntered.Task.WaitAsync(TimeSpan.FromMilliseconds(500));
        }
        finally
        {
            release.TrySetResult();
        }
        var result = await apply;

        Assert.True(result.IsFullyApplied);
        Assert.Equal(2, entered);
    }

    [Fact]
    public async Task RuntimeSessionDoesNotReloadAHealthyRuntime()
    {
        var client = new FakeCdpClient(
            """{"result":{"result":{"type":"object","value":{"active":true}}}}""");
        var session = new RuntimeTargetSession(
            new CdpTarget("target", "page", "Codex", "app://-/index.html", null),
            client,
            "__mc_test",
            "globalThis.__mustNotRun=true;",
            "{}");

        Assert.True(await session.EnsureActiveAsync());
        Assert.Single(client.Calls);
        Assert.DoesNotContain(
            "__mustNotRun",
            client.Calls[0].Parameters.GetProperty("expression").GetString());
    }

    [Fact]
    public async Task TargetDiscoveryPrefersVisibleConversationEvidence()
    {
        var background = new CdpTarget(
            "background",
            "page",
            "Codex",
            "app://-/index.html",
            null);
        var conversation = new CdpTarget(
            "conversation",
            "page",
            "Codex",
            "app://-/index.html",
            null);
        var connection = new FakeDesktopDebugConnection(
            [background, conversation],
            new Dictionary<string, string>
            {
                ["background"] = ProbeResponse(
                    "hidden",
                    conversationSurfaces: 0,
                    turns: 0,
                    units: 0,
                    userBubbles: 0),
                ["conversation"] = ProbeResponse(
                    "visible",
                    conversationSurfaces: 1,
                    turns: 2,
                    units: 4,
                    userBubbles: 2)
            });

        var candidates = await new TargetDiscoveryService().DiscoverAsync(connection);

        Assert.Equal("conversation", candidates[0].Target.Id);
        Assert.True(candidates[0].HasConversationEvidence);
        Assert.True(candidates[0].Score >= 55);
        Assert.False(candidates[1].HasConversationEvidence);
        Assert.True(candidates[1].Score < 55);
    }

    private static string ProbeResponse(
        string visibility,
        int conversationSurfaces,
        int turns,
        int units,
        int userBubbles) =>
        JsonSerializer.Serialize(new
        {
            result = new
            {
                result = new
                {
                    value = new
                    {
                        readyState = "complete",
                        bodyChildCount = 2,
                        hasDocument = true,
                        visibilityState = visibility,
                        conversationSurfaceCount = conversationSurfaces,
                        turnCount = turns,
                        unitCount = units,
                        userBubbleCount = userBubbles
                    }
                }
            }
        });

    private static string DiagnosticsResponse(string errors = "[]") =>
        JsonSerializer.Serialize(new
        {
            result = new
            {
                result = new
                {
                    type = "object",
                    value = new
                    {
                        version = "0.99.2",
                        protocolVersion = 1,
                        installed = true,
                        compatibility = "compatible",
                        scannedTurns = 2,
                        identifiedUserTurns = 1,
                        decoratedUserTurns = 1,
                        decoratedAssistantTurns = 1,
                        assistantBubbleBlocks = 1,
                        unknownTurns = 0,
                        averageConfidence = 1,
                        observerActive = true,
                        lastRefreshAt = "2026-08-06T00:00:00Z",
                        errors = JsonSerializer.Deserialize<JsonElement>(errors)
                    }
                }
            }
        });

    private static RuntimeTargetSession CreateRuntimeSession(
        ICdpClient client,
        string targetId = "target") =>
        new(
            new CdpTarget(targetId, "page", "Codex", "app://-/index.html", null),
            client,
            "__mc_test",
            "globalThis.__runtimeReloaded=true;",
            "{}");

    private static void AttachSession(
        DesktopSessionController controller,
        RuntimeTargetSession session)
    {
        var targetId = session.Target.Id ??
            throw new InvalidOperationException("A test renderer must have a target id.");
        Assert.True(ControllerSessions(controller).TryAdd(targetId, session));
    }

    private static ConcurrentDictionary<string, RuntimeTargetSession> ControllerSessions(
        DesktopSessionController controller) =>
        (ConcurrentDictionary<string, RuntimeTargetSession>)
        typeof(DesktopSessionController)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!;

    private sealed class FakeCdpClient : ICdpClient
    {
        private readonly Queue<JsonElement> _responses;

        public FakeCdpClient(params string[] responses)
        {
            _responses = new Queue<JsonElement>(
                responses.Select(response =>
                    JsonDocument.Parse(response).RootElement.Clone()));
        }

        public event EventHandler<JsonElement>? EventReceived
        {
            add { }
            remove { }
        }

        public List<(string Method, JsonElement Parameters)> Calls { get; } = [];

        public Task ConnectAsync(
            Uri webSocketUri,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<JsonElement> SendCommandAsync(
            string method,
            object? parameters = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var serialized = JsonSerializer.SerializeToElement(parameters ?? new { });
            Calls.Add((method, serialized));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No fake CDP response is queued.");
            }
            return Task.FromResult(_responses.Dequeue());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RuntimeSessionCdpClient : ICdpClient
    {
        private int _diagnosticsCalls;

        public bool ApplyHasErrors { get; init; }
        public bool BlockFirstDiagnostics { get; set; }
        public Func<CancellationToken, Task>? BeforeApplyAsync { get; init; }
        public int ApplyCount { get; private set; }
        public string? LastAppliedName { get; private set; }
        public TaskCompletionSource DiagnosticsBlocked { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDiagnostics { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondConfigApplied { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event EventHandler<JsonElement>? EventReceived
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(
            Uri webSocketUri,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public async Task<JsonElement> SendCommandAsync(
            string method,
            object? parameters = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var serialized = JsonSerializer.SerializeToElement(parameters ?? new { });
            if (method == "Page.addScriptToEvaluateOnNewDocument")
            {
                return Parse("""{"result":{"identifier":"registered"}}""");
            }
            if (method == "Page.removeScriptToEvaluateOnNewDocument")
            {
                return Parse("""{"result":{}}""");
            }
            var expression = serialized.GetProperty("expression").GetString() ?? string.Empty;
            if (expression.Contains("applyConfig(", StringComparison.Ordinal))
            {
                ApplyCount++;
                if (BeforeApplyAsync is not null)
                {
                    await BeforeApplyAsync(cancellationToken);
                }
                LastAppliedName = expression.Contains("second-config", StringComparison.Ordinal)
                    ? "second-config"
                    : expression.Contains("first-config", StringComparison.Ordinal)
                        ? "first-config"
                        : AppConfig.Default.Assistant.Name;
                if (LastAppliedName == "second-config")
                {
                    SecondConfigApplied.TrySetResult();
                }
                return Parse(DiagnosticsResponse(
                    ApplyHasErrors
                        ? """[{"code":"refresh","at":"2026-08-06T00:00:00Z"}]"""
                        : "[]"));
            }
            if (expression.Contains("ensureActive", StringComparison.Ordinal))
            {
                return Parse(
                    """{"result":{"result":{"type":"object","value":{"active":true}}}}""");
            }
            if (expression.Contains("getDiagnostics", StringComparison.Ordinal))
            {
                if (BlockFirstDiagnostics && Interlocked.Increment(ref _diagnosticsCalls) == 1)
                {
                    DiagnosticsBlocked.TrySetResult();
                    await ReleaseDiagnostics.Task.WaitAsync(cancellationToken);
                }
                return Parse(DiagnosticsResponse());
            }
            throw new InvalidOperationException($"Unexpected CDP expression: {expression}");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static JsonElement Parse(string json) =>
            JsonDocument.Parse(json).RootElement.Clone();
    }

    private sealed class ScriptReplacementCdpClient : ICdpClient
    {
        public bool FailAdd { get; init; }
        public bool CancelAdd { get; init; }
        public bool FailRemove { get; init; }
        public Queue<string> RegistrationIdentifiers { get; } = [];
        public Dictionary<string, int> RemoveFailuresRemaining { get; } = [];
        public List<string> RemovedIdentifiers { get; } = [];

        public event EventHandler<JsonElement>? EventReceived
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(
            Uri webSocketUri,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<JsonElement> SendCommandAsync(
            string method,
            object? parameters = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (method == "Runtime.evaluate")
            {
                return Task.FromResult(Parse(DiagnosticsResponse()));
            }
            if (method == "Page.addScriptToEvaluateOnNewDocument")
            {
                if (CancelAdd)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                if (FailAdd)
                {
                    throw new InvalidOperationException("Replacement registration failed.");
                }
                var identifier = RegistrationIdentifiers.TryDequeue(out var queued)
                    ? queued
                    : "latest-source";
                return Task.FromResult(
                    JsonSerializer.SerializeToElement(new
                    {
                        result = new { identifier }
                    }));
            }
            if (method == "Page.removeScriptToEvaluateOnNewDocument")
            {
                var serialized = JsonSerializer.SerializeToElement(parameters ?? new { });
                RemovedIdentifiers.Add(
                    serialized.GetProperty("identifier").GetString() ?? string.Empty);
                var identifier = RemovedIdentifiers[^1];
                var hasScheduledFailure =
                    RemoveFailuresRemaining.TryGetValue(identifier, out var remaining) &&
                    remaining > 0;
                if (FailRemove || hasScheduledFailure)
                {
                    if (hasScheduledFailure)
                    {
                        RemoveFailuresRemaining[identifier] = remaining - 1;
                    }
                    throw new InvalidOperationException("Previous removal failed.");
                }
                return Task.FromResult(Parse("""{"result":{}}"""));
            }
            throw new InvalidOperationException($"Unexpected CDP method: {method}");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static JsonElement Parse(string json) =>
            JsonDocument.Parse(json).RootElement.Clone();
    }

    private sealed class FakeDesktopDebugConnection : IDesktopDebugConnection
    {
        private readonly IReadOnlyList<CdpTarget> _targets;
        private readonly IReadOnlyDictionary<string, string> _responses;

        public FakeDesktopDebugConnection(
            IReadOnlyList<CdpTarget> targets,
            IReadOnlyDictionary<string, string> responses)
        {
            _targets = targets;
            _responses = responses;
        }

        public DesktopDebugTransport Transport => DesktopDebugTransport.Pipe;
        public int? LoopbackPort => null;
        public event EventHandler? TargetsChanged
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<CdpTarget>> ListTargetsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_targets);

        public Task<ICdpClient> OpenTargetAsync(
            CdpTarget target,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ICdpClient>(
                new FakeCdpClient(_responses[target.Id!]));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
