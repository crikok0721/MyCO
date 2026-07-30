import assert from "node:assert/strict";
import test from "node:test";
import { JSDOM } from "jsdom";
import { HostThemeDetector } from "../src/host-theme-detector.js";
import type { HostThemeResult } from "../src/types.js";

function themeDocument(
  theme?: "light" | "dark",
  background = "transparent"
): JSDOM {
  const attribute = theme ? ` data-theme="${theme}"` : "";
  return new JSDOM(
    `<!doctype html><html${attribute} style="background:${background}">
      <head></head><body><main></main></body></html>`,
    { pretendToBeVisual: true, url: "app://-/index.html" }
  );
}

function installMatchMedia(dom: JSDOM, light: boolean): {
  setLight(value: boolean): void;
  listenerCount(): number;
} {
  let matches = light;
  const listeners = new Set<(event: MediaQueryListEvent) => void>();
  Object.defineProperty(dom.window, "matchMedia", {
    configurable: true,
    value: (_query: string) => ({
      get matches() {
        return matches;
      },
      media: "(prefers-color-scheme: light)",
      onchange: null,
      addEventListener: (_type: string, listener: (event: MediaQueryListEvent) => void) =>
        listeners.add(listener),
      removeEventListener: (
        _type: string,
        listener: (event: MediaQueryListEvent) => void
      ) => listeners.delete(listener),
      addListener: () => undefined,
      removeListener: () => undefined,
      dispatchEvent: () => true
    })
  });
  return {
    setLight(value: boolean) {
      matches = value;
      for (const listener of listeners) {
        listener({ matches: value } as MediaQueryListEvent);
      }
    },
    listenerCount: () => listeners.size
  };
}

test("HostThemeDetector identifies light and dark at startup", () => {
  for (const expected of ["light", "dark"] as const) {
    const dom = themeDocument(expected);
    installMatchMedia(dom, expected === "light");
    const detector = new HostThemeDetector(dom.window.document, 0);
    const observed: HostThemeResult[] = [];
    const initial = detector.start((result) => observed.push(result));
    assert.equal(initial.theme, expected);
    assert.equal(observed.at(-1)?.theme, expected);
    assert.ok(initial.confidence >= 0.84);
    detector.destroy();
  }
});

test("Codex root theme wins when Windows media preference differs", () => {
  const dom = themeDocument("light", "#ffffff");
  installMatchMedia(dom, false);
  const result = new HostThemeDetector(dom.window.document).detect();
  assert.equal(result.theme, "light");
  assert.ok(result.evidence.some((entry) => entry.startsWith("html.data-theme")));
});

test("computed surface is used without a private theme attribute", () => {
  const dom = themeDocument(undefined, "#101114");
  installMatchMedia(dom, true);
  const result = new HostThemeDetector(dom.window.document).detect();
  assert.equal(result.theme, "dark");
  assert.ok(result.confidence >= 0.75);
});

test("unknown evidence preserves the last trusted theme", async () => {
  const dom = themeDocument("dark");
  installMatchMedia(dom, false);
  const detector = new HostThemeDetector(dom.window.document, 0);
  const observed: HostThemeResult[] = [];
  detector.start((result) => observed.push(result));
  dom.window.document.documentElement.removeAttribute("data-theme");
  dom.window.document.documentElement.style.background = "rgb(188, 188, 188)";
  Object.defineProperty(dom.window, "matchMedia", {
    configurable: true,
    value: undefined
  });
  await new Promise((resolve) => dom.window.setTimeout(resolve, 10));
  assert.deepEqual(observed.map((result) => result.theme), ["dark"]);
  assert.equal(detector.snapshot().theme, "unknown");
  detector.destroy();
});

test("destroy removes observers and media listeners", async () => {
  const dom = themeDocument("dark");
  const media = installMatchMedia(dom, false);
  const detector = new HostThemeDetector(dom.window.document, 0);
  let calls = 0;
  detector.start(() => calls++);
  assert.equal(media.listenerCount(), 1);
  detector.destroy();
  assert.equal(media.listenerCount(), 0);
  dom.window.document.documentElement.dataset.theme = "light";
  media.setLight(true);
  await new Promise((resolve) => dom.window.setTimeout(resolve, 10));
  assert.equal(calls, 1);
});

test("renderer body replacement rebinds theme observation", async () => {
  const dom = themeDocument("dark");
  installMatchMedia(dom, false);
  const detector = new HostThemeDetector(dom.window.document, 25);
  const observed: string[] = [];
  detector.start((result) => observed.push(result.theme));

  dom.window.document.documentElement.removeAttribute("data-theme");
  const replacement = dom.window.document.createElement("body");
  replacement.dataset.theme = "light";
  replacement.innerHTML = "<main></main>";
  dom.window.document.documentElement.replaceChild(
    replacement,
    dom.window.document.body
  );
  await new Promise((resolve) => dom.window.setTimeout(resolve, 80));

  assert.deepEqual(observed, ["dark", "light"]);
  replacement.dataset.theme = "dark";
  await new Promise((resolve) => dom.window.setTimeout(resolve, 80));
  assert.equal(observed.at(-1), "dark");
  detector.destroy();
});

test("thirty theme changes do not multiply callbacks", async () => {
  const dom = themeDocument("dark");
  installMatchMedia(dom, false);
  const detector = new HostThemeDetector(dom.window.document, 0);
  const observed: string[] = [];
  detector.start((result) => observed.push(result.theme));
  for (let index = 0; index < 30; index++) {
    dom.window.document.documentElement.dataset.theme =
      index % 2 === 0 ? "light" : "dark";
    await new Promise((resolve) => dom.window.setTimeout(resolve, 10));
  }
  assert.equal(observed.length, 31);
  assert.equal(observed.at(-1), "dark");
  detector.destroy();
});
