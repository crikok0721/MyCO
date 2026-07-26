using System.Collections.Concurrent;
using System.Text.Json;

// Matches asynchronous CDP replies to the command task that is waiting for them.
namespace MyCodex.Cdp;

public sealed class CdpMessageCorrelator
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending =
        new();

    public Task<JsonElement> Register(long id)
    {
        // Run continuations asynchronously so the receive loop is never blocked by callers.
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion))
        {
            throw new InvalidOperationException($"CDP request id {id} is already pending.");
        }
        return completion.Task;
    }

    public bool TryHandle(JsonElement message)
    {
        if (!message.TryGetProperty("id", out var idElement) ||
            !idElement.TryGetInt64(out var id) ||
            !_pending.TryGetValue(id, out var completion))
        {
            return false;
        }
        if (message.TryGetProperty("error", out var error))
        {
            var code = error.TryGetProperty("code", out var codeElement) &&
                       codeElement.TryGetInt32(out var parsedCode)
                ? parsedCode
                : 0;
            var detail = error.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : "Unknown CDP error";
            detail = string.IsNullOrWhiteSpace(detail)
                ? "Unknown CDP error"
                : detail.Length <= 256 ? detail : detail[..256];
            completion.TrySetException(
                new InvalidOperationException($"CDP error {code}: {detail}"));
        }
        else
        {
            completion.TrySetResult(message.Clone());
        }
        return true;
    }

    public void Remove(long id)
    {
        _pending.TryRemove(id, out _);
    }

    public void CancelAll()
    {
        foreach (var completion in _pending.Values)
        {
            completion.TrySetCanceled();
        }
        _pending.Clear();
    }
}
