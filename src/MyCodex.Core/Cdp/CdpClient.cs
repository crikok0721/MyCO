using System.Net.WebSockets;
using System.Text.Json;

// Minimal Chrome DevTools Protocol client used for one renderer WebSocket.
namespace MyCodex.Cdp;

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
    private readonly ClientWebSocket _socket = new();
    private readonly CdpMessageCorrelator _correlator = new();
    private readonly CancellationTokenSource _lifetime = new();
    private long _nextId;
    private Task? _receiveTask;

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

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetime.Token);
        timeoutSource.CancelAfter(timeout ?? TimeSpan.FromSeconds(10));

        try
        {
            await _socket.SendAsync(
                payload,
                WebSocketMessageType.Text,
                true,
                timeoutSource.Token).ConfigureAwait(false);
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
            try
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "MyCodex probe complete",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                _socket.Abort();
            }
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
        }

        _socket.Dispose();
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
            EventReceived?.Invoke(this, root);
        }
    }
}
