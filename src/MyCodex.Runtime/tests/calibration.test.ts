// Covers event-path selection so calibration chooses the whole turn, not a text leaf.
import assert from "node:assert/strict";
import test from "node:test";
import { JSDOM } from "jsdom";
import {
  CalibrationController,
  resolveCalibrationRoot
} from "../src/calibration.js";
import {
  createConsensusSignature,
  signatureContextMatches
} from "../src/matcher.js";

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
    `<main><section><div data-message-author-role="assistant"><p><span>fixture</span></p><div role="toolbar"><button>copy</button></div></div></section></main>`
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

test("calibration rejects composer, navigation, and empty-page prose", () => {
  const dom = new JSDOM(
    `<body>
      <nav><p><span id="nav">Project</span></p></nav>
      <main><div class="group flex min-w-0 flex-col"><p><span id="welcome">What should we build?</span></p></div></main>
      <form data-testid="composer"><textarea></textarea><p><span id="composer">Draft</span></p></form>
    </body>`
  );
  installDomGlobals(dom);
  for (const id of ["nav", "welcome", "composer"]) {
    const origin = dom.window.document.querySelector(`#${id}`)!;
    const path: EventTarget[] = [];
    let current: Element | null = origin;
    while (current) {
      path.push(current);
      current = current.parentElement;
    }
    assert.equal(resolveCalibrationRoot(path, "assistant"), null);
  }
});

test("multi-sample calibration stores consensus without layout coordinates", () => {
  const dom = new JSDOM(
    `<main class="thread-scroll-container">
      <article data-message-author-role="assistant"><p>One</p></article>
      <article data-message-author-role="assistant"><p>Two</p><ul><li>x</li></ul></article>
      <article data-message-author-role="assistant"><p>Three</p><pre><code>x</code></pre></article>
    </main>`
  );
  installDomGlobals(dom);
  const root = dom.window.document.querySelector("main")!;
  const samples = Array.from(root.querySelectorAll("article"));
  const signature = createConsensusSignature(samples, root);
  assert.equal(signature.sampleCount, 3);
  assert.equal(signature.layout.alignment, "unknown");
  assert.equal(signature.layout.widthRatio, 0);
  assert.equal(signatureContextMatches(signature, root), true);
  assert.equal(JSON.stringify(signature).includes("One"), false);
});

test("calibration emits only after three distinct valid message samples", () => {
  const dom = new JSDOM(
    `<main class="thread-scroll-container">
      <article data-message-author-role="assistant"><p><span>One</span></p></article>
      <article data-message-author-role="assistant"><p><span>Two</span></p></article>
      <article data-message-author-role="assistant"><p><span>Three</span></p></article>
      <article data-message-author-role="assistant"><p><span>Held out</span></p></article>
      <form data-testid="composer"><span>Draft</span></form>
    </main>`,
    { pretendToBeVisual: true }
  );
  installDomGlobals(dom);
  const controller = new CalibrationController();
  const results: Array<{ role: string; sampleCount: number }> = [];
  controller.start(dom.window.document, "assistant", (role, signature) => {
    results.push({ role, sampleCount: signature.sampleCount });
  });
  const spans = Array.from(dom.window.document.querySelectorAll("article span"));
  spans[0]!.dispatchEvent(
    new dom.window.MouseEvent("click", { bubbles: true, composed: true })
  );
  spans[0]!.dispatchEvent(
    new dom.window.MouseEvent("click", { bubbles: true, composed: true })
  );
  dom.window.document.querySelector("form span")!.dispatchEvent(
    new dom.window.MouseEvent("click", { bubbles: true, composed: true })
  );
  spans[1]!.dispatchEvent(
    new dom.window.MouseEvent("click", { bubbles: true, composed: true })
  );
  assert.equal(results.length, 0);
  spans[2]!.dispatchEvent(
    new dom.window.MouseEvent("click", { bubbles: true, composed: true })
  );
  assert.deepEqual(results, [{ role: "assistant", sampleCount: 3 }]);
});

test("calibration is not saved when only the selected samples match", () => {
  const dom = new JSDOM(
    `<main class="thread-scroll-container">
      <article data-message-author-role="assistant"><p><span>One</span></p></article>
      <article data-message-author-role="assistant"><p><span>Two</span></p></article>
      <article data-message-author-role="assistant"><p><span>Three</span></p></article>
    </main>`,
    { pretendToBeVisual: true }
  );
  installDomGlobals(dom);
  const controller = new CalibrationController();
  let emitted = false;
  controller.start(dom.window.document, "assistant", () => {
    emitted = true;
  });
  for (const span of dom.window.document.querySelectorAll("span")) {
    span.dispatchEvent(
      new dom.window.MouseEvent("click", { bubbles: true, composed: true })
    );
  }
  assert.equal(emitted, false);
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
  assert.equal(
    resolveCalibrationRoot([span, paragraph, bubble, unit], "assistant"),
    null
  );
});

test("calibration rejects protected native surfaces instead of selecting an ancestor", () => {
  const dom = new JSDOM(
    `<main><article data-message-author-role="assistant"><div data-testid="tool-card"><pre><code>fixture</code></pre></div></article></main>`
  );
  installDomGlobals(dom);
  const code = dom.window.document.querySelector("code")!;
  const pre = code.parentElement!;
  const tool = pre.parentElement!;
  const article = tool.parentElement!;

  assert.equal(
    resolveCalibrationRoot([code, pre, tool, article], "assistant"),
    null
  );
});

function installDomGlobals(dom: JSDOM): void {
  Object.assign(globalThis, {
    Element: dom.window.Element
  });
}
