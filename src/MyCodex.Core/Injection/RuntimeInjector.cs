using System.Text.Json;
using MyCodex.Cdp;
using MyCodex.Configuration;

// Installs the browser runtime into one renderer and verifies the protocol handshake.
namespace MyCodex.Injection;

public sealed class RuntimeInjector
{
    public async Task<RuntimeInjectionResult> InjectAsync(
        CdpTarget target,
        ICdpClient client,
        string runtimeScript,
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        // A unique binding prevents page code from guessing another session's channel name.
        var bindingName = $"__mc_host_{Guid.NewGuid():N}";
        try
        {
            var configJson = await RuntimeConfigSerializer.SerializeAsync(
                config,
                bindingName,
                cancellationToken).ConfigureAwait(false);
            var session = new RuntimeTargetSession(
                target,
                client,
                bindingName,
                runtimeScript,
                configJson);
            client.EventReceived += session.HandleCdpEvent;
            await client.SendCommandAsync(
                "Runtime.enable",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await client.SendCommandAsync(
                "Page.enable",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await client.SendCommandAsync(
                "Runtime.addBinding",
                new { name = bindingName },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var source = BuildBootstrapSource(runtimeScript, configJson);
            // Register for future navigations before evaluating in the current document.
            var registration = await client.SendCommandAsync(
                "Page.addScriptToEvaluateOnNewDocument",
                new { source },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            session.NewDocumentScriptId = registration
                .GetProperty("result")
                .GetProperty("identifier")
                .GetString();

            await client.SendCommandAsync(
                "Runtime.evaluate",
                new
                {
                    expression = source,
                    awaitPromise = true,
                    returnByValue = true
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var handshake = await ReadHandshakeAsync(client, cancellationToken)
                .ConfigureAwait(false);
            if (handshake.ProtocolVersion != BuildInfo.ProtocolVersion)
            {
                await session.DestroyAsync(CancellationToken.None).ConfigureAwait(false);
                return new RuntimeInjectionResult(
                    false,
                    "RuntimeProtocolMismatch",
                    handshake,
                    null,
                    $"Manager protocol {BuildInfo.ProtocolVersion}, runtime protocol {handshake.ProtocolVersion}.");
            }

            return new RuntimeInjectionResult(true, "Pass", handshake, session, null);
        }
        catch (Exception exception)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            return new RuntimeInjectionResult(
                false,
                "InjectionFailed",
                null,
                null,
                exception.GetType().Name);
        }
    }

    private static async Task<RuntimeHandshake> ReadHandshakeAsync(
        ICdpClient client,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        // Runtime installation and DOM readiness can cross a few event-loop turns.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                var response = await client.SendCommandAsync(
                    "Runtime.evaluate",
                    new
                    {
                        expression =
                            "window.__MYCODEX_RUNTIME__?.getVersion?.() ?? null",
                        returnByValue = true
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                var remote = response.GetProperty("result").GetProperty("result");
                if (remote.TryGetProperty("value", out var value) &&
                    value.ValueKind == JsonValueKind.Object)
                {
                    return new RuntimeHandshake(
                        value.GetProperty("version").GetString() ?? "unknown",
                        value.GetProperty("protocolVersion").GetInt32());
                }
            }
            catch (Exception exception)
            {
                lastError = exception;
            }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Runtime handshake timed out.", lastError);
    }

    private static string BuildBootstrapSource(string runtimeScript, string configJson)
    {
        return $$"""
            {{runtimeScript}}
            ;(()=>{
              const apply=()=>window.__MYCODEX_RUNTIME__?.applyConfig({{configJson}});
              if(document.readyState==="loading"){
                document.addEventListener("DOMContentLoaded",apply,{once:true});
                return {pending:true};
              }
              return apply();
            })()
            //# sourceURL=mycodex.runtime.js
            """;
    }
}
