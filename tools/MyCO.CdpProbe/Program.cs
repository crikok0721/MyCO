using System.Text.Json;
using MyCO.Applications;
using MyCO.Cdp;

// Standalone command-line gate for desktop discovery, CDP access, and runtime recovery.
var options = ProbeOptions.Parse(args);
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

if (options.Help)
{
    Console.WriteLine(
        """
        MyCO.CdpProbe

          --discover                List official desktop candidates only.
          --executable <path>       Probe a specific ChatGPT/Codex executable.
          --profile <directory>     Use a dedicated Chromium user-data directory.
          --timeout <seconds>       Startup timeout (default: 30).
          --runtime <path>          Run injection, synthetic recovery, diagnostics, and cleanup gates.
          --transport <pipe|tcp>    CDP transport (default: tcp).
          --hold <seconds>          Keep a successful pipe open briefly for OS inspection.
          --verify-detach           Verify the exact isolated pipe target survives disconnect.
          --keep-running            Leave the probe instance running.
        """);
    return 0;
}

// Discovery mode is read-only and can be used before launching a dedicated probe process.
var locator = new WindowsApplicationLocator();
var candidates = await locator.FindCandidatesAsync();
if (options.Discover)
{
    Console.WriteLine(JsonSerializer.Serialize(candidates, jsonOptions));
    return candidates.Count > 0 ? 0 : 2;
}

var executable = options.Executable ??
                 candidates.FirstOrDefault(candidate =>
                     !string.IsNullOrWhiteSpace(candidate.ExecutablePath))?.ExecutablePath;
if (string.IsNullOrWhiteSpace(executable))
{
    Console.Error.WriteLine("No official ChatGPT/Codex Desktop candidate was found.");
    return 2;
}

var profile = options.Profile ??
              Path.Combine(
                  Path.GetTempPath(),
                  "MyCO",
                  "CdpProbe",
                  Guid.NewGuid().ToString("N"));

if (options.Transport == DesktopDebugTransport.Pipe && options.Runtime is not null)
{
    throw new ArgumentException(
        "The runtime harness currently uses the TCP probe; run the pipe transport gate separately.");
}
if (options.Transport == DesktopDebugTransport.Pipe && options.KeepRunning)
{
    throw new ArgumentException(
        "Use --hold for pipe inspection; pipe probe processes are always cleaned up.");
}
if (options.VerifyDetach && options.Transport != DesktopDebugTransport.Pipe)
{
    throw new ArgumentException("--verify-detach requires --transport pipe.");
}
var result = options.Transport == DesktopDebugTransport.Pipe
    ? await new PipeCdpProbe().RunAsync(
        executable,
        profile,
        TimeSpan.FromSeconds(options.TimeoutSeconds),
        terminateLaunchedProcess: !options.VerifyDetach,
        holdOpen: TimeSpan.FromSeconds(options.HoldSeconds))
    : await new CdpProbe().RunAsync(
        executable,
        profile,
        TimeSpan.FromSeconds(options.TimeoutSeconds),
        terminateLaunchedProcess: !options.KeepRunning && options.Runtime is null);

