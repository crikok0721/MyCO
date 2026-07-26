import { MyCodexRuntime } from "./runtime.js";
import {
  PROTOCOL_VERSION,
  RUNTIME_VERSION,
  RUNTIME_SYMBOL,
  type MyCodexRuntimeApi
} from "./types.js";

declare global {
  interface Window {
    __MYCODEX_RUNTIME__?: MyCodexRuntimeApi;
  }
}

export function bootstrap(document: Document = globalThis.document): MyCodexRuntimeApi {
  const realm = document.defaultView ?? globalThis;
  const registry = realm as unknown as Record<PropertyKey, unknown>;
  const existing = registry[RUNTIME_SYMBOL] as MyCodexRuntimeApi | undefined;
  if (existing) {
    const version = existing.getVersion?.();
    if (
      version?.version === RUNTIME_VERSION &&
      version.protocolVersion === PROTOCOL_VERSION
    ) {
      defineRuntimeApi(registry, existing);
      existing.install();
      return existing;
    }
    try {
      existing.destroy?.();
    } finally {
      delete registry[RUNTIME_SYMBOL];
    }
  }

  const runtime = new MyCodexRuntime(document);
  Object.defineProperty(registry, RUNTIME_SYMBOL, {
    configurable: true,
    enumerable: false,
    value: runtime
  });
  defineRuntimeApi(registry, runtime);
  runtime.install();
  return runtime;
}

function defineRuntimeApi(
  registry: Record<PropertyKey, unknown>,
  runtime: MyCodexRuntimeApi
): void {
  Object.defineProperty(registry, "__MYCODEX_RUNTIME__", {
    configurable: true,
    enumerable: false,
    value: runtime
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => bootstrap(document), {
      once: true
    });
  } else {
    bootstrap(document);
  }
}

export * from "./classifier.js";
export * from "./dom-utils.js";
export * from "./matcher.js";
export * from "./types.js";
