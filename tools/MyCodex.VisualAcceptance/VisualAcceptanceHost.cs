using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MyCodex.Avatars;
using MyCodex.Cdp;
using MyCodex.Configuration;
using MyCodex.Injection;
using MyCodex.VisualAcceptance;

namespace MyCodex.VisualAcceptanceTool;

// Owns one Codex B process for the complete acceptance lifecycle. It deliberately
// does not use ApplicationRestartService or any process-name-wide shutdown path.
internal sealed class VisualAcceptanceHost : IAsyncDisposable
{
    private readonly VisualAcceptanceRunPaths _paths;
    private readonly string _executablePath;
    private readonly string _runtimePath;
    private readonly DateTimeOffset _runStartedAt;
    private readonly VisualAcceptanceStateStore _store;
    private readonly string _pipeName;
    private VisualAcceptanceState _state;
    private PipeLaunchedProcess? _launched;
    private PipeCdpConnection? _root;
    private RuntimeTargetSession? _session;
    private CdpTarget? _target;
    private VisualAcceptanceProcessIdentity? _ownedIdentity;
    private string _runtimeScript = string.Empty;
    private AvatarService? _avatarService;
    private AppConfig _config = AppConfig.Default;
    private bool _stopRequested;

    public VisualAcceptanceHost(
        VisualAcceptanceRunPaths paths,
        string executablePath,
        string runtimePath,
        DateTimeOffset runStartedAt)
    {
        _paths = paths;
        _executablePath = Path.GetFullPath(executablePath);
        _runtimePath = Path.GetFullPath(runtimePath);
        _runStartedAt = runStartedAt;
        _pipeName = $"MyCodex.VisualAcceptance.{paths.RunId}";
        _store = new VisualAcceptanceStateStore(paths.StateFile);
        _state = new VisualAcceptanceState
        {
            RunId = paths.RunId,
            Phase = "host-starting",
            ControllerPid = Environment.ProcessId,
            ExecutablePath = _executablePath,
            ProfilePath = paths.ProfileDirectory,
            StartedAt = runStartedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            PipeName = _pipeName
        };
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await PrepareAsync(cancellationToken).ConfigureAwait(false);
            await LaunchAndInjectAsync(isRestart: false, cancellationToken)
                .ConfigureAwait(false);
            await RunCommandServerAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            var code = $"MCX-VA-{exception.GetType().Name.ToUpperInvariant()}";
            await UpdateStateAsync(
                _state with
                {
                    Phase = "failed",
                    ErrorCode = code,
                    ErrorDetail = exception.Message
                },
                CancellationToken.None).ConfigureAwait(false);
            try
            {
                await StopOwnedTargetAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Fail closed: never broaden cleanup to another process.
            }
            return 1;
        }
    }

    private async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_executablePath))
        {
            throw new FileNotFoundException(
                "Official Desktop executable was not found.",
                _executablePath);
        }
        if (!File.Exists(_runtimePath))
        {
            throw new FileNotFoundException("Runtime bundle was not found.", _runtimePath);
        }
        Directory.CreateDirectory(_paths.RunDirectory);
        Directory.CreateDirectory(_paths.ProfileDirectory);
        var avatarDirectory = Path.Combine(_paths.RunDirectory, "avatars");
        Directory.CreateDirectory(avatarDirectory);
        _avatarService = new AvatarService(avatarDirectory);
        _runtimeScript = await File.ReadAllTextAsync(
            _runtimePath,
            cancellationToken).ConfigureAwait(false);

        var assistantSource = Path.Combine(_paths.RunDirectory, "assistant-source.bmp");
        var userSource = Path.Combine(_paths.RunDirectory, "user-source.bmp");
        await File.WriteAllBytesAsync(
            assistantSource,
            BuildSyntheticBmp(96, 64, 92, 110, 246),
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(
            userSource,
            BuildSyntheticBmp(96, 64, 80, 190, 145),
            cancellationToken).ConfigureAwait(false);
        var assistantAvatar = await _avatarService.ImportAsync(
            assistantSource,
            cancellationToken).ConfigureAwait(false);
        var userAvatar = await _avatarService.ImportAsync(
            userSource,
            cancellationToken).ConfigureAwait(false);
        File.Delete(assistantSource);
        File.Delete(userSource);

        _config = AppConfig.Default with
        {
            Assistant = new PersonConfig
            {
                Name = "Assistant B",
                Avatar = assistantAvatar.StoredPath
            },
            User = new PersonConfig
            {
                Name = "User B",
                Avatar = userAvatar.StoredPath
            },
            Appearance = AppConfig.Default.Appearance with
            {
                AvatarSize = 44,
                BubbleRadius = 16,
                MessageGap = 30,
                MessageMaxWidth = 66,
                AssistantBubble = "#262930",
                NicknameColor = "#aeb4c3"
            }
        };
        await UpdateStateAsync(_state, cancellationToken).ConfigureAwait(false);
    }

    private async Task LaunchAndInjectAsync(
        bool isRestart,
        CancellationToken cancellationToken)
    {
        // CDP arguments are present on the very first process launch; Codex A is
        // never restarted to make the target debuggable.
        await UpdateStateAsync(
            _state with { Phase = isRestart ? "restarting" : "launching" },
            cancellationToken).ConfigureAwait(false);

        _launched = WindowsPipeProcessLauncher.Launch(
            _executablePath,
            Path.GetDirectoryName(_executablePath) ?? Environment.CurrentDirectory,
            VisualAcceptanceLaunchArguments.Create(_paths.ProfileDirectory));
        _root = new PipeCdpConnection(
            _launched.BrowserOutput,
            _launched.BrowserInput);
        await UpdateStateAsync(
            _state with
            {
                TargetPid = _launched.Process.Id,
                TargetStartedAt = null,
                ExecutablePath = _executablePath,
                ProfilePath = _paths.ProfileDirectory
            },
            cancellationToken).ConfigureAwait(false);
        await EnsureOwnedIdentityAsync(cancellationToken).ConfigureAwait(false);
        var ownedIdentity = _ownedIdentity ??
                            throw new InvalidOperationException(
                                "Acceptance target identity is unavailable.");
        if (!ownedIdentity.ExecutablePath.Equals(
                _executablePath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Launched target executable does not match the selected installation.");
        }
        await UpdateStateAsync(
            _state with
            {
                TargetPid = ownedIdentity.ProcessId,
                TargetStartedAt = ownedIdentity.ProcessStartedAt,
                ExecutablePath = ownedIdentity.ExecutablePath,
                ProfilePath = ownedIdentity.ProfilePath
            },
            cancellationToken).ConfigureAwait(false);

        _target = await WaitForBestRendererAsync(_root, cancellationToken)
            .ConfigureAwait(false);
        var client = await _root.AttachAsync(_target, cancellationToken)
            .ConfigureAwait(false);
        var injection = await new RuntimeInjector().InjectAsync(
            _target,
            client,
            _runtimeScript,
            _config,
            _avatarService!,
            cancellationToken).ConfigureAwait(false);
        if (!injection.Passed || injection.Session is null || injection.Handshake is null)
        {
            throw new InvalidOperationException(
                $"Runtime injection failed with status {injection.Status}.");
        }
        _session = injection.Session;

        await using (var fixtureClient = await _root.AttachAsync(
                         _target,
                         cancellationToken).ConfigureAwait(false))
        {
            await EvaluateAsync(
                fixtureClient,
                AcceptanceFixture.BuildInstallScript(_paths.RunId),
                cancellationToken).ConfigureAwait(false);
        }
        await _session.EnsureActiveAsync(cancellationToken).ConfigureAwait(false);
        await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        var diagnostics = await _session.GetDiagnosticsAsync(cancellationToken)
            .ConfigureAwait(false);
        var automated = await ReadAutomatedChecksAsync(cancellationToken)
            .ConfigureAwait(false);
        await NormalizeWindowAsync(cancellationToken).ConfigureAwait(false);

        var compatibility = diagnostics.TryGetProperty(
            "compatibility",
            out var compatibilityProperty)
            ? compatibilityProperty.GetString() ?? "safeMode"
            : "safeMode";
        automated["runtimeCompatible"] =
            compatibility is "compatible" or "degraded";
        automated["isolatedProfile"] =
            Path.GetFullPath(ownedIdentity.ProfilePath).Equals(
                Path.GetFullPath(_paths.ProfileDirectory),
                StringComparison.OrdinalIgnoreCase);
        automated["exactOwnedPid"] = ownedIdentity.ProcessId == _launched.Process.Id;

        await UpdateStateAsync(
            _state with
            {
                Phase = "ready",
                RuntimeVersion = injection.Handshake.Version,
                ProtocolVersion = injection.Handshake.ProtocolVersion,
                AutomatedChecks = automated,
                RestartCount = isRestart
                    ? _state.RestartCount + 1
                    : _state.RestartCount,
                ErrorCode = null,
                ErrorDetail = null
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<CdpTarget> WaitForBestRendererAsync(
        PipeCdpConnection root,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        while (!timeout.IsCancellationRequested)
        {
            var candidates = new List<(CdpTarget Target, int Score)>();
            foreach (var target in (await root.ListTargetsAsync(timeout.Token)
                         .ConfigureAwait(false))
                     .Where(target => target.Type is "page" or "webview"))
            {
                try
                {
                    await using var client = await root.AttachAsync(
                        target,
                        timeout.Token).ConfigureAwait(false);
                    var value = await EvaluateAsync(
                        client,
                        """
                        ({
                          readyState: document.readyState,
                          bodyChildren: document.body?.children.length ?? 0,
                          visible: document.visibilityState === "visible",
                          appDocument: location.protocol === "app:"
                        })
                        """,
                        timeout.Token).ConfigureAwait(false);
                    var score =
                        (value.GetProperty("readyState").GetString() == "complete" ? 20 : 5) +
                        (value.GetProperty("bodyChildren").GetInt32() > 0 ? 20 : 0) +
                        (value.GetProperty("visible").GetBoolean() ? 20 : 0) +
                        (value.GetProperty("appDocument").GetBoolean() ? 40 : 0);
                    candidates.Add((target, score));
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException)
                {
                    // Background renderers are expected during startup.
                }
            }
            var best = candidates.OrderByDescending(item => item.Score).FirstOrDefault();
            if (best.Target is not null && best.Score >= 60)
            {
                return best.Target;
            }
            await Task.Delay(250, timeout.Token).ConfigureAwait(false);
        }
        throw new TimeoutException("No isolated official app renderer became ready.");
    }

    private async Task<Dictionary<string, bool>> ReadAutomatedChecksAsync(
        CancellationToken cancellationToken)
    {
        await using var client = await _root!.AttachAsync(
            _target!,
            cancellationToken).ConfigureAwait(false);
        var value = await EvaluateAsync(
            client,
            AcceptanceFixture.AutomatedCheckScript,
            cancellationToken).ConfigureAwait(false);
        return value.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.ValueKind == JsonValueKind.True,
            StringComparer.Ordinal);
    }

    private async Task NormalizeWindowAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _root!.SendCommandAsync(
                "Browser.getWindowForTarget",
                new { targetId = _target!.Id },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var windowId = response.GetProperty("result")
                .GetProperty("windowId")
                .GetInt32();
            await _root.SendCommandAsync(
                "Browser.setWindowBounds",
                new
                {
                    windowId,
                    bounds = new
                    {
                        left = 80,
                        top = 70,
                        width = 1120,
                        height = 860,
                        windowState = "normal"
                    }
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or KeyNotFoundException)
        {
            // Window positioning is helpful but not part of the security boundary.
        }
    }

    private async Task RunCommandServerAsync(CancellationToken cancellationToken)
    {
        while (!_stopRequested && !cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(
                server,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
            await using var writer = new StreamWriter(
                server,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true)
            {
                AutoFlush = true
            };
            AcceptanceCommandResult result;
            try
            {
                var line = await reader.ReadLineAsync(cancellationToken)
                    .ConfigureAwait(false);
                var command = JsonSerializer.Deserialize<AcceptanceCommand>(
                    line ?? "{}",
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new AcceptanceCommand("invalid");
                result = await HandleCommandAsync(command, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException)
            {
                result = new AcceptanceCommandResult(
                    false,
                    "invalid-command-json",
                    _state,
                    "MCX-VA-INVALID-COMMAND-JSON");
            }
            await writer.WriteLineAsync(
                    VisualAcceptanceStateStore.SerializeCompact(result))
                .ConfigureAwait(false);
        }
    }

    private async Task<AcceptanceCommandResult> HandleCommandAsync(
        AcceptanceCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (command.Command.ToLowerInvariant())
            {
                case "status":
                    return new AcceptanceCommandResult(true, "ok", _state);
                case "restart":
                    await RestartAsync(cancellationToken).ConfigureAwait(false);
                    return new AcceptanceCommandResult(true, "restarted", _state);
                case "disable":
                    await DisableAsync(cancellationToken).ConfigureAwait(false);
                    return new AcceptanceCommandResult(true, "disabled", _state);
                case "record":
                    await RecordVisualCheckAsync(command, cancellationToken)
                        .ConfigureAwait(false);
                    return new AcceptanceCommandResult(true, "recorded", _state);
                case "stop":
                    await StopAsync(command.PreserveArtifacts, cancellationToken)
                        .ConfigureAwait(false);
                    _stopRequested = true;
                    return new AcceptanceCommandResult(true, "stopped", _state);
                default:
                    return new AcceptanceCommandResult(
                        false,
                        "unknown-command",
                        _state,
                        "MCX-VA-UNKNOWN-COMMAND");
            }
        }
        catch (Exception exception)
        {
            var code = $"MCX-VA-{command.Command.ToUpperInvariant()}-" +
                       exception.GetType().Name.ToUpperInvariant();
            await UpdateStateAsync(
                _state with
                {
                    Phase = "failed",
                    ErrorCode = code,
                    ErrorDetail = exception.Message
                },
                CancellationToken.None).ConfigureAwait(false);
            return new AcceptanceCommandResult(false, "failed", _state, code);
        }
    }

    private async Task RestartAsync(CancellationToken cancellationToken)
    {
        await UpdateStateAsync(
            _state with
            {
                Phase = "restarting",
                ErrorCode = null,
                ErrorDetail = null
            },
            cancellationToken).ConfigureAwait(false);
        await DestroyRuntimeAsync(cancellationToken).ConfigureAwait(false);
        await StopOwnedTargetAsync(cancellationToken).ConfigureAwait(false);
        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        await LaunchAndInjectAsync(isRestart: true, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DisableAsync(CancellationToken cancellationToken)
    {
        await DestroyRuntimeAsync(cancellationToken).ConfigureAwait(false);
        await using var client = await _root!.AttachAsync(
            _target!,
            cancellationToken).ConfigureAwait(false);
        var value = await EvaluateAsync(
            client,
            AcceptanceFixture.DestroyCheckScript,
            cancellationToken).ConfigureAwait(false);
        var checks = new Dictionary<string, bool>(
            _state.AutomatedChecks,
            StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            checks[$"destroy.{property.Name}"] =
                property.Value.ValueKind == JsonValueKind.True;
        }
        await UpdateStateAsync(
            _state with
            {
                Phase = "disabled",
                AutomatedChecks = checks
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordVisualCheckAsync(
        AcceptanceCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Check) ||
            command.Result is not ("pass" or "fail" or "blocked"))
        {
            throw new ArgumentException(
                "Visual check requires a name and pass/fail/blocked result.");
        }
        var checks = new Dictionary<string, VisualCheckResult>(
            _state.VisualChecks,
            StringComparer.Ordinal)
        {
            [command.Check] = new VisualCheckResult(
                command.Result,
                command.Note ?? string.Empty,
                DateTimeOffset.UtcNow)
        };
        await UpdateStateAsync(
            _state with { VisualChecks = checks },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task StopAsync(
        bool preserveArtifacts,
        CancellationToken cancellationToken)
    {
        await UpdateStateAsync(
            _state with { Phase = "stopping" },
            cancellationToken).ConfigureAwait(false);
        await DestroyRuntimeAsync(cancellationToken).ConfigureAwait(false);
        await StopOwnedTargetAsync(cancellationToken).ConfigureAwait(false);

        var cleanup = "target-stopped";
        if (Directory.Exists(_paths.ProfileDirectory))
        {
            _paths.ValidateForRecursiveCleanup(_paths.ProfileDirectory);
            Directory.Delete(_paths.ProfileDirectory, recursive: true);
            cleanup = "target-stopped-profile-removed";
        }
        _state = _state with
        {
            Phase = "stopped",
            TargetPid = null,
            TargetStartedAt = null,
            CleanupResult = cleanup,
            ErrorCode = null,
            ErrorDetail = null
        };
        await UpdateStateAsync(_state, cancellationToken).ConfigureAwait(false);
        await new VisualAcceptanceStateStore(_paths.FinalStateFile)
            .WriteAsync(_state, cancellationToken).ConfigureAwait(false);

        if (!preserveArtifacts)
        {
            _paths.ValidateForRecursiveCleanup(_paths.RunDirectory);
            Directory.Delete(_paths.RunDirectory, recursive: true);
            _state = _state with
            {
                Phase = "cleaned",
                CleanupResult = "target-stopped-run-directory-removed"
            };
            await new VisualAcceptanceStateStore(_paths.FinalStateFile)
                .WriteAsync(_state, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DestroyRuntimeAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return;
        }
        var session = _session;
        _session = null;
        await session.DestroyAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task StopOwnedTargetAsync(CancellationToken cancellationToken)
    {
        // Re-read the live process identity immediately before closing it.
        // Any mismatch refuses the close instead of searching for another PID.
        if (_launched is null)
        {
            return;
        }
        if (_ownedIdentity is null)
        {
            await EnsureOwnedIdentityAsync(cancellationToken).ConfigureAwait(false);
        }
        var expected = _ownedIdentity ??
                       throw new InvalidOperationException(
                           "Owned target identity is unavailable.");
        var actual = VisualAcceptanceProcessGuard.Snapshot(
            _launched.Process,
            _paths.RunId,
            _runStartedAt,
            _paths.ProfileDirectory);
        if (!VisualAcceptanceProcessGuard.IsExactOwnedTarget(
                expected,
                actual,
                _paths))
        {
            throw new InvalidOperationException(
                "Owned target identity changed; refusing to close any process.");
        }

        if (!_launched.Process.HasExited)
        {
            _launched.Process.Kill(entireProcessTree: true);
            await _launched.Process.WaitForExitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        if (_root is not null)
        {
            await _root.DisposeAsync().ConfigureAwait(false);
        }
        await _launched.DisposeAsync().ConfigureAwait(false);
        _launched = null;
        _root = null;
        _target = null;
        _ownedIdentity = null;
    }

    private async Task EnsureOwnedIdentityAsync(
        CancellationToken cancellationToken)
    {
        if (_ownedIdentity is not null)
        {
            return;
        }
        if (_launched is null)
        {
            throw new InvalidOperationException(
                "Owned target process is unavailable; refusing to infer another process.");
        }

        Exception? lastError = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_launched.Process.HasExited)
            {
                throw new InvalidOperationException(
                    "The recorded acceptance target exited before identity verification.");
            }
            try
            {
                var identity = VisualAcceptanceProcessGuard.Snapshot(
                    _launched.Process,
                    _paths.RunId,
                    _runStartedAt,
                    _paths.ProfileDirectory);
                if (!identity.ExecutablePath.Equals(
                        _executablePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Launched target executable does not match the selected installation.");
                }
                _ownedIdentity = identity;
                await UpdateStateAsync(
                    _state with
                    {
                        TargetPid = identity.ProcessId,
                        TargetStartedAt = identity.ProcessStartedAt,
                        ExecutablePath = identity.ExecutablePath,
                        ProfilePath = identity.ProfilePath
                    },
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                    System.ComponentModel.Win32Exception)
            {
                lastError = exception;
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException(
            "Acceptance target identity could not be verified after launch.",
            lastError);
    }

    private async Task UpdateStateAsync(
        VisualAcceptanceState state,
        CancellationToken cancellationToken)
    {
        VisualAcceptanceLifecycle.EnsureTransition(_state.Phase, state.Phase);
        _state = state with { UpdatedAt = DateTimeOffset.UtcNow };
        await _store.WriteAsync(_state, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonElement> EvaluateAsync(
        ICdpClient client,
        string expression,
        CancellationToken cancellationToken)
    {
        var response = await client.SendCommandAsync(
            "Runtime.evaluate",
            new
            {
                expression,
                returnByValue = true,
                awaitPromise = true
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var result = response.GetProperty("result").GetProperty("result");
        if (result.TryGetProperty("value", out var value))
        {
            return value.Clone();
        }
        throw new InvalidOperationException(
            "Renderer evaluation did not return a serializable value.");
    }

    private static byte[] BuildSyntheticBmp(
        int width,
        int height,
        byte red,
        byte green,
        byte blue)
    {
        var rowBytes = ((width * 3 + 3) / 4) * 4;
        var pixelBytes = rowBytes * height;
        var bytes = new byte[54 + pixelBytes];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BitConverter.GetBytes(bytes.Length).CopyTo(bytes, 2);
        BitConverter.GetBytes(54).CopyTo(bytes, 10);
        BitConverter.GetBytes(40).CopyTo(bytes, 14);
        BitConverter.GetBytes(width).CopyTo(bytes, 18);
        BitConverter.GetBytes(height).CopyTo(bytes, 22);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 26);
        BitConverter.GetBytes((short)24).CopyTo(bytes, 28);
        BitConverter.GetBytes(pixelBytes).CopyTo(bytes, 34);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = 54 + y * rowBytes + x * 3;
                var accent = (byte)((x / 12 + y / 12) % 2 == 0 ? 22 : 0);
                bytes[offset] = (byte)Math.Min(255, blue + accent);
                bytes[offset + 1] = (byte)Math.Min(255, green + accent);
                bytes[offset + 2] = (byte)Math.Min(255, red + accent);
            }
        }
        return bytes;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DestroyRuntimeAsync(CancellationToken.None).ConfigureAwait(false);
            await StopOwnedTargetAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Disposal is fail closed and never falls back to process enumeration.
        }
    }
}
