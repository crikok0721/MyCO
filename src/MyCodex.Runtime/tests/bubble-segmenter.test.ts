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

test("one very long paragraph is never cut in the middle", () => {
  const text = `${"Complete sentence. ".repeat(100)}`;
  const article = turn(`<p>${text}</p>`);
  const segments = segmentAssistantProse(article, "Automatic");

  assert.equal(segments.length, 1);
  assert.equal(segments[0]!.element.textContent, text);
  assert.equal(segments[0]!.position, "single");
});
