using System.Globalization;
using MyCO.Updates;

namespace MyCO.Updater;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (!TryParse(args, out var request))
        {
            return 2;
        }

        try
        {
            await new ExternalUpdateInstaller()
                .ApplyAsync(request, CancellationToken.None)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is UpdateInstallException or InvalidDataException or
                IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The manager remains unchanged on every failure. Do not write user data
            // or attempt elevation from this process.
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static bool TryParse(string[] args, out UpdateApplyRequest request)
    {
        request = null!;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            if (index == 0 && args[index].Equals(
                    "--apply-update",
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }
            if (!values.TryAdd(args[index], args[++index]))
            {
                return false;
            }
        }

        var required = new[]
        {
            "--pid",
            "--path",
            "--start-ticks",
            "--install-dir",
            "--stage",
            "--expected-sha256",
            "--cleanup"
        };
        if (required.Any(key => !values.ContainsKey(key)) ||
            values.Keys.Any(key => !required.Contains(key, StringComparer.Ordinal)))
        {
            return false;
        }
        if (!int.TryParse(
                values["--pid"],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var pid) ||
            !long.TryParse(
                values["--start-ticks"],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var startTicks) ||
            string.IsNullOrWhiteSpace(values["--cleanup"]))
        {
            return false;
        }

        request = new UpdateApplyRequest(
            pid,
            values["--path"],
            startTicks,
            values["--install-dir"],
            values["--stage"],
            values["--expected-sha256"],
            LaunchNewProcess: true,
            WaitTimeout: TimeSpan.FromMinutes(5),
            CleanupDirectory: values["--cleanup"]);
        return true;
    }
}
