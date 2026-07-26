using System.Diagnostics;

namespace MyCodex.Applications;

public sealed class ApplicationRestartService
{
    public async Task<bool> RequestGracefulCloseAsync(
        ApplicationCandidate candidate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var processes = MatchingRootProcesses(candidate).ToArray();
        foreach (var process in processes)
        {
            using (process)
            {
                process.CloseMainWindow();
            }
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MatchingRootProcesses(candidate).Any())
            {
                return true;
            }
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    public Task ForceCloseAsync(
        ApplicationCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var process in MatchingRootProcesses(candidate))
        {
            using (process)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        return Task.CompletedTask;
    }

    private static IEnumerable<Process> MatchingRootProcesses(
        ApplicationCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.ExecutablePath))
        {
            yield break;
        }
        foreach (var process in Process.GetProcessesByName(candidate.ProcessName))
        {
            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch (Exception)
            {
                process.Dispose();
                continue;
            }
            if (path?.Equals(
                    candidate.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase) == true &&
                process.MainWindowHandle != IntPtr.Zero)
            {
                yield return process;
            }
            else
            {
                process.Dispose();
            }
        }
    }
}
