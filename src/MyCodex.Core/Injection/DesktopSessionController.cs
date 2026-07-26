using System.Diagnostics;
using MyCodex.Applications;
using MyCodex.Cdp;
using MyCodex.Configuration;
using MyCodex.Diagnostics;
using MyCodex.Discovery;

namespace MyCodex.Injection;

public sealed record DesktopSessionState(
    bool IsConnected,
    bool IsSkinEnabled,
    int? CdpPort,
    int TargetCount,
    string Status,
    string? ApplicationVersion,
    string? RuntimeVersion);

public sealed class DesktopSessionController : IAsyncDisposable
{
    private readonly string _runtimeScript;
    private readonly TargetDiscoveryService _targetDiscovery;
    private readonly IInjectionBackend _injectionBackend;
    private readonly IPrivacySafeLogger _logger;
    private readonly Dictionary<string, RuntimeTargetSession> _sessions =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private Process? _launchedProcess;
    private int? _port;
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
        CancellationToken cancellationToken = default)
    {
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

        _config = config;
        _skinEnabled = true;
        _port = PortAllocator.GetRandomLoopbackPort();
        var startInfo = new ProcessStartInfo
        {
            FileName = candidate.ExecutablePath,
            WorkingDirectory =
                Path.GetDirectoryName(candidate.ExecutablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false
        };
        foreach (var argument in adapter.BuildLaunchArguments(_port.Value))
        {
            startInfo.ArgumentList.Add(argument);
        }
        _launchedProcess = Process.Start(startInfo)
                           ?? throw new InvalidOperationException("Desktop process did not start.");
        _logger.Info("app_started", new Dictionary<string, object?>
        {
            ["appVersion"] = candidate.Version,
            ["adapter"] = adapter.Id,
            ["cdpPort"] = _port.Value
        });

        await WaitForEndpointAsync(_port.Value, cancellationToken).ConfigureAwait(false);
        await RefreshSessionsAsync(cancellationToken).ConfigureAwait(false);
        _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = MonitorAsync(_monitorCancellation.Token);
    }

    public async Task AttachAsync(
        int port,
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        _port = port;
        _config = config;
        _skinEnabled = true;
        await WaitForEndpointAsync(port, cancellationToken).ConfigureAwait(false);
        await RefreshSessionsAsync(cancellationToken).ConfigureAwait(false);
        _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = MonitorAsync(_monitorCancellation.Token);
    }

    public async Task EnableSkinAsync(CancellationToken cancellationToken = default)
    {
        if (_port is null)
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
        _port = null;
        _launchedProcess?.Dispose();
        _launchedProcess = null;
        UpdateState("Disconnected");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task WaitForEndpointAsync(
        int port,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                var targets = await _targetDiscovery.ListTargetsAsync(port, timeout.Token)
                    .ConfigureAwait(false);
                if (targets.Count > 0)
                {
                    return;
                }
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException)
            {
                // Retry until timeout.
            }
            await Task.Delay(250, timeout.Token).ConfigureAwait(false);
        }
        throw new TimeoutException("Desktop did not expose a CDP renderer in time.");
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.5));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await RefreshSessionsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or InvalidOperationException)
            {
                _logger.Error("target_monitor_error", exception);
                UpdateState("Renderer reconnect pending");
            }
        }
    }

    private async Task RefreshSessionsAsync(CancellationToken cancellationToken)
    {
        if (!_skinEnabled || _port is null)
        {
            return;
        }
        var targets = await _targetDiscovery.DiscoverAsync(_port.Value, cancellationToken)
            .ConfigureAwait(false);
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
                _sessions.Remove(id);
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
                        _sessions.Remove(pair.Key);
                    }
                }
                finally
                {
                    _gate.Release();
                }
                existing.HostEventReceived -= HandleRuntimeEvent;
                await existing.DestroyAsync(CancellationToken.None).ConfigureAwait(false);
            }

            var result = await _injectionBackend.InjectAsync(
                pair.Value.Target,
                _runtimeScript,
                _config,
                cancellationToken).ConfigureAwait(false);
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
            _port is not null,
            _skinEnabled && _sessions.Count > 0,
            _port,
            _sessions.Count,
            status,
            null,
            null);
        StateChanged?.Invoke(this, State);
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
