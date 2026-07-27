using System.Collections.Concurrent;
using System.Diagnostics;
using MyCodex.Applications;
using MyCodex.Cdp;
using MyCodex.Configuration;
using MyCodex.Diagnostics;
using MyCodex.Discovery;

// Owns the desktop process, renderer sessions, reinjection monitor, and visible session state.
namespace MyCodex.Injection;

public sealed record DesktopSessionState(
    bool IsConnected,
    bool IsSkinEnabled,
    int? CdpPort,
    int TargetCount,
    string Status,
    string? ApplicationVersion,
    string? RuntimeVersion,
    DesktopDebugTransport? Transport = null,
    DesktopSessionPhase Phase = DesktopSessionPhase.Disconnected,
    string? LastErrorCode = null);

public enum DesktopSessionPhase
{
    Disconnected,
    Starting,
    Connected,
    Stopping,
    Faulted
}

public sealed class DesktopSessionController : IAsyncDisposable
{
    private readonly string _runtimeScript;
    private readonly TargetDiscoveryService _targetDiscovery;
    private readonly IInjectionBackend _injectionBackend;
    private readonly IPrivacySafeLogger _logger;
    private readonly ConcurrentDictionary<string, RuntimeTargetSession> _sessions =
        new(StringComparer.Ordinal);
    // Renderer discovery runs in the background, so every access to _sessions is serialized.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private Process? _launchedProcess;
    private IDesktopDebugConnection? _connection;
    private DesktopSessionPhase _phase = DesktopSessionPhase.Disconnected;
    private string? _applicationVersion;
    private string? _runtimeVersion;
    private string? _lastErrorCode;
    private AppConfig _config = AppConfig.Default;
    private bool _skinEnabled;

    public DesktopSessionController(
        string runtimeScript,
        TargetDiscoveryService? targetDiscovery = null,
        IInjectionBackend? injectionBackend = null,
        IPrivacySafeLogger? logger = null)
    {
        _runtimeScript = runtimeScript;
        _targetDiscovery = targetDiscovery ?? new TargetDiscoveryService();
        _injectionBackend = injectionBackend ?? new CdpInjectionBackend();
        var paths = new ConfigPaths();
        _logger = logger ?? new PrivacySafeLogger(paths.LogsDirectory);
    }

    public event EventHandler<DesktopSessionState>? StateChanged;
    public event EventHandler<RuntimeHostEvent>? RuntimeEventReceived;

    public DesktopSessionState State { get; private set; } =
        new(false, false, null, 0, "Not connected", null, null);

    public async Task StartAsync(
        ApplicationCandidate candidate,
        IApplicationAdapter adapter,
        AppConfig config,
        DesktopDebugTransport transport = DesktopDebugTransport.Pipe,
        CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is not null ||
                _phase is DesktopSessionPhase.Starting or DesktopSessionPhase.Stopping)
            {
                throw new InvalidOperationException(
                    "A Desktop session is already active or changing state.");
            }
            // CDP flags only take effect at process start; attaching to a normal process fails.
            if (candidate.IsRunning)
            {
                throw new ApplicationAlreadyRunningException(candidate);
            }
            if (string.IsNullOrWhiteSpace(candidate.ExecutablePath) ||
                !File.Exists(candidate.ExecutablePath))
            {
                throw new FileNotFoundException(
                    "Desktop executable was not found.",
                    candidate.ExecutablePath);
            }

            _phase = DesktopSessionPhase.Starting;
            _lastErrorCode = null;
            _applicationVersion = candidate.Version;
            _config = config;
            _skinEnabled = true;
            UpdateState("Starting Desktop");

