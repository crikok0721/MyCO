// Exercises installation, decoration boundaries, mutation refresh, cleanup, and self-repair.
import assert from "node:assert/strict";
import test from "node:test";
import { JSDOM } from "jsdom";
import { bootstrap } from "../src/index.js";
import { MyCodexRuntime } from "../src/runtime.js";
import {
  RUNTIME_SYMBOL,
  RUNTIME_VERSION,
  defaultConfig,
  type MyCodexRuntimeApi
} from "../src/types.js";

function fixture(): JSDOM {
  return new JSDOM(
    `
    <!doctype html><html><head></head><body><main>
      <article data-message-author-role="assistant">
        <div data-content-type="status">Processed 14s</div>
        <p>I inspected the file.</p>
        <pre><code>const value = 1;</code></pre>
        <div data-testid="tool-card"><p>Tool output</p></div>
        <p>The change is complete.</p>
        <div role="toolbar"><button>Copy</button></div>
      </article>
      <article data-message-author-role="user">
        <div class="native-user-bubble rounded-2xl"><p>Short message</p></div>
      </article>
    </main></body></html>
    `,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
}

test("runtime bubbles prose only and preserves native tool surfaces", () => {
  const dom = fixture();
  const runtime = new MyCodexRuntime(dom.window.document);
  const config = defaultConfig();
  config.assistant.name = "Luna";
  config.user.name = "Avery";
  runtime.applyConfig(config);

  const assistant = dom.window.document.querySelector(
    '[data-message-author-role="assistant"]'
  )!;
  const prose = assistant.querySelectorAll('[data-mycodex-prose="assistant"]');
  assert.equal(prose.length, 2);
  assert.equal(assistant.querySelector("pre")!.hasAttribute("data-mycodex-prose"), false);
  assert.equal(
    assistant.querySelector("[data-testid=tool-card]")!.hasAttribute("data-mycodex-prose"),
    false
  );
  assert.equal(
    assistant.querySelector("[data-content-type=status]")!.hasAttribute(
      "data-mycodex-prose"
    ),
    false
  );
  assert.equal(
    assistant.querySelector("[role=toolbar]")!.hasAttribute("data-mycodex-prose"),
    false
  );
  assert.equal(assistant.querySelector(".mc-nickname")!.textContent, "Luna");

  const user = dom.window.document.querySelector(
    '[data-message-author-role="user"]'
  )!;
  const nativeUserBubble = user.querySelector(".native-user-bubble")!;
  assert.equal(user.querySelector('[data-mycodex-prose="user"]'), null);
  assert.equal(nativeUserBubble.hasAttribute("data-mycodex-prose"), false);
  assert.equal(nativeUserBubble.classList.contains("rounded-2xl"), true);
  assert.equal(user.querySelector(".mc-nickname")!.textContent, "Avery");
});

test("install is idempotent and destroy restores injected DOM", () => {
  const dom = fixture();
  const runtime = new MyCodexRuntime(dom.window.document);
  runtime.install();
  runtime.install();

  assert.equal(dom.window.document.querySelectorAll("#mycodex-runtime-style").length, 1);
  assert.equal(dom.window.document.querySelectorAll(".mc-nickname").length, 2);
  runtime.destroy();
  assert.equal(dom.window.document.querySelector("#mycodex-runtime-style"), null);
  assert.equal(dom.window.document.querySelector(".mc-nickname"), null);
  assert.equal(dom.window.document.querySelector("[data-mycodex-turn]"), null);
  assert.equal(dom.window.document.querySelector("[data-mycodex-prose]"), null);
  assert.ok(dom.window.document.querySelector("pre code"));
  assert.ok(dom.window.document.querySelector("button"));
});

test("theme changes update palette variables without duplicating decorations", async () => {
  const dom = fixture();
  dom.window.document.documentElement.dataset.theme = "dark";
  const runtime = new MyCodexRuntime(dom.window.document);
  const config = defaultConfig();
  runtime.applyConfig(config);

  for (let index = 0; index < 30; index++) {
    dom.window.document.documentElement.dataset.theme =
      index % 2 === 0 ? "light" : "dark";
    await new Promise((resolve) => dom.window.setTimeout(resolve, 60));
  }

  const root = dom.window.document.documentElement;
  assert.equal(root.getAttribute("data-mycodex-host-theme"), "dark");
  assert.equal(
    root.style.getPropertyValue("--mc-assistant-bubble"),
    config.appearance.darkBubblePalette.assistantBubble
  );
  assert.equal(dom.window.document.querySelectorAll("#mycodex-runtime-style").length, 1);
  assert.equal(dom.window.document.querySelectorAll(".mc-avatar").length, 2);
  assert.equal(dom.window.document.querySelectorAll(".mc-nickname").length, 2);

  runtime.destroy();
  assert.equal(root.hasAttribute("data-mycodex-host-theme"), false);
  assert.equal(root.style.getPropertyValue("--mc-assistant-bubble"), "");
});

test("runtime rejects an unreadable bubble palette", () => {
  const dom = fixture();
  const runtime = new MyCodexRuntime(dom.window.document);
  const config = defaultConfig();
  config.appearance.lightBubblePalette.assistantBubble = "#ffffff";
  config.appearance.lightBubblePalette.assistantText = "#ffffff";

  assert.throws(
    () => runtime.applyConfig(config),
    /contrast must be at least 4\.5:1/
  );
  assert.equal(
    dom.window.document.getElementById("mycodex-runtime-style"),
    null
  );
});

test("assistant and user avatars use circular crop and configurable offsets", () => {
  const dom = fixture();
  const runtime = new MyCodexRuntime(dom.window.document);
  const config = defaultConfig();
  config.assistant.avatar = "data:image/png;base64,AA==";
  config.user.avatar = "data:image/png;base64,AA==";
  config.appearance.avatarOffsetX = 7;
  config.appearance.avatarOffsetY = 13;

  runtime.applyConfig(config);

  const rootStyle = dom.window.document.documentElement.style;
  assert.equal(rootStyle.getPropertyValue("--mc-avatar-offset-x"), "7px");
  assert.equal(rootStyle.getPropertyValue("--mc-avatar-offset-y"), "13px");
  const runtimeStyle = dom.window.document.querySelector<HTMLStyleElement>(
    "#mycodex-runtime-style"
  )!.textContent!;
  assert.match(runtimeStyle, /top: var\(--mc-avatar-offset-y\)/);
  assert.match(runtimeStyle, /left: var\(--mc-avatar-offset-x\)/);
  assert.match(runtimeStyle, /right: var\(--mc-avatar-offset-x\)/);
  const avatars = Array.from(
    dom.window.document.querySelectorAll<HTMLImageElement>(".mc-avatar")
  );
  assert.equal(avatars.length, 2);
  for (const avatar of avatars) {
    const style = dom.window.getComputedStyle(avatar);
    assert.equal(style.borderRadius, "50%");
    assert.equal(style.objectFit, "cover");
    assert.equal(avatar.hidden, false);
  }
  runtime.destroy();
});

test("mutation observer decorates newly appended turns", async () => {
  const dom = fixture();
  const runtime = new MyCodexRuntime(dom.window.document);
  runtime.install();
  const article = dom.window.document.createElement("article");
  article.setAttribute("data-message-author-role", "user");
  article.innerHTML = "<p>Later</p>";
  dom.window.document.querySelector("main")!.append(article);
  await new Promise((resolve) => dom.window.setTimeout(resolve, 140));
  assert.equal(article.getAttribute("data-mycodex-role"), "user");
  runtime.destroy();
});

test("ensureActive repairs a removed style and a replaced conversation root", () => {
  const dom = fixture();
  const runtime = new MyCodexRuntime(dom.window.document);
  runtime.applyConfig(defaultConfig());

  dom.window.document.querySelector("#mycodex-runtime-style")!.remove();
  const replacement = dom.window.document.createElement("main");
  replacement.innerHTML = `
    <div class="group flex min-w-0 flex-col">
      <h4 class="sr-only">Assistant</h4>
      <div class="_markdownContent_changed_42"><p>New response</p></div>
    </div>`;
  dom.window.document.querySelector("main")!.replaceWith(replacement);

  const health = runtime.ensureActive();
  const assistant = replacement.querySelector(".group")!;
  assert.equal(health.active, true);
  assert.equal(health.repaired, true);
  assert.ok(dom.window.document.querySelector("#mycodex-runtime-style"));
  assert.equal(assistant.getAttribute("data-mycodex-role"), "assistant");
  assert.ok(assistant.querySelector('[data-mycodex-prose="assistant"]'));
  runtime.destroy();
});

test("current Codex unit anchors decorate identities and assistant prose only", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body>
      <main data-testid="navigation-pane"><p>Navigation</p></main>
      <main class="thread-scroll-container">
        <section data-content-search-turn-key="turn-private-id">
          <div data-content-search-unit-key="unit-user-private-id">
            <div data-user-message-bubble><p>User prompt</p></div>
          </div>
          <div data-content-search-unit-key="unit-assistant-private-id">
            <div class="markdownContent-current"><p>Assistant response</p></div>
            <div data-testid="tool-card"><p>Tool output</p></div>
          </div>
        </section>
      </main>
    </body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCodexRuntime(dom.window.document);
  const diagnostics = runtime.applyConfig(defaultConfig());
  const user = dom.window.document.querySelector(
    '[data-content-search-unit-key^="unit-user"]'
  )!;
  const assistant = dom.window.document.querySelector(
    '[data-content-search-unit-key^="unit-assistant"]'
  )!;

  assert.equal(user.getAttribute("data-mycodex-role"), "user");
  assert.equal(assistant.getAttribute("data-mycodex-role"), "assistant");
  assert.ok(user.querySelector(".mc-avatar"));
  assert.ok(user.querySelector(".mc-nickname"));
  assert.equal(user.querySelector("[data-user-message-bubble]")!.classList.length, 0);
  assert.equal(user.querySelector("[data-mycodex-prose]"), null);
  assert.equal(
    assistant.querySelectorAll('[data-mycodex-prose="assistant"]').length,
    1
  );
  assert.equal(
    assistant.querySelector("[data-testid=tool-card]")!.hasAttribute(
      "data-mycodex-prose"
    ),
    false
  );
  assert.equal(diagnostics.compatibility, "compatible");
  assert.equal(diagnostics.decoratedUserTurns, 1);
  assert.equal(diagnostics.decoratedAssistantTurns, 1);
  assert.equal(diagnostics.assistantBubbleBlocks, 1);
  runtime.destroy();
});

test("bootstrap replaces an older runtime build in the same renderer", () => {
  const dom = fixture();
  let legacyDestroyed = false;
  const legacy = {
    getVersion: () => ({ version: "0.1.0-alpha", protocolVersion: 1 }),
    destroy: () => {
      legacyDestroyed = true;
    }
  } as unknown as MyCodexRuntimeApi;
  Object.defineProperty(dom.window, RUNTIME_SYMBOL, {
    configurable: true,
    value: legacy
  });
  Object.defineProperty(dom.window, "__MYCODEX_RUNTIME__", {
    configurable: true,
    value: legacy
  });

  const runtime = bootstrap(dom.window.document);
  assert.equal(legacyDestroyed, true);
  assert.notEqual(runtime, legacy);
  assert.equal(runtime.getVersion().version, RUNTIME_VERSION);
  assert.equal(dom.window.__MYCODEX_RUNTIME__, runtime);
  runtime.destroy();
});
