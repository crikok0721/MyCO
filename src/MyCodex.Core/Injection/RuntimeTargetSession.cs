using System.Text.Json;
using MyCodex.Cdp;
using MyCodex.Configuration;

namespace MyCodex.Injection;

public sealed class RuntimeTargetSession : IAsyncDisposable
{
    private static readonly HashSet<string> AllowedEvents =
        new(StringComparer.Ordinal)
        {
            "calibrationResult",
            "runtimeReady",
            "diagnostics",
            "compatibilityChanged",
            "error"
        };

    private readonly ICdpClient _client;
    private readonly string _bindingName;
    private readonly string _runtimeScript;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private string _configJson;
    private bool _disposed;

    internal RuntimeTargetSession(
        CdpTarget target,
        ICdpClient client,
        string bindingName,
        string runtimeScript,
        string configJson)
    {
        Target = target;
        _client = client;
        _bindingName = bindingName;
        _runtimeScript = runtimeScript;
        _configJson = configJson;
    }

    public CdpTarget Target { get; }
    public string? NewDocumentScriptId { get; internal set; }
    public event EventHandler<RuntimeHostEvent>? HostEventReceived;

    public async Task ApplyConfigAsync(
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        var configJson = await RuntimeConfigSerializer.SerializeAsync(
            config,
            _bindingName,
            cancellationToken).ConfigureAwait(false);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            _configJson = configJson;
            await EvaluateSourceAsync(BuildReloadSource(), cancellationToken)
                .ConfigureAwait(false);
            await ReplaceNewDocumentScriptAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<bool> EnsureActiveAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await EnsureActiveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task ReplaceNewDocumentScriptAsync(
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(NewDocumentScriptId))
        {
            await _client.SendCommandAsync(
                "Page.removeScriptToEvaluateOnNewDocument",
                new { identifier = NewDocumentScriptId },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        var source = BuildReloadSource();
        var registration = await _client.SendCommandAsync(
            "Page.addScriptToEvaluateOnNewDocument",
            new { source },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        NewDocumentScriptId = registration
            .GetProperty("result")
            .GetProperty("identifier")
            .GetString();
    }

    public async Task StartCalibrationAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        if (role is not ("user" or "assistant"))
        {
            throw new ArgumentException("Calibration role must be user or assistant.", nameof(role));
        }
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!await EnsureActiveCoreAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Runtime could not be activated.");
            }
            await _client.SendCommandAsync(
                "Runtime.evaluate",
                new
                {
                    expression = $"window.__MYCODEX_RUNTIME__?.startCalibration('{role}')"
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<JsonElement> GetDiagnosticsAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!await EnsureActiveCoreAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Runtime could not be activated.");
            }
            var response = await _client.SendCommandAsync(
                "Runtime.evaluate",
                new
                {
                    expression = "window.__MYCODEX_RUNTIME__?.getDiagnostics() ?? null",
                    returnByValue = true
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return response
                .GetProperty("result")
                .GetProperty("result")
                .GetProperty("value")
                .Clone();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task DestroyAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (_disposed)
        {
            _operationGate.Release();
            return;
        }
        try
        {
            await _client.SendCommandAsync(
                "Runtime.evaluate",
                new
                {
                    expression = "window.__MYCODEX_RUNTIME__?.destroy?.()"
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(NewDocumentScriptId))
            {
                await _client.SendCommandAsync(
                    "Page.removeScriptToEvaluateOnNewDocument",
                    new { identifier = NewDocumentScriptId },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // Target may already be gone; local teardown must still complete.
        }
        finally
        {
            _disposed = true;
            _client.EventReceived -= HandleCdpEvent;
            await _client.DisposeAsync().ConfigureAwait(false);
            _operationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DestroyAsync(CancellationToken.None).ConfigureAwait(false);
    }

    internal void HandleCdpEvent(object? sender, JsonElement message)
    {
        try
        {
            if (message.GetProperty("method").GetString() != "Runtime.bindingCalled")
            {
                return;
            }
            var parameters = message.GetProperty("params");
            if (parameters.GetProperty("name").GetString() != _bindingName)
            {
                return;
            }
            var payload = parameters.GetProperty("payload").GetString();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }
            var hostEvent = JsonSerializer.Deserialize<RuntimeHostEvent>(
                payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (hostEvent is not null &&
                hostEvent.ProtocolVersion == RuntimeInjector.ProtocolVersion &&
                AllowedEvents.Contains(hostEvent.Type))
            {
                HostEventReceived?.Invoke(this, hostEvent);
            }
        }
        catch (Exception)
        {
            // Ignore malformed page-supplied binding messages.
        }
    }

    private async Task<bool> EnsureActiveCoreAsync(CancellationToken cancellationToken)
    {
        if (await EvaluateHealthAsync(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }
        await EvaluateSourceAsync(BuildReloadSource(), cancellationToken)
            .ConfigureAwait(false);
        return await EvaluateHealthAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> EvaluateHealthAsync(CancellationToken cancellationToken)
    {
        var response = await _client.SendCommandAsync(
            "Runtime.evaluate",
            new
            {
                expression =
                    "window.__MYCODEX_RUNTIME__?.ensureActive?.() ?? null",
                awaitPromise = true,
                returnByValue = true
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!response.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("result", out var remote) ||
            !remote.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("active", out var active))
        {
            return false;
        }
        return active.ValueKind == JsonValueKind.True;
    }

    private async Task EvaluateSourceAsync(
        string source,
        CancellationToken cancellationToken)
    {
        var response = await _client.SendCommandAsync(
            "Runtime.evaluate",
            new
            {
                expression = source,
                awaitPromise = true,
                returnByValue = true
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.TryGetProperty("result", out var result) &&
            result.TryGetProperty("exceptionDetails", out var exception))
        {
            var description =
                exception.TryGetProperty("text", out var text)
                    ? text.GetString()
                    : "Runtime evaluation failed.";
            throw new InvalidOperationException(description);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private string BuildReloadSource()
    {
        return $$"""
            {{_runtimeScript}}
            ;(()=>{
              const apply=()=>window.__MYCODEX_RUNTIME__?.applyConfig({{_configJson}});
              if(document.readyState==="loading"){
                document.addEventListener("DOMContentLoaded",apply,{once:true});
              } else {
                apply();
              }
            })()
            //# sourceURL=mycodex.runtime.js
            """;
    }
}