object? runtimeVerification = null;
var runtimePassed = true;
DetachVerification? detachVerification = null;
if (result.Passed && options.VerifyDetach)
{
    detachVerification = await VerifyDetachAsync(
        result,
        executable,
        TimeSpan.FromSeconds(3));
}
if (result.Passed && options.Runtime is not null)
{
    // The extended gate injects the real bundle into a synthetic, text-only conversation DOM.
    if (!File.Exists(options.Runtime))
    {
        throw new FileNotFoundException("Runtime bundle was not found.", options.Runtime);
    }
    var target = result.Targets.First(candidate =>
        candidate.Id == result.Renderer?.TargetId);
    var injector = new MyCO.Injection.RuntimeInjector();
    var injectionClient = new CdpClient();
    await injectionClient.ConnectAsync(new Uri(target.WebSocketDebuggerUrl!));
    var injection = await injector.InjectAsync(
        target,
        injectionClient,
        await File.ReadAllTextAsync(options.Runtime),
        MyCO.Configuration.AppConfig.Default);
    JsonElement? diagnostics = null;
    JsonElement? harness = null;
    JsonElement? cleanup = null;
    var healthPassed = false;
    var harnessPassed = false;
    if (injection.Session is not null)
    {
        await using var validationClient = new CdpClient();
        await validationClient.ConnectAsync(new Uri(target.WebSocketDebuggerUrl!));
        // Replace the visible main element so tests never depend on private production DOM.
        await validationClient.SendCommandAsync(
            "Runtime.evaluate",
            new
            {
                expression =
                    """
                    (() => {
                      document.getElementById('myco-runtime-style')?.remove();
                      const main = document.createElement('main');
                      main.id = 'myco-probe-root';
                      main.innerHTML = `
                        <div id="myco-probe-user"
                             class="group flex w-full flex-col items-end justify-end gap-1">
                          <div class="probe-native-user-bubble bg-token-foreground/5 rounded-2xl">
                            <p>Synthetic user prompt</p>
                          </div>
                          <div><button>Copy</button></div>
                        </div>
                        <div id="myco-probe-assistant"
                             class="group flex min-w-0 flex-col">
                          <h4 class="sr-only">Assistant</h4>
                          <div class="probe-markdown _markdownContent_probe_42">
                            <p>Synthetic assistant response</p>
                          </div>
                          <div class="probe-tool-card" data-testid="tool-card">
                            <button>Tool</button>
                          </div>
                        </div>`;
                      const previous = document.querySelector('main');
                      if (previous) previous.replaceWith(main);
                      else (document.body ?? document.documentElement).append(main);
                      return true;
                    })()
                    """,
                returnByValue = true
            });
        healthPassed = await injection.Session.EnsureActiveAsync();
        diagnostics = await injection.Session.GetDiagnosticsAsync();
        var harnessResponse = await validationClient.SendCommandAsync(
            "Runtime.evaluate",
            new
            {
                expression =
                    """
                    (() => {
                      const user = document.getElementById('myco-probe-user');
                      const assistant = document.getElementById('myco-probe-assistant');
                      const nativeUserBubble = user?.querySelector('.probe-native-user-bubble');
                      const assistantProse = assistant?.querySelector('.probe-markdown');
                      const tool = assistant?.querySelector('.probe-tool-card');
                      return {
                        style: !!document.getElementById('myco-runtime-style'),
                        runtime: window.__MYCO_RUNTIME__?.getVersion?.() ?? null,
                        assistantRole:
                          assistant?.getAttribute('data-myco-role') ?? null,
                        assistantBubble:
                          assistantProse?.querySelector('[data-myco-prose=assistant]')
                            ?.getAttribute('data-myco-prose') ?? null,
                        assistantIdentity:
                          assistant?.querySelectorAll(':scope > .mc-avatar,:scope > .mc-nickname')
                            .length ?? 0,
                        userRole: user?.getAttribute('data-myco-role') ?? null,
                        userIdentity:
                          user?.querySelectorAll(':scope > .mc-avatar,:scope > .mc-nickname')
                            .length ?? 0,
                        userBubbleDecorated:
                          nativeUserBubble?.hasAttribute('data-myco-prose') ?? true,
                        userBubbleClassIntact:
                          nativeUserBubble?.classList.contains('rounded-2xl') ?? false,
                        userBubbleInlineStyle:
                          nativeUserBubble?.getAttribute('style') ?? null,
                        toolDecorated: tool?.hasAttribute('data-myco-prose') ?? true
                      };
                    })()
                    """,
                returnByValue = true
            });
        harness = harnessResponse
            .GetProperty("result")
            .GetProperty("result")
            .GetProperty("value")
            .Clone();
        harnessPassed =
            harness.Value.GetProperty("style").GetBoolean() &&
            harness.Value.GetProperty("assistantRole").GetString() == "assistant" &&
            harness.Value.GetProperty("assistantBubble").GetString() == "assistant" &&
            harness.Value.GetProperty("assistantIdentity").GetInt32() == 2 &&
            harness.Value.GetProperty("userRole").GetString() == "user" &&
            harness.Value.GetProperty("userIdentity").GetInt32() == 2 &&
            !harness.Value.GetProperty("userBubbleDecorated").GetBoolean() &&
            harness.Value.GetProperty("userBubbleClassIntact").GetBoolean() &&
            harness.Value.GetProperty("userBubbleInlineStyle").ValueKind ==
                JsonValueKind.Null &&
            !harness.Value.GetProperty("toolDecorated").GetBoolean();

        await injection.Session.DestroyAsync();
        var cleanupResponse = await validationClient.SendCommandAsync(
            "Runtime.evaluate",
            new
            {
                expression =
                    "({style:!!document.getElementById('myco-runtime-style'),turns:document.querySelectorAll('[data-myco-turn]').length,prose:document.querySelectorAll('[data-myco-prose]').length})",
                returnByValue = true
            });
        cleanup = cleanupResponse
            .GetProperty("result")
            .GetProperty("result")
            .GetProperty("value")
            .Clone();
    }
    runtimePassed = injection.Passed &&
                    healthPassed &&
                    harnessPassed &&
                    cleanup is { } value &&
                    !value.GetProperty("style").GetBoolean() &&
                    value.GetProperty("turns").GetInt32() == 0 &&
                    value.GetProperty("prose").GetInt32() == 0;
    runtimeVerification = new
    {
        injection.Passed,
        injection.Status,
        injection.Handshake,
        injection.Error,
        HealthPassed = healthPassed,
        Harness = harness,
        HarnessPassed = harnessPassed,
        Diagnostics = diagnostics,
        Cleanup = cleanup,
        CleanupPassed = runtimePassed
    };
}

if (!options.KeepRunning &&
    options.Runtime is not null &&
    result.LaunchedProcessId is int launchedProcessId)
{
    try
    {
        using var launched = System.Diagnostics.Process.GetProcessById(launchedProcessId);
        if (!launched.HasExited)
        {
            launched.Kill(entireProcessTree: true);
            await launched.WaitForExitAsync();
        }
    }
    catch (ArgumentException)
    {
        // The probe instance already exited.
    }
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    Candidates = candidates,
    Probe = result,
    Runtime = runtimeVerification,
    Detach = detachVerification
}, jsonOptions));
return result.Passed &&
       runtimePassed &&
       (detachVerification?.Passed ?? !options.VerifyDetach)
    ? 0
    : 1;

