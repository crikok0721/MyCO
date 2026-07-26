using System.Collections.Concurrent;
using System.Text.Json;

namespace MyCodex.Cdp;

public sealed class CdpMessageCorrelator
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending =
        new();

    public Task<JsonElement> Register(long id)
    {
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
            completion.TrySetException(
                new InvalidOperationException($"CDP error: {error.GetRawText()}"));
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
