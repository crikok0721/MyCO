using System.Collections.Concurrent;
using System.Diagnostics;
using MyCO.Applications;
using MyCO.Cdp;
using MyCO.Configuration;
using MyCO.Diagnostics;
using MyCO.Discovery;

// Owns the desktop process, renderer sessions, reinjection monitor, and visible session state.
namespace MyCO.Injection;

public sealed record DesktopSessionState(
    bool IsConnected,
    bool IsSkinEnabled,
    bool IsSkinRequested,
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
    private const int RemovalFailureThreshold = 3;
    private readonly string _runtimeScript;
    private readonly TargetDiscoveryService _targetDiscovery;
    private readonly IInjectionBackend _injectionBackend;
    private readonly IPrivacySafeLogger _logger;
    private readonly ConcurrentDictionary<string, RuntimeTargetSession> _sessions =
        new(StringComparer.Ordinal);
    // Renderer discovery runs in the background, so every access to _sessions is serialized.
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Covers discovery, health checks, removal, and injection as one single-flight operation.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly Dictionary<string, int> _missingTargetCounts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _healthFailureCounts =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RuntimeSessionEvidence> _sessionEvidence =
        new(StringComparer.Ordinal);
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
    private int _eventRefreshQueued;
    private int _calibrationPending;
    private string? _calibrationRole;
    private DiscoveryLogSnapshot? _lastDiscoveryLog;

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
        new(false, false, false, null, 0, "Not connected", null, null);

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
            _lastDiscoveryLog = null;
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

                _connection.TargetsChanged += HandleTargetsChanged;
                _logger.Info("app_started", new Dictionary<string, object?>
                {
                    ["appVersion"] = candidate.Version,
                    ["adapter"] = adapter.Id,
                    ["cdpPort"] = _connection.LoopbackPort,
                    ["state"] = _connection.Transport.ToString()
                });

                await WaitForEndpointAsync(cancellationToken).ConfigureAwait(false);
                await WaitForRuntimeReadyAsync(cancellationToken).ConfigureAwait(false);
                _phase = DesktopSessionPhase.Connected;
                UpdateState(StatusFromEvidence());
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
                if (exception is DesktopProcessExitedBeforeReadyException or
                    DesktopRendererNotReadyException)
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
        UpdateState(StatusFromEvidence());
    }

    public async Task DisableSkinAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _skinEnabled = false;
            Interlocked.Exchange(ref _calibrationPending, 0);
            Volatile.Write(ref _calibrationRole, null);
            RuntimeTargetSession[] sessions;
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                sessions = _sessions.Values.ToArray();
                _sessions.Clear();
                _sessionEvidence.Clear();
                _missingTargetCounts.Clear();
                _healthFailureCounts.Clear();
            }
            finally
            {
                _gate.Release();
            }
            // Destroy outside the dictionary lock but inside the refresh transaction.
            foreach (var session in sessions)
            {
                session.HostEventReceived -= HandleRuntimeEvent;
                await session.DestroyAsync(cancellationToken).ConfigureAwait(false);
            }
            UpdateState("Skin disabled");
        }
        finally
        {
            _refreshGate.Release();
        }
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
            await UpdateEvidenceAsync(session, cancellationToken).ConfigureAwait(false);
        }
        UpdateState(StatusFromEvidence());
    }

    public async Task StartCalibrationAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        RuntimeTargetSession[] sessions;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            sessions = _sessions.Values
                .Where(candidate => EvidenceScore(candidate.Target.Id) > 0)
                .OrderByDescending(candidate =>
                    EvidenceScore(candidate.Target.Id))
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
        if (sessions.Length == 0)
        {
            throw new InvalidOperationException(
                "No compatible conversation renderer is attached.");
        }

        Interlocked.Exchange(ref _calibrationPending, 1);
        Volatile.Write(ref _calibrationRole, role);
        var started = 0;
        foreach (var session in sessions)
        {
            try
            {
                await session.StartCalibrationAsync(role, cancellationToken)
                    .ConfigureAwait(false);
                started++;
            }
            catch (OperationCanceledException)
            {
                Interlocked.Exchange(ref _calibrationPending, 0);
                Volatile.Write(ref _calibrationRole, null);
                throw;
            }
            catch (Exception exception)
            {
                _logger.Error("calibration_target_start_failed", exception);
            }
        }
        if (started == 0)
        {
            Interlocked.Exchange(ref _calibrationPending, 0);
            Volatile.Write(ref _calibrationRole, null);
            throw new InvalidOperationException(
                "Calibration could not be activated in a conversation renderer.");
        }
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
                _connection.TargetsChanged -= HandleTargetsChanged;
                await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }
            _launchedProcess?.Dispose();
            _launchedProcess = null;
            _phase = DesktopSessionPhase.Disconnected;
            Interlocked.Exchange(ref _calibrationPending, 0);
            Volatile.Write(ref _calibrationRole, null);
            _lastDiscoveryLog = null;
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
        _refreshGate.Dispose();
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
                ThrowIfLaunchedProcessExited();
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
                ThrowIfLaunchedProcessExited();
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

    private async Task WaitForRuntimeReadyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            while (!timeout.IsCancellationRequested)
            {
                ThrowIfLaunchedProcessExited();
                UpdateState("Waiting for Codex renderer");
                await RefreshSessionsAsync(timeout.Token).ConfigureAwait(false);
                if (_sessionEvidence.Values.Any(evidence => evidence.ObserverActive))
                {
                    return;
                }
                await Task.Delay(250, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DesktopRendererNotReadyException();
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new DesktopRendererNotReadyException();
    }

    private void ThrowIfLaunchedProcessExited()
    {
        if (_launchedProcess is null)
        {
            throw new InvalidOperationException(
                "The launched Desktop process is unavailable.");
        }
        if (!_launchedProcess.HasExited)
        {
            return;
        }
        throw new DesktopProcessExitedBeforeReadyException(
            _launchedProcess.ExitCode);
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
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_skinEnabled || _connection is null)
            {
                return;
            }
            var targets = await _targetDiscovery
                .DiscoverAsync(_connection, cancellationToken)
                .ConfigureAwait(false);
            // Empty visible conversations remain eligible through their composer/surface
            // evidence; background renderers no longer qualify on app:// alone.
            var hasConversationCandidate =
                targets.Any(candidate => candidate.HasConversationEvidence);
            var fallbackTargetId = hasConversationCandidate
                ? null
                : targets.FirstOrDefault(candidate =>
                    candidate.VisibilityState == "visible")?.Target.Id;
            var eligible = targets
                .Where(candidate =>
                    candidate.Score >= 55 &&
                    candidate.Target.Id is not null &&
                    (candidate.HasConversationEvidence ||
                     string.Equals(
                         candidate.Target.Id,
                         fallbackTargetId,
                         StringComparison.Ordinal)))
                .ToDictionary(candidate => candidate.Target.Id!, candidate => candidate);
            var discoveryLog = new DiscoveryLogSnapshot(
                targets.Count,
                eligible.Count,
                targets.Count(candidate => candidate.HasConversationEvidence),
                targets.Count(candidate => candidate.VisibilityState == "visible"));
            if (discoveryLog != _lastDiscoveryLog)
            {
                _lastDiscoveryLog = discoveryLog;
                _logger.Info("target_discovery_completed", new Dictionary<string, object?>
                {
                    ["candidateCount"] = discoveryLog.CandidateCount,
                    ["eligibleCount"] = discoveryLog.EligibleCount,
                    ["conversationTargets"] = discoveryLog.ConversationTargets,
                    ["visibleTargets"] = discoveryLog.VisibleTargets
                });
            }

            var removed = new List<RuntimeTargetSession>();
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var id in _sessions.Keys)
                {
                    if (eligible.ContainsKey(id))
                    {
                        _missingTargetCounts.Remove(id);
                        continue;
                    }
                    var failures = Increment(_missingTargetCounts, id);
                    if (failures < RemovalFailureThreshold)
                    {
                        continue;
                    }
                    if (_sessions.TryRemove(id, out var session))
                    {
                        removed.Add(session);
                    }
                    ForgetTarget(id);
                }
            }
            finally
            {
                _gate.Release();
            }
            foreach (var session in removed)
            {
                try
                {
                    session.HostEventReceived -= HandleRuntimeEvent;
                    await session.DestroyAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException)
                {
                    _logger.Error("stale_session_cleanup_failed", exception);
                }
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
                    // Transient navigation failures get a grace window before teardown.
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
                        _healthFailureCounts.Remove(pair.Key);
                        await UpdateEvidenceAsync(existing, cancellationToken)
                            .ConfigureAwait(false);
                        continue;
                    }

                    var failures = Increment(_healthFailureCounts, pair.Key);
                    if (failures < RemovalFailureThreshold)
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
                            ForgetTarget(pair.Key);
                        }
                    }
                    finally
                    {
                        _gate.Release();
                    }
                    existing.HostEventReceived -= HandleRuntimeEvent;
                    await existing.DestroyAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }

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
                        _missingTargetCounts.Remove(pair.Key);
                        _healthFailureCounts.Remove(pair.Key);
                    }
                    finally
                    {
                        _gate.Release();
                    }
                    await UpdateEvidenceAsync(result.Session, cancellationToken)
                        .ConfigureAwait(false);
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
            UpdateState(StatusFromEvidence());
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void HandleRuntimeEvent(object? sender, RuntimeHostEvent hostEvent)
    {
        if (hostEvent.Type == "calibrationResult")
        {
            var role = hostEvent.Payload.TryGetProperty("role", out var property)
                ? property.GetString()
                : null;
            if (!string.Equals(
                    role,
                    Volatile.Read(ref _calibrationRole),
                    StringComparison.Ordinal))
            {
                return;
            }
            if (Interlocked.Exchange(ref _calibrationPending, 0) != 1)
            {
                return;
            }
            Volatile.Write(ref _calibrationRole, null);
            _ = StopCalibrationInOtherTargetsAsync(
                sender as RuntimeTargetSession);
        }
        RuntimeEventReceived?.Invoke(this, hostEvent);
    }

    private async Task StopCalibrationInOtherTargetsAsync(
        RuntimeTargetSession? selected)
    {
        try
        {
            RuntimeTargetSession[] sessions;
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                sessions = _sessions.Values
                    .Where(session => !ReferenceEquals(session, selected))
                    .ToArray();
            }
            finally
            {
                _gate.Release();
            }
            foreach (var session in sessions)
            {
                try
                {
                    await session.StopCalibrationAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.Error("calibration_target_stop_failed", exception);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Controller shutdown already removed every calibration listener.
        }
    }

    private void HandleTargetsChanged(object? sender, EventArgs eventArgs)
    {
        if (!_skinEnabled ||
            Interlocked.Exchange(ref _eventRefreshQueued, 1) != 0)
        {
            return;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshSessionsAsync(
                    _monitorCancellation?.Token ?? CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Session shutdown cancels queued refreshes.
            }
            catch (Exception exception)
            {
                _logger.Error("target_event_refresh_failed", exception);
            }
            finally
            {
                Interlocked.Exchange(ref _eventRefreshQueued, 0);
            }
        });
    }

    private async Task UpdateEvidenceAsync(
        RuntimeTargetSession session,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.Target.Id))
        {
            return;
        }
        try
        {
            var diagnostics = await session.GetDiagnosticsAsync(cancellationToken)
                .ConfigureAwait(false);
            var evidence = RuntimeSessionEvidence.From(diagnostics);
            var changed =
                !_sessionEvidence.TryGetValue(session.Target.Id, out var previous) ||
                previous != evidence;
            _sessionEvidence[session.Target.Id] = evidence;
            if (changed)
            {
                _logger.Info("runtime_evidence", new Dictionary<string, object?>
                {
                    ["compatibility"] = evidence.Compatibility,
                    ["matchCount"] =
                        evidence.DecoratedUserTurns + evidence.DecoratedAssistantTurns,
                    ["state"] = evidence.HasAppliedDecorations
                        ? "applied"
                        : evidence.ScannedTurns == 0
                            ? "waiting"
                            : "unmatched"
                });
            }
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            _logger.Error("runtime_diagnostics_failed", exception);
        }
    }

    private int EvidenceScore(string? targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId) ||
            !_sessionEvidence.TryGetValue(targetId, out var evidence))
        {
            return 0;
        }
        return evidence.Score;
    }

    private string StatusFromEvidence()
    {
        if (!_skinEnabled)
        {
            return "Skin disabled";
        }
        var evidence = _sessionEvidence.Values.ToArray();
        if (evidence.Any(item => item.HasAppliedDecorations))
        {
            return "Skin active";
        }
        if (evidence.Any(item => item.ScannedTurns > 0))
        {
            return "Compatibility degraded: no decorated turns";
        }
        if (_sessions.Count > 0)
        {
            return "Runtime ready: waiting for conversation";
        }
        return "Safe mode: no compatible renderer";
    }

    private static int Increment(Dictionary<string, int> counts, string id)
    {
        counts.TryGetValue(id, out var current);
        var next = current + 1;
        counts[id] = next;
        return next;
    }

    private void ForgetTarget(string id)
    {
        _missingTargetCounts.Remove(id);
        _healthFailureCounts.Remove(id);
        _sessionEvidence.TryRemove(id, out _);
    }

    private void UpdateState(string status)
    {
        var hasAppliedDecorations =
            _skinEnabled &&
            _sessionEvidence.Values.Any(evidence => evidence.HasAppliedDecorations);
        State = new DesktopSessionState(
            _connection is not null && _phase == DesktopSessionPhase.Connected,
            hasAppliedDecorations,
            _skinEnabled,
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
        await _refreshGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            RuntimeTargetSession[] sessions;
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                sessions = _sessions.Values.ToArray();
                _sessions.Clear();
                _sessionEvidence.Clear();
                _missingTargetCounts.Clear();
                _healthFailureCounts.Clear();
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
                _connection.TargetsChanged -= HandleTargetsChanged;
                await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }
            // A failed attachment must not turn into a surprise "close Codex".
            // The launched root owns pipe keepalive duplicates and remains usable.
            _launchedProcess?.Dispose();
            _launchedProcess = null;
            _skinEnabled = false;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private sealed record RuntimeSessionEvidence(
        string Compatibility,
        int ScannedTurns,
        int DecoratedUserTurns,
        int DecoratedAssistantTurns,
        int AssistantBubbleBlocks,
        bool ObserverActive)
    {
        public bool HasAppliedDecorations =>
            ObserverActive &&
            (DecoratedUserTurns > 0 ||
             DecoratedAssistantTurns > 0 ||
             AssistantBubbleBlocks > 0);

        public int Score =>
            (HasAppliedDecorations ? 100 : 0) +
            (Compatibility == "compatible" ? 40 :
                Compatibility == "degraded" ? 20 : 0) +
            Math.Min(ScannedTurns, 20);

        public static RuntimeSessionEvidence From(System.Text.Json.JsonElement payload)
        {
            return new RuntimeSessionEvidence(
                String(payload, "compatibility"),
                Int(payload, "scannedTurns"),
                Int(payload, "decoratedUserTurns"),
                Int(payload, "decoratedAssistantTurns"),
                Int(payload, "assistantBubbleBlocks"),
                Bool(payload, "observerActive"));
        }

        private static int Int(System.Text.Json.JsonElement payload, string name)
        {
            return payload.TryGetProperty(name, out var property) &&
                   property.TryGetInt32(out var value)
                ? Math.Max(0, value)
                : 0;
        }

        private static bool Bool(
            System.Text.Json.JsonElement payload,
            string name)
        {
            return payload.TryGetProperty(name, out var property) &&
                   property.ValueKind == System.Text.Json.JsonValueKind.True;
        }

        private static string String(
            System.Text.Json.JsonElement payload,
            string name)
        {
            return payload.TryGetProperty(name, out var property) &&
                   property.ValueKind == System.Text.Json.JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }
    }

    private sealed record DiscoveryLogSnapshot(
        int CandidateCount,
        int EligibleCount,
        int ConversationTargets,
        int VisibleTargets);
}

public sealed class ApplicationAlreadyRunningException : InvalidOperationException
{
    public ApplicationAlreadyRunningException(ApplicationCandidate candidate)
        : base($"{candidate.DisplayName} is already running and must be restarted with MyCO.")
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

public sealed class DesktopProcessExitedBeforeReadyException : InvalidOperationException
{
    public DesktopProcessExitedBeforeReadyException(int exitCode)
        : base($"Codex exited before its renderer became ready (exit code {exitCode}).")
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

public sealed class DesktopRendererNotReadyException : TimeoutException
{
    public DesktopRendererNotReadyException()
        : base("Codex started, but no compatible renderer became ready in time.")
    {
    }
}
