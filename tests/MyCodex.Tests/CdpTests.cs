using System.Text.Json;
using MyCodex.Cdp;
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
}
