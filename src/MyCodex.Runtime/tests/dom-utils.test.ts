// Verifies generated classes are ignored and fingerprints never include conversation text.
import assert from "node:assert/strict";
import test from "node:test";
import { JSDOM } from "jsdom";
import {
  createSignature,
  isLikelyGeneratedClass,
  stableClassTokens,
  structuralFingerprint
} from "../src/index.js";

test("generated class filtering ignores hashes but preserves semantic classes", () => {
  assert.equal(isLikelyGeneratedClass("css-a8f32c"), true);
  assert.equal(isLikelyGeneratedClass("_x7sd92"), true);
  assert.equal(isLikelyGeneratedClass("message-row"), false);

  const dom = new JSDOM(
    '<article class="css-a8f32c message-row _x7sd92"></article>'
  );
  const element = dom.window.document.querySelector("article")!;
  assert.deepEqual(stableClassTokens(element), ["message-row"]);
});

test("structural fingerprints and signatures never contain chat text", () => {
  const secret = "PRIVATE CHAT CONTENT 92b341";
  const dom = new JSDOM(
    `<article data-message-author-role="assistant"><p>${secret}</p><button>Copy</button></article>`
  );
  const element = dom.window.document.querySelector("article")!;
  const fingerprint = structuralFingerprint(element);
  const signature = createSignature(element);
  assert.equal(fingerprint.includes(secret), false);
  assert.equal(JSON.stringify(signature).includes(secret), false);
  assert.equal(signature.capabilities.hasButtons, true);
  assert.equal(signature.capabilities.hasMarkdown, true);
});
