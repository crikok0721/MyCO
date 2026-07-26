// Sends a small allow-listed event stream from the renderer back to the C# host.
const ALLOWED_EVENTS = new Set([
  "calibrationResult",
  "runtimeReady",
  "diagnostics",
  "compatibilityChanged",
  "error"
]);

export class RuntimeBridge {
  constructor(private bindingName: string | undefined) {}

  updateBinding(bindingName: string | undefined): void {
    this.bindingName = bindingName;
  }

  emit(type: string, payload: unknown): void {
    // The CDP binding is injected by the host and may disappear during navigation.
    if (!this.bindingName || !ALLOWED_EVENTS.has(type)) return;
    const host = (globalThis as Record<string, unknown>)[this.bindingName];
    if (typeof host !== "function") return;
    try {
      (host as (value: string) => void)(
        JSON.stringify({
          type,
          payload,
          protocolVersion: 1,
          at: new Date().toISOString()
        })
      );
    } catch {
      // The page must never gain a privileged failure channel.
    }
  }
}
