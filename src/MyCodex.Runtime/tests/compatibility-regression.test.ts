import assert from "node:assert/strict";
import test from "node:test";
import { JSDOM } from "jsdom";
import {
  classifyTurn,
  createSignature,
  scoreSignature,
  type CalibrationConfig
} from "../src/index.js";
import { scanTurnCandidates } from "../src/scanner.js";

const fixtures = [
  `
  <main>
    <article class="row-a" data-message-author-role="user"><div><p>Hello</p></div></article>
    <article class="row-b" data-message-author-role="assistant"><div><p>Hi</p><pre><code>x</code></pre></div></article>
  </main>`,
  `
  <main>
    <article class="css-f9b17a" data-message-author-role="user"><div><p>Hello</p></div></article>
    <article class="_x7sd92" data-message-author-role="assistant"><div><p>Hi</p><pre><code>x</code></pre></div></article>
  </main>`,
  `
  <main><section>
    <article class="new-hash-17a92f" data-message-author-role="user"><div><p>Hello</p></div></article>
    <article class="new-hash-91c20a" data-message-author-role="assistant"><div><p>Hi</p><pre><code>x</code></pre></div></article>
  </section></main>`,
  `
  <main><section data-view="thread">
    <article class="css-111111" data-role="user"><div><p>Hello</p></div></article>
    <article class="css-222222" data-role="assistant"><div><p>Hi</p><pre><code>x</code></pre></div></article>
  </section></main>`
];

test("calibrated matching survives generated class and wrapper changes", () => {
  const source = new JSDOM(fixtures[0]!);
  const sourceTurns = source.window.document.querySelectorAll("article");
  const calibration: CalibrationConfig = {
    schemaVersion: 1,
    userTurn: createSignature(sourceTurns[0]!),
    assistantTurn: createSignature(sourceTurns[1]!)
  };

  for (const [index, fixture] of fixtures.entries()) {
    const dom = new JSDOM(fixture);
    const turns = dom.window.document.querySelectorAll("article");
    const user = classifyTurn(turns[0]!, calibration);
    const assistant = classifyTurn(turns[1]!, calibration);
    assert.equal(user.role, "user", `fixture ${index + 1} user role`);
    assert.equal(assistant.role, "assistant", `fixture ${index + 1} assistant role`);
    assert.ok(user.confidence >= 0.72, `fixture ${index + 1} user confidence`);
    assert.ok(
      assistant.confidence >= 0.72,
      `fixture ${index + 1} assistant confidence`
    );
  }
});

test("signature scoring does not depend on generated classes", () => {
  const left = new JSDOM(fixtures[0]!).window.document.querySelector("article")!;
  const right = new JSDOM(fixtures[1]!).window.document.querySelector("article")!;
  assert.ok(scoreSignature(createSignature(left), right) >= 0.85);
});

test("current Codex structural turns outrank stale ambiguous calibration", () => {
  const dom = new JSDOM(`
    <main>
      <div class="flex flex-col gap-0">
        <div class="group flex w-full flex-col items-end justify-end gap-1">
          <div class="bg-token-foreground/5 rounded-2xl"><p>User prompt</p></div>
          <div><button>Copy</button></div>
        </div>
        <div class="group flex min-w-0 flex-col">
          <h4 class="sr-only">Assistant</h4>
          <div class="_markdownContent_newhash_42"><p>Assistant response</p></div>
        </div>
      </div>
    </main>
  `);
  const document = dom.window.document;
  const ambiguous = createSignature(document.querySelector("main > div")!);
  const calibration: CalibrationConfig = {
    schemaVersion: 1,
    userTurn: ambiguous,
    assistantTurn: ambiguous
  };
  const turns = scanTurnCandidates(document.querySelector("main")!, calibration);

  assert.equal(turns.length, 2);
  assert.equal(classifyTurn(turns[0]!, calibration).role, "user");
  assert.equal(classifyTurn(turns[1]!, calibration).role, "assistant");
});
