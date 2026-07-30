import { MyCORuntime } from "./runtime.js";
import {
  PROTOCOL_VERSION,
  RUNTIME_VERSION,
  RUNTIME_SYMBOL,
  LEGACY_RUNTIME_SYMBOL,
  type MyCORuntimeApi
} from "./types.js";

// Browser entry point that installs exactly one compatible runtime per renderer realm.
declare global {
  interface Window {
    __MYCO_RUNTIME__?: MyCORuntimeApi;
    __MYCODEX_RUNTIME__?: MyCORuntimeApi;
  }
}

export function bootstrap(document: Document = globalThis.document): MyCORuntimeApi {
  const realm = document.defaultView ?? globalThis;
  const registry = realm as unknown as Record<PropertyKey, unknown>;
  removeLegacyRuntime(registry);
  const existing = registry[RUNTIME_SYMBOL] as MyCORuntimeApi | undefined;
  if (existing) {
    // Reuse the same build; tear down an older build before replacing its global API.
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

  const runtime = new MyCORuntime(document);
  Object.defineProperty(registry, RUNTIME_SYMBOL, {
    configurable: true,
    enumerable: false,
    value: runtime
  });
  defineRuntimeApi(registry, runtime);
  runtime.install();
  return runtime;
}

function removeLegacyRuntime(registry: Record<PropertyKey, unknown>): void {
  const legacy = registry[LEGACY_RUNTIME_SYMBOL] as MyCORuntimeApi | undefined;
  if (legacy) {
    try {
      legacy.destroy?.();
    } finally {
      delete registry[LEGACY_RUNTIME_SYMBOL];
    }
  }
  delete registry.__MYCODEX_RUNTIME__;
}

function defineRuntimeApi(
  registry: Record<PropertyKey, unknown>,
  runtime: MyCORuntimeApi
): void {
  Object.defineProperty(registry, "__MYCO_RUNTIME__", {
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
