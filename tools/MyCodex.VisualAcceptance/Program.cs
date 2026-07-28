using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using MyCodex.Applications;
using MyCodex.Configuration;
using MyCodex.VisualAcceptance;

namespace MyCodex.VisualAcceptanceTool;

// Development-only CLI. The long-lived host owns exactly one isolated target;
// short-lived commands communicate with it through a run-specific named pipe.
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                WriteHelp();
                return 0;
            }

            return args[0].ToLowerInvariant() switch
            {
                "start" => await StartAsync(args[1..]).ConfigureAwait(false),
                "host" => await HostAsync(args[1..]).ConfigureAwait(false),
                "status" => await SendAsync("status", args[1..]).ConfigureAwait(false),
                "restart" => await SendAsync("restart", args[1..]).ConfigureAwait(false),
                "theme" => await SendAsync("theme", args[1..]).ConfigureAwait(false),
                "disable" => await SendAsync("disable", args[1..]).ConfigureAwait(false),
                "record" => await SendAsync("record", args[1..]).ConfigureAwait(false),
                "stop" => await SendAsync("stop", args[1..]).ConfigureAwait(false),
                _ => throw new ArgumentException($"Unknown command: {args[0]}")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(
                new
                {
                    passed = false,
                    errorCode = $"MCX-VA-CLI-{exception.GetType().Name.ToUpperInvariant()}",
                    message = exception.Message
                },
                JsonOptions));
            return 1;
        }
    }

    private static async Task<int> StartAsync(string[] args)
    {
        // Build all identity metadata before starting the host so every later
        // command can address the run without asking the developer for a PID.
        var runtimePath = GetOption(args, "--runtime") ??
                          Path.Combine(
                              Environment.CurrentDirectory,
                              "src",
                              "MyCodex.Runtime",
                              "dist",
                              "mycodex.runtime.js");
        var executablePath = GetOption(args, "--executable") ??
                             await DiscoverOfficialExecutableAsync()
                                 .ConfigureAwait(false);
        runtimePath = Path.GetFullPath(runtimePath);
        executablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(runtimePath))
        {
            throw new FileNotFoundException("Runtime bundle was not found.", runtimePath);
        }
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "Official Desktop executable was not found.",
                executablePath);
        }

        var paths = VisualAcceptanceRunPaths.Create();
        Directory.CreateDirectory(paths.RunDirectory);
        Directory.CreateDirectory(paths.ProfileDirectory);
        var startedAt = DateTimeOffset.UtcNow;
        await new VisualAcceptanceStateStore(paths.StateFile).WriteAsync(
            new VisualAcceptanceState
            {
                RunId = paths.RunId,
                Phase = "created",
                ExecutablePath = executablePath,
                ProfilePath = paths.ProfileDirectory,
                StartedAt = startedAt,
                UpdatedAt = startedAt,
                PipeName = $"MyCodex.VisualAcceptance.{paths.RunId}"
            }).ConfigureAwait(false);

        var processPath = Environment.ProcessPath ??
                          throw new InvalidOperationException(
                              "Visual acceptance host executable path is unavailable.");
        var hostArguments = new List<string>();
        var appHostPath = Path.ChangeExtension(
            Assembly.GetExecutingAssembly().Location,
            ".exe");
        var useShellExecute = File.Exists(appHostPath);
        if (useShellExecute)
        {
            processPath = appHostPath;
        }
        else if (Path.GetFileNameWithoutExtension(processPath).Equals(
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            hostArguments.Add(Assembly.GetExecutingAssembly().Location);
        }
        hostArguments.AddRange(
        [
            "host",
            "--run-id", paths.RunId,
            "--executable", executablePath,
            "--runtime", runtimePath,
            "--started-at", startedAt.ToString("O")
        ]);
        var hostStartInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = useShellExecute,
            CreateNoWindow = !useShellExecute,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Environment.CurrentDirectory
        };
        foreach (var argument in hostArguments)
        {
            hostStartInfo.ArgumentList.Add(argument);
        }
        var host = Process.Start(hostStartInfo) ?? throw new InvalidOperationException(
            "Visual acceptance host process could not be started.");

        var timeoutSeconds = int.TryParse(
            GetOption(args, "--timeout-seconds"),
            out var parsedTimeout)
            ? Math.Clamp(parsedTimeout, 10, 180)
            : 75;
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        VisualAcceptanceState? state = null;
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (File.Exists(paths.StateFile))
            {
                state = await VisualAcceptanceStateStore.ReadAsync(paths.StateFile)
                    .ConfigureAwait(false);
                if (state.Phase is "ready" or "failed")
                {
                    break;
                }
            }
            if (host.HasExited)
            {
                break;
            }
            await Task.Delay(200).ConfigureAwait(false);
        }
        state ??= await VisualAcceptanceStateStore.ReadAsync(paths.StateFile)
            .ConfigureAwait(false);
        Console.WriteLine(VisualAcceptanceStateStore.Serialize(
            new
            {
                passed = state.Phase == "ready",
                state,
                commands = CommandExamples(paths.RunId)
            }));
        return state.Phase == "ready" ? 0 : 1;
    }

    private static async Task<int> HostAsync(string[] args)
    {
        var runId = RequireOption(args, "--run-id");
        var executablePath = RequireOption(args, "--executable");
        var runtimePath = RequireOption(args, "--runtime");
        var startedAt = DateTimeOffset.Parse(
            RequireOption(args, "--started-at"),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);
        await using var host = new VisualAcceptanceHost(
            VisualAcceptanceRunPaths.Create(runId),
            executablePath,
            runtimePath,
            startedAt);
        return await host.RunAsync().ConfigureAwait(false);
    }

    private static async Task<int> SendAsync(string commandName, string[] args)
    {
        // A final state remains readable after cleanup; active commands require
        // the run-specific host pipe and cannot fall back to process discovery.
        var runId = RequireOption(args, "--run-id");
        var paths = VisualAcceptanceRunPaths.Create(runId);
        var stateFile = File.Exists(paths.StateFile)
            ? paths.StateFile
            : paths.FinalStateFile;
        if (!File.Exists(stateFile))
        {
            throw new FileNotFoundException(
                "Visual acceptance state was not found.",
                stateFile);
        }
        var state = await VisualAcceptanceStateStore.ReadAsync(stateFile)
            .ConfigureAwait(false);
        if (commandName == "status" &&
            state.Phase is "stopped" or "cleaned" or "failed")
        {
            Console.WriteLine(VisualAcceptanceStateStore.Serialize(state));
            return state.Phase == "failed" ? 1 : 0;
        }
        if (commandName == "stop" &&
            state.Phase == "failed" &&
            !IsProcessAlive(state.TargetPid))
        {
            if (Directory.Exists(paths.RunDirectory))
            {
                paths.ValidateForRecursiveCleanup(paths.RunDirectory);
                Directory.Delete(paths.RunDirectory, recursive: true);
            }
            var cleaned = state with
            {
                Phase = "cleaned",
                TargetPid = null,
                TargetStartedAt = null,
                CleanupResult = "failed-run-directory-removed",
                ErrorCode = state.ErrorCode,
                ErrorDetail = state.ErrorDetail,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await new VisualAcceptanceStateStore(paths.FinalStateFile)
                .WriteAsync(cleaned).ConfigureAwait(false);
            Console.WriteLine(VisualAcceptanceStateStore.Serialize(cleaned));
            return 0;
        }

        var command = commandName switch
        {
            "record" => new AcceptanceCommand(
                commandName,
                RequireOption(args, "--check"),
                RequireOption(args, "--result").ToLowerInvariant(),
                GetOption(args, "--note")),
            "theme" => new AcceptanceCommand(
                commandName,
                Result: RequireOption(args, "--mode").ToLowerInvariant()),
            "stop" => new AcceptanceCommand(
                commandName,
                PreserveArtifacts: HasFlag(args, "--preserve-artifacts")),
            _ => new AcceptanceCommand(commandName)
        };
        using var pipe = new NamedPipeClientStream(
            ".",
            state.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        await using var writer = new StreamWriter(
            pipe,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true)
        {
            AutoFlush = true
        };
        using var reader = new StreamReader(
            pipe,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        await writer.WriteLineAsync(
            VisualAcceptanceStateStore.SerializeCompact(command)).ConfigureAwait(false);
        var response = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
        Console.WriteLine(response);
        var result = JsonSerializer.Deserialize<AcceptanceCommandResult>(
            response ?? "{}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return result?.Passed == true ? 0 : 1;
    }

    private static async Task<string> DiscoverOfficialExecutableAsync()
    {
        var candidates = await new WindowsApplicationLocator()
            .FindCandidatesAsync().ConfigureAwait(false);
        return candidates.FirstOrDefault(candidate =>
                   File.Exists(candidate.ExecutablePath))
                   ?.ExecutablePath
               ?? throw new FileNotFoundException(
                   "No official Desktop installation was discovered. " +
                   "Pass --executable with the exact selected installation.");
    }

    private static Dictionary<string, string> CommandExamples(string runId) =>
        new(StringComparer.Ordinal)
        {
            ["restart"] = $"dotnet MyCodex.VisualAcceptance.dll restart --run-id {runId}",
            ["themeLight"] =
                $"dotnet MyCodex.VisualAcceptance.dll theme --run-id {runId} --mode light",
            ["themeDark"] =
                $"dotnet MyCodex.VisualAcceptance.dll theme --run-id {runId} --mode dark",
            ["status"] = $"dotnet MyCodex.VisualAcceptance.dll status --run-id {runId}",
            ["disable"] = $"dotnet MyCodex.VisualAcceptance.dll disable --run-id {runId}",
            ["stop"] = $"dotnet MyCodex.VisualAcceptance.dll stop --run-id {runId}"
        };

    private static string RequireOption(string[] args, string name) =>
        GetOption(args, name) ??
        throw new ArgumentException($"Required option is missing: {name}");

    private static string? GetOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }
        return null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(value => value.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static bool IsProcessAlive(int? processId)
    {
        if (processId is null)
        {
            return false;
        }
        try
        {
            using var process = Process.GetProcessById(processId.Value);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsHelp(string value) =>
        value is "-h" or "--help" or "help";

    private static void WriteHelp()
    {
        Console.WriteLine(
            """
            MyCodex.VisualAcceptance (development/acceptance-only)

            start   [--executable PATH] [--runtime PATH] [--timeout-seconds N]
            restart --run-id RUN_ID
            theme   --run-id RUN_ID --mode dark|light
            status  --run-id RUN_ID
            disable --run-id RUN_ID
            record  --run-id RUN_ID --check NAME --result pass|fail|blocked [--note TEXT]
            stop    --run-id RUN_ID [--preserve-artifacts]
            """);
    }
}
