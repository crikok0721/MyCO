using System.Text.Json;

namespace MyCodex.Cdp;

// Browser-level CDP connection using Chromium's null-delimited pipe protocol.
public sealed class PipeCdpConnection : IAsyncDisposable
{
    public const int MaximumMessageBytes = 32 * 1024 * 1024;

    private readonly Stream _browserOutput;
    private readonly Stream _browserInput;
    private readonly CdpMessageCorrelator _correlator = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _receiveTask;
    private long _nextId;
    private int _targetDiscoveryEnabled;
    private bool _disposed;

    public PipeCdpConnection(Stream browserOutput, Stream browserInput)
    {
        _browserOutput = browserOutput;
        _browserInput = browserInput;
        _receiveTask = ReceiveLoopAsync(_lifetime.Token);
    }

    public event EventHandler<JsonElement>? EventReceived;

    public async Task<JsonElement> SendCommandAsync(
        string method,
        object? parameters = null,
        string? sessionId = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var id = Interlocked.Increment(ref _nextId);
        var completion = _correlator.Register(id);
        var envelope = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters ?? new { }
        };
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            envelope["sessionId"] = sessionId;
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(envelope);
        if (json.Length + 1 > MaximumMessageBytes)
        {
            _correlator.Remove(id);
            throw new InvalidOperationException("CDP command exceeds the safe message limit.");
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
                await _browserInput.WriteAsync(json, timeoutSource.Token)
                    .ConfigureAwait(false);
                await _browserInput.WriteAsync(
                    new byte[] { 0 },
                    timeoutSource.Token).ConfigureAwait(false);
                await _browserInput.FlushAsync(timeoutSource.Token).ConfigureAwait(false);
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

    public async Task<IReadOnlyList<CdpTarget>> ListTargetsAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _targetDiscoveryEnabled, 1) == 0)
        {
            try
            {
                await SendCommandAsync(
                    "Target.setDiscoverTargets",
                    new { discover = true },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                Interlocked.Exchange(ref _targetDiscoveryEnabled, 0);
                throw;
            }
        }
        var response = await SendCommandAsync(
            "Target.getTargets",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.GetProperty("result")
            .GetProperty("targetInfos")
            .EnumerateArray()
            .Select(info => new CdpTarget(
                info.GetProperty("targetId").GetString(),
                info.GetProperty("type").GetString(),
                info.GetProperty("title").GetString(),
                info.GetProperty("url").GetString(),
                null))
            .ToArray();
    }

    public async Task<ICdpClient> AttachAsync(
        CdpTarget target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.Id))
        {
            throw new ArgumentException("CDP target has no identifier.", nameof(target));
        }
        var response = await SendCommandAsync(
            "Target.attachToTarget",
            new { targetId = target.Id, flatten = true },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var sessionId = response.GetProperty("result").GetProperty("sessionId").GetString()
                        ?? throw new InvalidOperationException(
                            "CDP target attachment returned no session identifier.");
        return new PipeTargetCdpClient(this, sessionId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _lifetime.Cancel();
        _correlator.CancelAll();
        await _browserInput.DisposeAsync().ConfigureAwait(false);
        await _browserOutput.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _receiveTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (IOException)
        {
            // Chromium may close the pipe first.
        }
        catch (ObjectDisposedException)
        {
            // Local stream disposal unblocks a pending read.
        }
        catch (Exception)
        {
            // Dispose is best effort; the command that observed a broken pipe
            // reports the transport failure before shutdown reaches this path.
        }
        _sendGate.Dispose();
        _lifetime.Dispose();
    }

    internal Task<JsonElement> SendSessionCommandAsync(
        string sessionId,
        string method,
        object? parameters,
        TimeSpan? timeout,
        CancellationToken cancellationToken) =>
        SendCommandAsync(method, parameters, sessionId, timeout, cancellationToken);

    internal async Task DetachAsync(string sessionId)
    {
        try
        {
            await SendCommandAsync(
                "Target.detachFromTarget",
                new { sessionId },
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or
                OperationCanceledException)
        {
            // A destroyed target is already detached.
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await _browserOutput.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }
            for (var index = 0; index < read; index++)
            {
                if (buffer[index] != 0)
                {
                    if (message.Length >= MaximumMessageBytes)
                    {
                        throw new InvalidDataException(
                            "CDP pipe message exceeded the safe limit.");
                    }
                    message.WriteByte(buffer[index]);
                    continue;
                }
                if (message.Length == 0)
                {
                    continue;
                }
                using var document = JsonDocument.Parse(message.ToArray());
                var root = document.RootElement.Clone();
                message.SetLength(0);
                if (!_correlator.TryHandle(root))
                {
                    try
                    {
                        EventReceived?.Invoke(this, root);
                    }
                    catch (Exception)
                    {
                        // A target consumer cannot terminate the browser receive loop.
                    }
                }
            }
        }
    }
}

internal sealed class PipeTargetCdpClient : ICdpClient
{
    private readonly PipeCdpConnection _connection;
    private readonly string _sessionId;
    private bool _disposed;

    public PipeTargetCdpClient(PipeCdpConnection connection, string sessionId)
    {
        _connection = connection;
        _sessionId = sessionId;
        _connection.EventReceived += HandleConnectionEvent;
    }

    public event EventHandler<JsonElement>? EventReceived;

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
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _connection.SendSessionCommandAsync(
            _sessionId,
            method,
            parameters,
            timeout,
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _connection.EventReceived -= HandleConnectionEvent;
        await _connection.DetachAsync(_sessionId).ConfigureAwait(false);
    }

    private void HandleConnectionEvent(object? sender, JsonElement message)
    {
        if (message.TryGetProperty("sessionId", out var session) &&
            string.Equals(
                session.GetString(),
                _sessionId,
                StringComparison.Ordinal))
        {
            EventReceived?.Invoke(this, message);
        }
    }
}
