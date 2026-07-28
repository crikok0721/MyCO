import type { HostTheme, HostThemeResult } from "./types.js";

type ThemeChanged = (result: HostThemeResult) => void;

const THEME_ATTRIBUTES = [
  "class",
  "data-theme",
  "data-color-scheme",
  "theme",
  "color-scheme",
  "style"
];

// Detects the official renderer theme without coupling to generated class names.
export class HostThemeDetector {
  private rootObserver: MutationObserver | null = null;
  private bodyObserver: MutationObserver | null = null;
  private structureObserver: MutationObserver | null = null;
  private mediaQuery: MediaQueryList | null = null;
  private debounceTimer: number | null = null;
  private callback: ThemeChanged | null = null;
  private lastTrustedTheme: Exclude<HostTheme, "unknown"> | null = null;
  private lastResult: HostThemeResult = {
    theme: "unknown",
    confidence: 0,
    evidence: []
  };

  constructor(private readonly document: Document, private readonly debounceMs = 50) {}

  start(callback: ThemeChanged): HostThemeResult {
    this.stop();
    this.callback = callback;
    this.bindRootObservers();
    const view = this.document.defaultView;
    if (view?.matchMedia) {
      this.mediaQuery = view.matchMedia("(prefers-color-scheme: light)");
      this.mediaQuery.addEventListener("change", this.handleMediaChange);
    }
    this.evaluate();
    return this.snapshot();
  }

  detect(): HostThemeResult {
    const evidence: string[] = [];
    const explicit = readExplicitTheme(this.document, evidence);
    const surface = readSurfaceTheme(this.document, evidence);
    const media = readMediaTheme(this.document, evidence);

    if (explicit) {
      let confidence = explicit.confidence;
      if (surface?.theme === explicit.theme) confidence = Math.min(0.99, confidence + 0.06);
      else if (surface && surface.theme !== explicit.theme) confidence -= 0.25;
      return confidence >= 0.7
        ? result(explicit.theme, confidence, evidence)
        : result("unknown", confidence, evidence);
    }

    if (surface) {
      const confidence =
        media?.theme === surface.theme
          ? Math.min(0.95, surface.confidence + 0.04)
          : surface.confidence;
      return result(surface.theme, confidence, evidence);
    }

    if (media) {
      return result(media.theme, 0.35, evidence);
    }

    return result("unknown", 0, evidence);
  }

  snapshot(): HostThemeResult {
    return structuredClone(this.lastResult);
  }

  destroy(): void {
    this.stop();
    this.lastTrustedTheme = null;
    this.lastResult = { theme: "unknown", confidence: 0, evidence: [] };
  }

