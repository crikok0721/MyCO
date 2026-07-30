using System.Net.WebSockets;
using System.Text.Json;

// Minimal Chrome DevTools Protocol client used for one renderer WebSocket.
namespace MyCO.Cdp;

public interface ICdpClient : IAsyncDisposable
{
    event EventHandler<JsonElement>? EventReceived;

    Task ConnectAsync(Uri webSocketUri, CancellationToken cancellationToken = default);

    Task<JsonElement> SendCommandAsync(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}

public sealed class CdpClient : ICdpClient
{
    public const int MaximumMessageBytes = 32 * 1024 * 1024;
    private readonly ClientWebSocket _socket = new();
    private readonly CdpMessageCorrelator _correlator = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private long _nextId;
    private Task? _receiveTask;

    public CdpClient()
    {
        // CDP is always loopback-only; system proxies must never intercept it.
        _socket.Options.Proxy = null;
    }

    public event EventHandler<JsonElement>? EventReceived;

    public async Task ConnectAsync(
        Uri webSocketUri,
        CancellationToken cancellationToken = default)
    {
        await _socket.ConnectAsync(webSocketUri, cancellationToken).ConfigureAwait(false);
        _receiveTask = ReceiveLoopAsync(_lifetime.Token);
    }

    public async Task<JsonElement> SendCommandAsync(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (_socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("CDP WebSocket is not connected.");
        }

        // CDP replies carry the request id, while messages without an id are events.
        var id = Interlocked.Increment(ref _nextId);
        var completion = _correlator.Register(id);

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id,
            method,
            @params = parameters ?? new { }
        });
        if (payload.Length > MaximumMessageBytes)
        {
            _correlator.Remove(id);
            throw new InvalidOperationException(
                "CDP command exceeds the safe message limit.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetime.Token);
        timeoutSource.CancelAfter(timeout ?? TimeSpan.FromSeconds(10));

        try
        {
            await _sendGate.WaitAsync(timeoutSource.Token).ConfigureAwait(false);
            try
            {
                await _socket.SendAsync(
                    payload,
                    WebSocketMessageType.Text,
                    true,
                    timeoutSource.Token).ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }
            return await completion.WaitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        finally
        {
            _correlator.Remove(id);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _correlator.CancelAll();

        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "MyCO probe complete",
                    closeTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is WebSocketException or OperationCanceledException)
            {
                _socket.Abort();
            }
        }
        else
        {
            _socket.Abort();
        }

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal.
            }
            catch (WebSocketException)
            {
                // The peer can close first during renderer teardown.
            }
            catch (Exception exception) when (
                exception is InvalidDataException or JsonException)
            {
                // A malformed or oversized peer message already terminated the receive loop.
            }
        }

        _socket.Dispose();
        _sendGate.Dispose();
        _lifetime.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        // WebSocket messages may arrive in several frames, so assemble one JSON payload first.
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested &&
               _socket.State is WebSocketState.Open or WebSocketState.CloseSent)
        {
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                if (message.Length + result.Count > MaximumMessageBytes)
                {
                    throw new InvalidDataException(
                        "CDP WebSocket message exceeded the safe limit.");
                }
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            using var document = JsonDocument.Parse(message.ToArray());
            var root = document.RootElement.Clone();
            message.SetLength(0);

            if (_correlator.TryHandle(root))
            {
                continue;
            }
            // Unmatched messages are CDP events such as Runtime.bindingCalled.
            try
            {
                EventReceived?.Invoke(this, root);
            }
            catch (Exception)
            {
                // A consumer cannot be allowed to terminate the shared receive loop.
            }
        }
    }
}
