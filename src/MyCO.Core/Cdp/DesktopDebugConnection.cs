using System.Diagnostics;

namespace MyCO.Cdp;

public enum DesktopDebugTransport
{
    Pipe,
    Tcp
}

// A transport-neutral browser connection. The controller owns this root connection,
// while each renderer session owns only the ICdpClient returned by OpenTargetAsync.
public interface IDesktopDebugConnection : IAsyncDisposable
{
    event EventHandler? TargetsChanged;

    DesktopDebugTransport Transport { get; }
    int? LoopbackPort { get; }

    Task<IReadOnlyList<CdpTarget>> ListTargetsAsync(
        CancellationToken cancellationToken = default);

    Task<ICdpClient> OpenTargetAsync(
        CdpTarget target,
        CancellationToken cancellationToken = default);
}

public sealed class PipeDesktopDebugConnection : IDesktopDebugConnection
{
    private readonly PipeCdpConnection _connection;

    public PipeDesktopDebugConnection(PipeCdpConnection connection)
    {
        _connection = connection;
        _connection.EventReceived += HandleConnectionEvent;
    }

    public event EventHandler? TargetsChanged;
    public DesktopDebugTransport Transport => DesktopDebugTransport.Pipe;
    public int? LoopbackPort => null;

    public Task<IReadOnlyList<CdpTarget>> ListTargetsAsync(
        CancellationToken cancellationToken = default) =>
        _connection.ListTargetsAsync(cancellationToken);

    public Task<ICdpClient> OpenTargetAsync(
        CdpTarget target,
        CancellationToken cancellationToken = default) =>
        _connection.AttachAsync(target, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _connection.EventReceived -= HandleConnectionEvent;
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    private void HandleConnectionEvent(object? sender, System.Text.Json.JsonElement message)
    {
        if (!message.TryGetProperty("method", out var methodProperty))
        {
            return;
        }
        var method = methodProperty.GetString();
        if (method is "Target.targetCreated" or "Target.targetDestroyed" or
            "Target.targetInfoChanged")
        {
            TargetsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed class TcpDesktopDebugConnection : IDesktopDebugConnection
{
    private readonly TargetHttpClient _targetClient;

    public TcpDesktopDebugConnection(int port, HttpClient? httpClient = null)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }
        LoopbackPort = port;
        _targetClient = new TargetHttpClient(port, httpClient);
    }

    public event EventHandler? TargetsChanged
    {
        add { }
        remove { }
    }

    public DesktopDebugTransport Transport => DesktopDebugTransport.Tcp;
    public int? LoopbackPort { get; }

    public Task<IReadOnlyList<CdpTarget>> ListTargetsAsync(
        CancellationToken cancellationToken = default) =>
        _targetClient.ListTargetsAsync(cancellationToken);

    public async Task<ICdpClient> OpenTargetAsync(
        CdpTarget target,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(
                target.WebSocketDebuggerUrl,
                UriKind.Absolute,
                out var socketUri) ||
            socketUri.Scheme is not ("ws" or "wss"))
        {
            throw new InvalidOperationException(
                "The renderer did not expose a valid debugger endpoint.");
        }
        var client = new CdpClient();
        try
        {
            await client.ConnectAsync(socketUri, cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        _targetClient.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class TargetHttpClient : IDisposable
    {
        private readonly int _port;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsClient;

        public TargetHttpClient(int port, HttpClient? httpClient)
        {
            _port = port;
            _httpClient = httpClient ?? new HttpClient(
                new SocketsHttpHandler { UseProxy = false });
            _ownsClient = httpClient is null;
        }

        public async Task<IReadOnlyList<CdpTarget>> ListTargetsAsync(
            CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(
                $"http://127.0.0.1:{_port}/json/list",
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(
                cancellationToken).ConfigureAwait(false);
            return await System.Text.Json.JsonSerializer.DeserializeAsync<CdpTarget[]>(
                       stream,
                       new System.Text.Json.JsonSerializerOptions(
                           System.Text.Json.JsonSerializerDefaults.Web),
                       cancellationToken).ConfigureAwait(false)
                   ?? [];
        }

        public void Dispose()
        {
            if (_ownsClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}

public static class DesktopDebugConnectionFactory
{
    public static (
        Process Process,
        IDesktopDebugConnection Connection) LaunchPipe(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        var directory = Path.GetDirectoryName(executablePath)
                        ?? Environment.CurrentDirectory;
        var launched = WindowsPipeProcessLauncher.Launch(
            executablePath,
            directory,
            arguments);
        try
        {
            var root = new PipeCdpConnection(
                launched.BrowserOutput,
                launched.BrowserInput);
            return (launched.Process, new PipeDesktopDebugConnection(root));
        }
        catch
        {
            launched.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }
    }

    public static (
        Process Process,
        IDesktopDebugConnection Connection) LaunchTcp(
        string executablePath,
        IReadOnlyList<string> arguments,
        int port)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory =
                Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        var process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException(
                          "Desktop process did not start.");
        return (process, new TcpDesktopDebugConnection(port));
    }
}
