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

test("unit signatures retain text-free role evidence from their descendants", () => {
  const dom = new JSDOM(
    `<main>
       <div id="user" data-content-search-unit-key="private-user-id">
         <div data-user-message-bubble><p>User text</p></div>
       </div>
       <div id="assistant" data-content-search-unit-key="private-assistant-id">
         <div class="markdownContent-generated"><p>Assistant text</p></div>
       </div>
     </main>`
  );
  const user = createSignature(dom.window.document.querySelector("#user")!);
  const assistant = createSignature(
    dom.window.document.querySelector("#assistant")!
  );

  assert.equal(user.stableAttributes["data-user-message-bubble"], "present");
  assert.equal(assistant.stableAttributes["data-content-type"], "prose");
  assert.notEqual(user.fingerprint, assistant.fingerprint);
  assert.equal(user.fingerprint.includes("private-user-id"), false);
  assert.equal(assistant.fingerprint.includes("private-assistant-id"), false);
});
