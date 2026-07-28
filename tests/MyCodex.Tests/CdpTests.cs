using System.Text.Json;
using MyCodex.Cdp;
using MyCodex.Discovery;
using MyCodex.Injection;

// Verifies CDP reply correlation and safe runtime bootstrap source generation.
namespace MyCodex.Tests;

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
            """{"result":{"result":{"type":"object","value":{}}}}""",
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