static async Task<DetachVerification> VerifyDetachAsync(
    CdpFeasibilityResult result,
    string expectedExecutable,
    TimeSpan observation)
{
    if (result.LaunchedProcessId is not int processId)
    {
        return new DetachVerification(false, null, null, "No launched PID.");
    }

    ApplicationProcessIdentity? identity = null;
    var cleanup = "not-attempted";
    try
    {
        using var process = System.Diagnostics.Process.GetProcessById(processId);
        var path = process.MainModule?.FileName
                   ?? throw new InvalidOperationException(
                       "The isolated target path is unreadable.");
        identity = new ApplicationProcessIdentity(
            process.Id,
            Path.GetFullPath(path),
            process.StartTime.ToUniversalTime());
        if (!identity.ExecutablePath.Equals(
                Path.GetFullPath(expectedExecutable),
                StringComparison.OrdinalIgnoreCase))
        {
            return new DetachVerification(
                false,
                identity.ProcessId,
                identity.StartedAt,
                "The launched path did not match.");
        }

        await Task.Delay(observation);
        process.Refresh();
        var survived = !process.HasExited &&
                       process.MainModule?.FileName?.Equals(
                           identity.ExecutablePath,
                           StringComparison.OrdinalIgnoreCase) == true &&
                       process.StartTime.ToUniversalTime() == identity.StartedAt;
        return new DetachVerification(
            survived,
            identity.ProcessId,
            identity.StartedAt,
            survived
                ? "Exact isolated target survived private-pipe disconnect."
                : "Exact isolated target exited or changed identity.");
    }
    finally
    {
        if (identity is not null)
        {
            try
            {
                using var current =
                    System.Diagnostics.Process.GetProcessById(identity.ProcessId);
                if (!current.HasExited &&
                    current.MainModule?.FileName?.Equals(
                        identity.ExecutablePath,
                        StringComparison.OrdinalIgnoreCase) == true &&
                    current.StartTime.ToUniversalTime() == identity.StartedAt)
                {
                    current.Kill(entireProcessTree: true);
                    await current.WaitForExitAsync();
                    cleanup = "exact-owned-tree-terminated";
                }
            }
            catch (ArgumentException)
            {
                cleanup = "already-exited";
            }
        }
        if (cleanup == "not-attempted" && identity is not null)
        {
            Console.Error.WriteLine(
                "Detached isolated target cleanup could not be revalidated.");
        }
    }
}

internal sealed record DetachVerification(
    bool Passed,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    string Outcome);

internal sealed record ProbeOptions(
    bool Discover,
    bool Help,
    bool KeepRunning,
    bool VerifyDetach,
    string? Executable,
    string? Profile,
    string? Runtime,
    DesktopDebugTransport Transport,
    int HoldSeconds,
    int TimeoutSeconds)
{
    public static ProbeOptions Parse(string[] arguments)
    {
        var discover = false;
        var help = false;
        var keepRunning = false;
        var verifyDetach = false;
        string? executable = null;
        string? profile = null;
        string? runtime = null;
        var transport = DesktopDebugTransport.Tcp;
        var holdSeconds = 0;
        var timeout = 30;

        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--discover":
                    discover = true;
                    break;
                case "--help":
                case "-h":
                    help = true;
                    break;
                case "--keep-running":
                    keepRunning = true;
                    break;
                case "--verify-detach":
                    verifyDetach = true;
                    break;
                case "--executable" when index + 1 < arguments.Length:
                    executable = arguments[++index];
                    break;
                case "--profile" when index + 1 < arguments.Length:
                    profile = arguments[++index];
                    break;
                case "--runtime" when index + 1 < arguments.Length:
                    runtime = arguments[++index];
                    break;
                case "--transport" when index + 1 < arguments.Length:
                    transport = arguments[++index].ToLowerInvariant() switch
                    {
                        "pipe" => DesktopDebugTransport.Pipe,
                        "tcp" => DesktopDebugTransport.Tcp,
                        _ => throw new ArgumentException(
                            "Transport must be 'pipe' or 'tcp'.")
                    };
                    break;
                case "--hold" when
                    index + 1 < arguments.Length &&
                    int.TryParse(arguments[++index], out var parsedHold):
                    holdSeconds = Math.Clamp(parsedHold, 0, 60);
                    break;
                case "--timeout" when
                    index + 1 < arguments.Length &&
                    int.TryParse(arguments[++index], out var parsed):
                    timeout = Math.Clamp(parsed, 1, 120);
                    break;
                default:
                    throw new ArgumentException($"Unknown or incomplete argument: {arguments[index]}");
            }
        }

        return new ProbeOptions(
            discover,
            help,
            keepRunning,
            verifyDetach,
            executable,
            profile,
            runtime,
            transport,
            holdSeconds,
            timeout);
    }
}