            try
            {
                if (transport == DesktopDebugTransport.Pipe)
                {
                    (_launchedProcess, _connection) =
                        DesktopDebugConnectionFactory.LaunchPipe(
                            candidate.ExecutablePath,
                            adapter.BuildPipeLaunchArguments());
                }
                else
                {
                    var port = PortAllocator.GetRandomLoopbackPort();
                    (_launchedProcess, _connection) =
                        DesktopDebugConnectionFactory.LaunchTcp(
                            candidate.ExecutablePath,
                            adapter.BuildLaunchArguments(port),
                            port);
                }

                _logger.Info("app_started", new Dictionary<string, object?>
                {
                    ["appVersion"] = candidate.Version,
                    ["adapter"] = adapter.Id,
                    ["cdpPort"] = _connection.LoopbackPort,
                    ["state"] = _connection.Transport.ToString()
                });

                await WaitForEndpointAsync(cancellationToken).ConfigureAwait(false);
                await RefreshSessionsAsync(cancellationToken).ConfigureAwait(false);
                _phase = DesktopSessionPhase.Connected;
                UpdateState(_sessions.Count > 0
                    ? "Skin active"
                    : "Safe mode: no compatible renderer");
                // New renderer targets can appear after navigation, so keep discovery running.
                _monitorCancellation = new CancellationTokenSource();
                _monitorTask = MonitorAsync(_monitorCancellation.Token);
            }
            catch (Exception exception)
            {
                var errorCode = ErrorCodeFactory.Create(
                    transport == DesktopDebugTransport.Pipe ? "PIPE" : "TCP",
                    "START");
                _lastErrorCode = errorCode;
                if (exception is not OperationCanceledException)
                {
                    _logger.Error(_lastErrorCode, exception);
                }
                await CleanupFailedStartAsync().ConfigureAwait(false);
                _phase = exception is OperationCanceledException
                    ? DesktopSessionPhase.Disconnected
                    : DesktopSessionPhase.Faulted;
                if (exception is OperationCanceledException)
                {
                    _lastErrorCode = null;
                }
                UpdateState(exception is OperationCanceledException
                    ? "Disconnected"
                    : "Connection failed");
                if (exception is OperationCanceledException)
                {
                    throw;
                }
                if (transport == DesktopDebugTransport.Pipe)
                {
                    throw new SecureTransportUnavailableException(
                        errorCode,
                        exception);
                }
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task EnableSkinAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("No CDP session is connected.");
        }
        _skinEnabled = true;
        await RefreshSessionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DisableSkinAsync(CancellationToken cancellationToken = default)
    {
        _skinEnabled = false;
        RuntimeTargetSession[] sessions;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }
        finally
        {
            _gate.Release();
        }
        // Destroy outside the lock because CDP calls can take time or fail during navigation.
        foreach (var session in sessions)
        {
            await session.DestroyAsync(cancellationToken).ConfigureAwait(false);
        }
        UpdateState("Skin disabled");
    }

    public async Task ApplyConfigAsync(
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        _config = config;
        RuntimeTargetSession[] sessions;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            sessions = _sessions.Values.ToArray();
        }
        finally
        {
            _gate.Release();
        }
        foreach (var session in sessions)
        {
            await session.ApplyConfigAsync(config, cancellationToken).ConfigureAwait(false);
        }
        UpdateState("Appearance applied");
    }

    public async Task StartCalibrationAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        RuntimeTargetSession? session;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            session = _sessions.Values.FirstOrDefault();
        }
        finally
        {
            _gate.Release();
        }
        if (session is null)
        {
            throw new InvalidOperationException("No renderer is attached.");
        }
        await session.StartCalibrationAsync(role, cancellationToken).ConfigureAwait(false);
        UpdateState($"Select a {role} turn in Codex");
    }

    public async Task<IReadOnlyList<System.Text.Json.JsonElement>> GetDiagnosticsAsync(
        CancellationToken cancellationToken = default)
    {
        RuntimeTargetSession[] sessions;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            sessions = _sessions.Values.ToArray();
        }
        finally
        {
            _gate.Release();
        }
        var results = new List<System.Text.Json.JsonElement>();
        foreach (var session in sessions)
        {
            results.Add(await session.GetDiagnosticsAsync(cancellationToken)
                .ConfigureAwait(false));
        }
        return results;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _phase = DesktopSessionPhase.Stopping;
            UpdateState("Disconnecting");
            if (_monitorCancellation is not null)
            {
                _monitorCancellation.Cancel();
            }
            if (_monitorTask is not null)
            {
                try
                {
                    await _monitorTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected.
                }
            }
            await DisableSkinAsync(cancellationToken).ConfigureAwait(false);
            _monitorCancellation?.Dispose();
            _monitorCancellation = null;
            _monitorTask = null;
            if (_connection is not null)
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }
            _launchedProcess?.Dispose();
            _launchedProcess = null;
            _phase = DesktopSessionPhase.Disconnected;
            _applicationVersion = null;
            _runtimeVersion = null;
            _lastErrorCode = null;
            UpdateState("Disconnected");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Dispose();
        _lifecycleGate.Dispose();
    }

    private async Task WaitForEndpointAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            while (!timeout.IsCancellationRequested)
            {
                try
                {
                    if (_connection is null)
                    {
                        throw new InvalidOperationException(
                            "The root debugger connection is not available.");
                    }
                    var targets = await _connection.ListTargetsAsync(timeout.Token)
                        .ConfigureAwait(false);
                    if (targets.Count > 0)
                    {
                        return;
                    }
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or IOException or
                        InvalidOperationException or TaskCanceledException)
                {
                    // Retry until timeout.
                }
                await Task.Delay(250, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Desktop did not expose a CDP renderer in time.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException("Desktop did not expose a CDP renderer in time.");
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        // A small polling loop repairs renderer replacement without touching the desktop process.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.5));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await RefreshSessionsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException)
            {
                _logger.Error("target_monitor_error", exception);
                UpdateState("Renderer reconnect pending");
            }
        }
    }

    private async Task RefreshSessionsAsync(CancellationToken cancellationToken)
    {
        if (!_skinEnabled || _connection is null)
        {
            return;
        }
        var targets = await _targetDiscovery.DiscoverAsync(_connection, cancellationToken)
            .ConfigureAwait(false);
        // The score threshold deliberately rejects uncertain/background renderers.
        var eligible = targets
            .Where(candidate => candidate.Score >= 55 && candidate.Target.Id is not null)
            .ToDictionary(candidate => candidate.Target.Id!, candidate => candidate);
        RuntimeTargetSession[] removed;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var removedIds = _sessions.Keys.Where(id => !eligible.ContainsKey(id)).ToArray();
            removed = removedIds.Select(id => _sessions[id]).ToArray();
            foreach (var id in removedIds)
            {
                _sessions.TryRemove(id, out _);
            }
        }
        finally
        {
            _gate.Release();
        }
        foreach (var session in removed)
        {
            await session.DestroyAsync(CancellationToken.None).ConfigureAwait(false);
        }

        foreach (var pair in eligible)
        {
            RuntimeTargetSession? existing;
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _sessions.TryGetValue(pair.Key, out existing);
            }
            finally
            {
                _gate.Release();
            }

            if (existing is not null)
            {
                // Health checks repair lost style/observer state before a full reinjection.
                var healthy = false;
                try
                {
                    healthy = await existing.EnsureActiveAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException)
                {
                    _logger.Error("runtime_health_check_failed", exception);
                }
                if (healthy)
                {
                    continue;
                }

                await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (_sessions.TryGetValue(pair.Key, out var current) &&
                        ReferenceEquals(current, existing))
                    {
                        _sessions.TryRemove(pair.Key, out _);
                    }
                }
                finally
                {
                    _gate.Release();
                }
                existing.HostEventReceived -= HandleRuntimeEvent;
                await existing.DestroyAsync(CancellationToken.None).ConfigureAwait(false);
            }

            // Only missing or unhealthy sessions reach this injection path.
            RuntimeInjectionResult result;
            ICdpClient? client = null;
            try
            {
                client = await _connection.OpenTargetAsync(
                    pair.Value.Target,
                    cancellationToken).ConfigureAwait(false);
                result = await _injectionBackend.InjectAsync(
                    pair.Value.Target,
                    client,
                    _runtimeScript,
                    _config,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (client is not null)
                {
                    await client.DisposeAsync().ConfigureAwait(false);
                }
                if (exception is OperationCanceledException)
                {
                    throw;
                }
                _logger.Error("runtime_client_open_failed", exception);
                continue;
            }
            if (result.Passed && result.Session is not null)
            {
                result.Session.HostEventReceived += HandleRuntimeEvent;
                await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    _sessions[pair.Key] = result.Session;
                }
                finally
                {
                    _gate.Release();
                }
                _logger.Info("runtime_injected", new Dictionary<string, object?>
                {
                    ["runtimeVersion"] = result.Handshake?.Version,
                    ["targetCount"] = _sessions.Count
                });
                _runtimeVersion = result.Handshake?.Version;
            }
            else
            {
                _logger.Error(
                    "runtime_injection_failed",
                    new InvalidOperationException(result.Error ?? result.Status));
            }
        }
        UpdateState(_sessions.Count > 0 ? "Skin active" : "Safe mode: no compatible renderer");
    }

    private void HandleRuntimeEvent(object? sender, RuntimeHostEvent hostEvent)
    {
        RuntimeEventReceived?.Invoke(this, hostEvent);
    }

    private void UpdateState(string status)
    {
        State = new DesktopSessionState(
            _connection is not null && _phase == DesktopSessionPhase.Connected,
            _skinEnabled && _sessions.Count > 0,
            _connection?.LoopbackPort,
            _sessions.Count,
            status,
            _applicationVersion,
            _runtimeVersion,
            _connection?.Transport,
            _phase,
            _lastErrorCode);
        StateChanged?.Invoke(this, State);
    }

    private async Task CleanupFailedStartAsync()
    {
        RuntimeTargetSession[] sessions;
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }
        finally
        {
            _gate.Release();
        }
        foreach (var session in sessions)
        {
            try
            {
                await session.DestroyAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error("failed_session_cleanup", exception);
            }
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
        if (_launchedProcess is not null)
        {
            try
            {
                if (!_launchedProcess.HasExited)
                {
                    _launchedProcess.Kill(entireProcessTree: true);
                    await _launchedProcess.WaitForExitAsync().WaitAsync(
                        TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                    System.ComponentModel.Win32Exception or TimeoutException)
            {
                _logger.Error("failed_start_cleanup", exception);
            }
            finally
            {
                _launchedProcess.Dispose();
                _launchedProcess = null;
            }
        }
        _skinEnabled = false;
    }
}

public sealed class ApplicationAlreadyRunningException : InvalidOperationException
{
    public ApplicationAlreadyRunningException(ApplicationCandidate candidate)
        : base($"{candidate.DisplayName} is already running and must be restarted with MyCodex.")
    {
        Candidate = candidate;
    }

    public ApplicationCandidate Candidate { get; }
}

public sealed class SecureTransportUnavailableException : InvalidOperationException
{
    public SecureTransportUnavailableException(
        string errorCode,
        Exception innerException)
        : base("The secure local pipe transport could not be established.", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