  private stop(): void {
    this.rootObserver?.disconnect();
    this.bodyObserver?.disconnect();
    this.structureObserver?.disconnect();
    this.rootObserver = null;
    this.bodyObserver = null;
    this.structureObserver = null;
    if (this.mediaQuery) {
      this.mediaQuery.removeEventListener("change", this.handleMediaChange);
    }
    this.mediaQuery = null;
    if (this.debounceTimer !== null) {
      this.document.defaultView?.clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    this.callback = null;
  }

  private bindRootObservers(): void {
    const view = this.document.defaultView;
    const root = this.document.documentElement;
    if (!view || !root) return;

    this.rootObserver = new view.MutationObserver(this.scheduleEvaluation);
    this.rootObserver.observe(root, {
      attributes: true,
      attributeFilter: THEME_ATTRIBUTES
    });
    this.bindBodyObserver();
    this.structureObserver = new view.MutationObserver(() => {
      this.bindBodyObserver();
      this.scheduleEvaluation();
    });
    this.structureObserver.observe(root, { childList: true });
  }

  private bindBodyObserver(): void {
    this.bodyObserver?.disconnect();
    const view = this.document.defaultView;
    const body = this.document.body;
    if (!view || !body) {
      this.bodyObserver = null;
      return;
    }
    this.bodyObserver = new view.MutationObserver(this.scheduleEvaluation);
    this.bodyObserver.observe(body, {
      attributes: true,
      attributeFilter: THEME_ATTRIBUTES
    });
  }

  private readonly handleMediaChange = (): void => this.scheduleEvaluation();

  private readonly scheduleEvaluation = (): void => {
    const view = this.document.defaultView;
    if (!view) return;
    if (this.debounceTimer !== null) view.clearTimeout(this.debounceTimer);
    this.debounceTimer = view.setTimeout(() => {
      this.debounceTimer = null;
      this.evaluate();
    }, this.debounceMs);
  };

  private evaluate(): void {
    const detected = this.detect();
    this.lastResult = detected;
    if (detected.theme === "unknown") {
      return;
    }
    if (this.lastTrustedTheme === detected.theme) {
      return;
    }
    this.lastTrustedTheme = detected.theme;
    this.callback?.(detected);
  }
}

function readExplicitTheme(
  document: Document,
  evidence: string[]
): { theme: Exclude<HostTheme, "unknown">; confidence: number } | null {
  for (const [label, element] of [
    ["html", document.documentElement],
    ["body", document.body]
  ] as const) {
    if (!element) continue;
    for (const attribute of [
      "data-theme",
      "data-color-scheme",
      "theme",
      "color-scheme"
    ]) {
      const theme = parseThemeToken(element.getAttribute(attribute));
      if (theme) {
        evidence.push(`${label}.${attribute}:${theme}`);
        return { theme, confidence: 0.93 };
      }
    }
    for (const token of Array.from(element.classList)) {
      const theme = parseThemeToken(token);
      if (theme) {
        evidence.push(`${label}.class:${theme}`);
        return { theme, confidence: 0.84 };
      }
    }
  }
  return null;
}

function readSurfaceTheme(
  document: Document,
  evidence: string[]
): { theme: Exclude<HostTheme, "unknown">; confidence: number } | null {
  const view = document.defaultView;
  if (!view) return null;
  const surfaces = [
    ["html", document.documentElement],
    ["body", document.body],
    ["main", document.querySelector("main")]
  ] as const;
  for (const [label, element] of surfaces) {
    if (!element) continue;
    const color = parseCssColor(view.getComputedStyle(element).backgroundColor);
    if (!color || color.alpha < 0.6) continue;
    const luminance = relativeLuminance(color.red, color.green, color.blue);
    if (luminance >= 0.68) {
      const confidence = Math.min(0.9, 0.75 + (luminance - 0.68) * 0.45);
      evidence.push(`${label}.background:light:${luminance.toFixed(3)}`);
      return { theme: "light", confidence };
    }
    if (luminance <= 0.32) {
      const confidence = Math.min(0.9, 0.75 + (0.32 - luminance) * 0.45);
      evidence.push(`${label}.background:dark:${luminance.toFixed(3)}`);
      return { theme: "dark", confidence };
    }
  }
  return null;
}

function readMediaTheme(
  document: Document,
  evidence: string[]
): { theme: Exclude<HostTheme, "unknown"> } | null {
  const view = document.defaultView;
  if (!view?.matchMedia) return null;
  const theme = view.matchMedia("(prefers-color-scheme: light)").matches
    ? "light"
    : "dark";
  evidence.push(`media:${theme}`);
  return { theme };
}

function parseThemeToken(value: string | null): Exclude<HostTheme, "unknown"> | null {
  if (!value) return null;
  const normalized = value.trim().toLowerCase();
  if (/^(light|light-mode|theme-light)$/.test(normalized)) return "light";
  if (/^(dark|dark-mode|theme-dark)$/.test(normalized)) return "dark";
  return null;
}

function parseCssColor(
  value: string
): { red: number; green: number; blue: number; alpha: number } | null {
  const match = value.match(
    /^rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)(?:\s*[,/]\s*([\d.]+))?\s*\)$/i
  );
  if (!match) return null;
  return {
    red: Math.min(255, Number(match[1])),
    green: Math.min(255, Number(match[2])),
    blue: Math.min(255, Number(match[3])),
    alpha: match[4] === undefined ? 1 : Math.max(0, Math.min(1, Number(match[4])))
  };
}

function relativeLuminance(red: number, green: number, blue: number): number {
  const [r, g, b] = [red, green, blue].map((component) => {
    const normalized = component / 255;
    return normalized <= 0.04045
      ? normalized / 12.92
      : ((normalized + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * (r ?? 0) + 0.7152 * (g ?? 0) + 0.0722 * (b ?? 0);
}

function result(
  theme: HostTheme,
  confidence: number,
  evidence: string[]
): HostThemeResult {
  return {
    theme,
    confidence: Math.round(Math.max(0, Math.min(1, confidence)) * 1000) / 1000,
    evidence: evidence.slice(0, 8)
  };
}
