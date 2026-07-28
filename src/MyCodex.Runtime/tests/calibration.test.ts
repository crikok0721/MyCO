// Covers event-path selection so calibration chooses the whole turn, not a text leaf.
import assert from "node:assert/strict";
import test from "node:test";
import { JSDOM } from "jsdom";
import { resolveCalibrationRoot } from "../src/calibration.js";

test("calibration resolves a semantic turn from composedPath", () => {
  const dom = new JSDOM(
    `<main><article data-message-author-role="assistant"><div><p><span>fixture</span></p></div></article></main>`
  );
  installDomGlobals(dom);
  const span = dom.window.document.querySelector("span")!;
  const paragraph = span.parentElement!;
  const wrapper = paragraph.parentElement!;
  const article = wrapper.parentElement!;

  assert.equal(
    resolveCalibrationRoot([span, paragraph, wrapper, article]),
    article
  );
});

test("calibration climbs away from a nested prose leaf without semantic attributes", () => {
  const dom = new JSDOM(
    `<main><section><div><p><span>fixture</span></p><div role="toolbar"><button>copy</button></div></div></section></main>`
  );
  installDomGlobals(dom);
  const span = dom.window.document.querySelector("span")!;
  const paragraph = span.parentElement!;
  const turn = paragraph.parentElement!;
  const section = turn.parentElement!;

  assert.equal(
    resolveCalibrationRoot([span, paragraph, turn, section]),
    turn
  );
});

test("calibration resolves current Codex user bubble to its unit", () => {
  const dom = new JSDOM(
    `<main><section data-content-search-turn-key="turn"><div data-content-search-unit-key="unit"><div data-user-message-bubble><p><span>fixture</span></p></div></div></section></main>`
  );
  installDomGlobals(dom);
  const span = dom.window.document.querySelector("span")!;
  const paragraph = span.parentElement!;
  const bubble = paragraph.parentElement!;
  const unit = bubble.parentElement!;

  assert.equal(resolveCalibrationRoot([span, paragraph, bubble, unit]), unit);
});

function installDomGlobals(dom: JSDOM): void {
  Object.assign(globalThis, {
    Element: dom.window.Element
  });
}
