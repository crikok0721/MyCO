import assert from "node:assert/strict";
import test from "node:test";
import { JSDOM } from "jsdom";
import { segmentAssistantProse } from "../src/bubble-segmenter.js";

function turn(markup: string): Element {
  const dom = new JSDOM(`<article>${markup}</article>`);
  return dom.window.document.querySelector("article")!;
}

test("automatic mode keeps a heading with following prose and short context", () => {
  const article = turn(
    "<h2>Release notes</h2><p>First explanation.</p><p>Related detail.</p>"
  );
  const segments = segmentAssistantProse(article, "Automatic");

  assert.deepEqual(
    segments.map(({ element, group, position }) => [
      element.tagName,
      group,
      position
    ]),
    [
      ["H2", 0, "start"],
      ["P", 0, "middle"],
      ["P", 0, "end"]
    ]
  );
});

test("lists and quotes remain atomic semantic blocks", () => {
  const article = turn(
    "<p>Steps:</p><ol><li>One</li><li>Two</li></ol>" +
      "<blockquote><p>A complete quote.</p></blockquote>"
  );
  const segments = segmentAssistantProse(article, "Automatic");

  assert.deepEqual(
    segments.map(({ element }) => element.tagName),
    ["P", "OL", "BLOCKQUOTE"]
  );
  assert.equal(segments.some(({ element }) => element.tagName === "LI"), false);
});

test("protected code tables and math are never marked or split", () => {
  const article = turn(
    "<p>Before.</p><pre><code>const x = 1;</code></pre>" +
      "<table><tr><td>Cell</td></tr></table>" +
      "<div class='katex'>x+y</div><p>After.</p>"
  );
  const segments = segmentAssistantProse(article, "Whole");

  assert.deepEqual(
    segments.map(({ element, group }) => [element.textContent, group]),
    [
      ["Before.", 0],
      ["After.", 1]
    ]
  );
  assert.equal(segments.some(({ element }) => element.closest("pre,table,.katex")), false);
});

test("whole mode joins all contiguous safe prose", () => {
  const article = turn("<h3>Title</h3><p>One.</p><ul><li>Two.</li></ul>");
  const segments = segmentAssistantProse(article, "Whole");

  assert.deepEqual(
    segments.map(({ group, position }) => [group, position]),
    [
      [0, "start"],
      [0, "middle"],
      [0, "end"]
    ]
  );
});

test("whole mode keeps a mixed Markdown surface split at protected children", () => {
  const article = turn(
    `<div class="markdownContent-current">
       <p>Opening.</p>
       <pre><code>const value = 1;</code></pre>
       <p>Closing.</p>
     </div>`
  );

  const segments = segmentAssistantProse(article, "Whole");

  assert.deepEqual(
    segments.map(({ element }) => element.tagName),
    ["P", "P"]
  );
  assert.equal(segments.some(({ element }) => element.querySelector("pre,code")), false);
});

test("whole mode does not mark a prose layout shell around the Markdown surface", () => {
  const article = turn(
    `<div data-content-type="prose" class="group flex min-w-0 flex-col">
       <div class="markdownContent-current">
         <p>Long assistant response.</p>
       </div>
     </div>`
  );

  const segments = segmentAssistantProse(article, "Whole");

  assert.equal(segments.length, 1);
  assert.equal(
    segments[0]!.element.classList.contains("markdownContent-current"),
    true
  );
  assert.equal(
    segments[0]!.element.getAttribute("data-content-type"),
    null
  );
});

test("one very long paragraph is never cut in the middle", () => {
  const text = `${"Complete sentence. ".repeat(100)}`;
  const article = turn(`<p>${text}</p>`);
  const segments = segmentAssistantProse(article, "Automatic");

  assert.equal(segments.length, 1);
  assert.equal(segments[0]!.element.textContent, text);
  assert.equal(segments[0]!.position, "single");
});

test("whole mode rejects tool and status shells even when they contain prose", () => {
  const article = turn(
    `<div data-content-type="tool"><p>Tool explanation</p></div>
     <div data-content-type="status"><p>Status text</p></div>
     <div class="markdownContent-current"><p>Assistant prose</p></div>`
  );

  const segments = segmentAssistantProse(article, "Whole");

  assert.deepEqual(
    segments.map(({ element }) => element.textContent?.trim()),
    ["Assistant prose"]
  );
  assert.equal(
    segments.some(({ element }) => element.closest("[data-content-type=tool]")),
    false
  );
});

test("long continuous strings remain a single safe prose surface", () => {
  const article = turn(
    `<div class="markdownContent-current"><p>${"C:\\very-long-path\\".repeat(160)}</p></div>`
  );
  const automatic = segmentAssistantProse(article, "Automatic");
  const whole = segmentAssistantProse(article, "Whole");

  assert.equal(automatic.length, 1);
  assert.equal(whole.length, 1);
  assert.equal(whole[0]!.element.classList.contains("markdownContent-current"), true);
});

test("automatic mode keeps a paragraph containing inline code", () => {
  const article = turn(
    "<p>Use <code>npm run check</code> before saving.</p>"
  );

  const segments = segmentAssistantProse(article, "Automatic");

  assert.deepEqual(segments.map(({ element }) => element.tagName), ["P"]);
  assert.equal(segments[0]!.element.querySelector("code")!.hasAttribute("data-myco-prose"), false);
});

test("whole mode keeps a pure Markdown surface with inline code", () => {
  const article = turn(
    `<div class="markdownContent-current"><p>Use <code>npm run check</code> before saving.</p></div>`
  );

  const segments = segmentAssistantProse(article, "Whole");

  assert.equal(segments.length, 1);
  assert.equal(segments[0]!.element.classList.contains("markdownContent-current"), true);
});

test("groups split across nested protected wrappers", () => {
  const article = turn(
    `<section>
       <div><p>Opening prose.</p></div>
       <div data-content-type="tool"><p>Tool output</p></div>
       <div><p>Closing prose.</p></div>
     </section>`
  );

  const segments = segmentAssistantProse(article, "Whole");

  assert.deepEqual(
    segments.map(({ element, group, position }) => [element.tagName, group, position]),
    [["P", 0, "single"], ["P", 1, "single"]]
  );
});
