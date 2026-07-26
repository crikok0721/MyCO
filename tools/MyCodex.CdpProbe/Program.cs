using System.Text.Json;
using MyCodex.Applications;
using MyCodex.Cdp;

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
        MyCodex.CdpProbe

          --discover                List official desktop candidates only.
          --executable <path>       Probe a specific ChatGPT/Codex executable.
          --profile <directory>     Use a dedicated Chromium user-data directory.
          --timeout <seconds>       Startup timeout (default: 30).
          --runtime <path>          Run injection, synthetic recovery, diagnostics, and cleanup gates.
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
                  "MyCodex",
                  "CdpProbe",
                  Guid.NewGuid().ToString("N"));

var probe = new CdpProbe();
var result = await probe.RunAsync(
    executable,
    profile,
    TimeSpan.FromSeconds(options.TimeoutSeconds),
    terminateLaunchedProcess: !options.KeepRunning && options.Runtime is null);

object? runtimeVerification = null;
var runtimePassed = true;
if (result.Passed && options.Runtime is not null)
{
    // The extended gate injects the real bundle into a synthetic, text-only conversation DOM.
    if (!File.Exists(options.Runtime))
    {
        throw new FileNotFoundException("Runtime bundle was not found.", options.Runtime);
    }
    var target = result.Targets.First(candidate =>
        candidate.Id == result.Renderer?.TargetId);
    var injector = new MyCodex.Injection.RuntimeInjector();
    var injection = await injector.InjectAsync(
        target,
        await File.ReadAllTextAsync(options.Runtime),
        MyCodex.Configuration.AppConfig.Default);
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
                      document.getElementById('mycodex-runtime-style')?.remove();
                      const main = document.createElement('main');
                      main.id = 'mycodex-probe-root';
                      main.innerHTML = `
                        <div id="mycodex-probe-user"
                             class="group flex w-full flex-col items-end justify-end gap-1">
                          <div class="probe-native-user-bubble bg-token-foreground/5 rounded-2xl">
                            <p>Synthetic user prompt</p>
                          </div>
                          <div><button>Copy</button></div>
                        </div>
                        <div id="mycodex-probe-assistant"
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
                      const user = document.getElementById('mycodex-probe-user');
                      const assistant = document.getElementById('mycodex-probe-assistant');
                      const nativeUserBubble = user?.querySelector('.probe-native-user-bubble');
                      const assistantProse = assistant?.querySelector('.probe-markdown');
                      const tool = assistant?.querySelector('.probe-tool-card');
                      return {
                        style: !!document.getElementById('mycodex-runtime-style'),
                        runtime: window.__MYCODEX_RUNTIME__?.getVersion?.() ?? null,
                        assistantRole:
                          assistant?.getAttribute('data-mycodex-role') ?? null,
                        assistantBubble:
                          assistantProse?.getAttribute('data-mycodex-prose') ?? null,
                        assistantIdentity:
                          assistant?.querySelectorAll(':scope > .mc-avatar,:scope > .mc-nickname')
                            .length ?? 0,
                        userRole: user?.getAttribute('data-mycodex-role') ?? null,
                        userIdentity:
                          user?.querySelectorAll(':scope > .mc-avatar,:scope > .mc-nickname')
                            .length ?? 0,
                        userBubbleDecorated:
                          nativeUserBubble?.hasAttribute('data-mycodex-prose') ?? true,
                        userBubbleClassIntact:
                          nativeUserBubble?.classList.contains('rounded-2xl') ?? false,
                        userBubbleInlineStyle:
                          nativeUserBubble?.getAttribute('style') ?? null,
                        toolDecorated: tool?.hasAttribute('data-mycodex-prose') ?? true
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
                    "({style:!!document.getElementById('mycodex-runtime-style'),turns:document.querySelectorAll('[data-mycodex-turn]').length,prose:document.querySelectorAll('[data-mycodex-prose]').length})",
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
    Runtime = runtimeVerification
}, jsonOptions));
return result.Passed && runtimePassed ? 0 : 1;

internal sealed record ProbeOptions(
    bool Discover,
    bool Help,
    bool KeepRunning,
    string? Executable,
    string? Profile,
    string? Runtime,
    int TimeoutSeconds)
{
    public static ProbeOptions Parse(string[] arguments)
    {
        var discover = false;
        var help = false;
        var keepRunning = false;
        string? executable = null;
        string? profile = null;
        string? runtime = null;
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
                case "--executable" when index + 1 < arguments.Length:
                    executable = arguments[++index];
                    break;
                case "--profile" when index + 1 < arguments.Length:
                    profile = arguments[++index];
                    break;
                case "--runtime" when index + 1 < arguments.Length:
                    runtime = arguments[++index];
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
            executable,
            profile,
            runtime,
            timeout);
    }
}
