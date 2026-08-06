// Exercises installation, decoration boundaries, mutation refresh, cleanup, and self-repair.
import assert from "node:assert/strict";
import test from "node:test";
import { JSDOM } from "jsdom";
import { bootstrap } from "../src/index.js";
import { MyCORuntime } from "../src/runtime.js";
import {
  LEGACY_RUNTIME_SYMBOL,
  RUNTIME_SYMBOL,
  RUNTIME_VERSION,
  defaultConfig,
  type MyCORuntimeApi
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

test("defaultConfig uses the recalibrated appearance baseline", () => {
  const appearance = defaultConfig().appearance;

  assert.equal(appearance.avatarSize, 35);
  assert.equal(appearance.assistantAvatarOffsetY, 11);
  assert.equal(appearance.userAvatarOffsetY, -4);
});

test("runtime bubbles prose only and preserves native tool surfaces", () => {
  const dom = fixture();
  const runtime = new MyCORuntime(dom.window.document);
  const config = defaultConfig();
  config.assistant.name = "Luna";
  config.user.name = "Avery";
  runtime.applyConfig(config);

  const assistant = dom.window.document.querySelector(
    '[data-message-author-role="assistant"]'
  )!;
  const prose = assistant.querySelectorAll('[data-myco-prose="assistant"]');
  assert.equal(prose.length, 2);
  assert.equal(assistant.querySelector("pre")!.hasAttribute("data-myco-prose"), false);
  assert.equal(
    assistant.querySelector("[data-testid=tool-card]")!.hasAttribute("data-myco-prose"),
    false
  );
  assert.equal(
    assistant.querySelector("[data-content-type=status]")!.hasAttribute(
      "data-myco-prose"
    ),
    false
  );
  assert.equal(
    assistant.querySelector("[role=toolbar]")!.hasAttribute("data-myco-prose"),
    false
  );
  assert.equal(assistant.querySelector(".mc-nickname")!.textContent, "Luna");

  const user = dom.window.document.querySelector(
    '[data-message-author-role="user"]'
  )!;
  const nativeUserBubble = user.querySelector(".native-user-bubble")!;
  assert.equal(user.querySelector('[data-myco-prose="user"]'), null);
  assert.equal(nativeUserBubble.hasAttribute("data-myco-prose"), false);
  assert.equal(nativeUserBubble.classList.contains("rounded-2xl"), true);
  assert.equal(user.querySelector(".mc-nickname")!.textContent, "Avery");
});

test("whole mode decorates the Markdown surface, not its flex layout shell", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body><main>
      <article data-message-author-role="assistant">
        <div data-content-type="prose" class="group flex min-w-0 flex-col">
          <div class="markdownContent-current"><p>${"Long answer. ".repeat(120)}</p></div>
        </div>
      </article>
    </main></body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCORuntime(dom.window.document);
  const config = defaultConfig();
  config.appearance.bubbleDisplayMode = "Whole";
  runtime.applyConfig(config);

  const article = dom.window.document.querySelector("article")!;
  assert.equal(article.querySelectorAll('[data-myco-prose="assistant"]').length, 1);
  assert.equal(
    article.querySelector("[data-content-type=prose]")!.hasAttribute("data-myco-prose"),
    false
  );
  assert.equal(
    article.querySelector(".markdownContent-current")!.hasAttribute("data-myco-prose"),
    true
  );
  runtime.destroy();
});

test("mixed Markdown gives bubbles only to prose blocks in both modes", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body><main>
      <article data-message-author-role="assistant">
        <div class="markdownContent-current">
          <h2>Summary</h2>
          <p>Readable assistant prose.</p>
          <pre><code>const copied = true;</code></pre>
          <button>Copy</button>
          <p>Closing prose.</p>
        </div>
      </article>
    </main></body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCORuntime(dom.window.document);
  const config = defaultConfig();
  for (const mode of ["Automatic", "Whole"] as const) {
    config.appearance.bubbleDisplayMode = mode;
    runtime.applyConfig(config);
    const article = dom.window.document.querySelector("article")!;
    assert.equal(article.querySelectorAll('[data-myco-prose="assistant"]').length, 3);
    assert.equal(article.querySelector("pre")!.hasAttribute("data-myco-prose"), false);
    assert.equal(article.querySelector("code")!.hasAttribute("data-myco-prose"), false);
    assert.equal(article.querySelector("button")!.hasAttribute("data-myco-prose"), false);
  }
  runtime.destroy();
});

test("long code and its copy control remain native in both modes", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body><main>
      <article data-message-author-role="assistant">
        <p>Explanation before the code.</p>
        <div data-testid="code-card">
          <pre><code>${"const value = 1; ".repeat(400)}</code></pre>
          <button data-testid="copy-code">Copy</button>
        </div>
        <p>Explanation after the code.</p>
      </article>
    </main></body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCORuntime(dom.window.document);
  const config = defaultConfig();
  const article = dom.window.document.querySelector("article")!;

  for (const mode of ["Automatic", "Whole"] as const) {
    config.appearance.bubbleDisplayMode = mode;
    runtime.applyConfig(config);
    assert.equal(article.querySelector("pre")!.hasAttribute("data-myco-prose"), false);
    assert.equal(article.querySelector("code")!.hasAttribute("data-myco-prose"), false);
    assert.equal(
      article.querySelector("[data-testid=copy-code]")!.hasAttribute("data-myco-prose"),
      false
    );
    assert.equal(article.querySelectorAll('[data-myco-prose="assistant"]').length, 2);
  }
  runtime.destroy();
});

test("long continuous prose uses bounded wrapping CSS without a fixed height", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body><main>
      <article data-message-author-role="assistant">
        <p>${"C:/a-very-long-unbroken-path/".repeat(220)}</p>
      </article>
    </main></body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCORuntime(dom.window.document);
  runtime.applyConfig(defaultConfig());
  const css = dom.window.document.querySelector<HTMLStyleElement>(
    "#myco-runtime-style"
  )!.textContent!;
  const proseRule = css.match(
    /\[data-myco-prose="assistant"\]\s*\{([\s\S]*?)\}/
  )?.[1] ?? "";

  assert.match(proseRule, /width:\s*max-content/);
  assert.match(proseRule, /min-width:\s*0/);
  assert.match(proseRule, /max-width:\s*min\(/);
  assert.match(proseRule, /height:\s*auto/);
  assert.match(proseRule, /flex:\s*0 1 auto/);
  assert.match(proseRule, /align-self:\s*flex-start/);
  assert.match(proseRule, /overflow-wrap:\s*anywhere/);
  assert.doesNotMatch(proseRule, /height:\s*100%/);
  runtime.destroy();
});

test("install is idempotent and destroy restores injected DOM", () => {
  const dom = fixture();
  const runtime = new MyCORuntime(dom.window.document);
  runtime.install();
  runtime.install();

  assert.equal(dom.window.document.querySelectorAll("#myco-runtime-style").length, 1);
  assert.equal(dom.window.document.querySelectorAll(".mc-nickname").length, 2);
  runtime.destroy();
  assert.equal(dom.window.document.querySelector("#myco-runtime-style"), null);
  assert.equal(dom.window.document.querySelector(".mc-nickname"), null);
  assert.equal(dom.window.document.querySelector("[data-myco-turn]"), null);
  assert.equal(dom.window.document.querySelector("[data-myco-prose]"), null);
  assert.ok(dom.window.document.querySelector("pre code"));
  assert.ok(dom.window.document.querySelector("button"));
});

test("theme changes update palette variables without duplicating decorations", async () => {
  const dom = fixture();
  dom.window.document.documentElement.dataset.theme = "dark";
  const runtime = new MyCORuntime(dom.window.document);
  const config = defaultConfig();
  runtime.applyConfig(config);

  for (let index = 0; index < 30; index++) {
    dom.window.document.documentElement.dataset.theme =
      index % 2 === 0 ? "light" : "dark";
    await new Promise((resolve) => dom.window.setTimeout(resolve, 60));
  }

  const root = dom.window.document.documentElement;
  assert.equal(root.getAttribute("data-myco-host-theme"), "dark");
  assert.equal(
    root.style.getPropertyValue("--mc-assistant-bubble"),
    config.appearance.darkBubblePalette.assistantBubble
  );
  assert.equal(dom.window.document.querySelectorAll("#myco-runtime-style").length, 1);
  assert.equal(dom.window.document.querySelectorAll(".mc-avatar").length, 2);
  assert.equal(dom.window.document.querySelectorAll(".mc-nickname").length, 2);

  runtime.destroy();
  assert.equal(root.hasAttribute("data-myco-host-theme"), false);
  assert.equal(root.style.getPropertyValue("--mc-assistant-bubble"), "");
});

test("locale changes use isolated CJK font stacks and restore html language", () => {
  const cases = [
    {
      language: "zh-CN",
      required: "Microsoft YaHei UI",
      forbidden: ["Yu Gothic", "Microsoft JhengHei", "Noto Sans CJK JP"],
      sample:
        "恢复到默认设置 设置 语言 消息 窗口 字体 骨 门 直 置 关 开 图 语 MyCO 0.99.1"
    },
    {
      language: "zh-TW",
      required: "Microsoft JhengHei UI",
      forbidden: ["Microsoft YaHei", "Yu Gothic", "Noto Sans CJK SC"],
      sample:
        "恢復預設設定 設定 語言 訊息 視窗 字體 骨 門 直 置 關 開 圖 語 MyCO 0.99.1"
    },
    {
      language: "ja-JP",
      required: "Yu Gothic UI",
      forbidden: ["Microsoft YaHei", "Microsoft JhengHei", "Noto Sans CJK TC"],
      sample:
        "デフォルト設定に戻す 設定 言語 メッセージ ウィンドウ フォント 骨 門 直 置 関 開 図 語 MyCO 0.99.1"
    },
    {
      language: "en-US",
      required: "Segoe UI Variable",
      forbidden: ["Microsoft YaHei", "Microsoft JhengHei", "Yu Gothic"],
      sample: "Restore default settings Settings Language Messages Window Font MyCO 0.99.1"
    }
  ] as const;

  for (const testCase of cases) {
    const dom = new JSDOM(
      "<!doctype html><html lang=\"host-language\"><head></head><body><main>" +
        "<article data-message-author-role=\"assistant\"><p>" +
        testCase.sample +
        "</p></article>" +
        "</main></body></html>",
      { url: "app://-/index.html", pretendToBeVisual: true }
    );
    const runtime = new MyCORuntime(dom.window.document);
    const config = {
      ...defaultConfig(),
      language: testCase.language
    };

    runtime.applyConfig(config);

    const root = dom.window.document.documentElement;
    const stack = root.style.getPropertyValue("--mc-font-family");
    const css = dom.window.document.querySelector<HTMLStyleElement>(
      "#myco-runtime-style"
    )!.textContent!;
    assert.equal(root.lang, testCase.language);
    assert.equal(
      dom.window.document.querySelector("article p")?.textContent,
      testCase.sample
    );
    assert.match(stack, new RegExp(testCase.required));
    for (const forbidden of testCase.forbidden) {
      assert.doesNotMatch(stack, new RegExp(forbidden));
    }
    assert.match(css, /var\(--mc-font-family\)/);
    assert.doesNotMatch(css, /system-ui/);

    runtime.destroy();

    assert.equal(root.lang, "host-language");
    assert.equal(root.style.getPropertyValue("--mc-font-family"), "");
  }
});

test("runtime rejects an unreadable bubble palette", () => {
  const dom = fixture();
  const runtime = new MyCORuntime(dom.window.document);
  const config = defaultConfig();
  config.appearance.lightBubblePalette.assistantBubble = "#ffffff";
  config.appearance.lightBubblePalette.assistantText = "#ffffff";

  assert.throws(
    () => runtime.applyConfig(config),
    /contrast must be at least 4\.5:1/
  );
  assert.equal(
    dom.window.document.getElementById("myco-runtime-style"),
    null
  );
});

test("role placement variables reapply independently and destroy removes them", () => {
  const dom = fixture();
  const runtime = new MyCORuntime(dom.window.document);
  const first = defaultConfig();
  first.assistant.avatar = "data:image/png;base64,AA==";
  first.user.avatar = "data:image/png;base64,AA==";
  first.assistant.name = "Assistant nickname that must truncate before prose";
  first.user.name = "User nickname that must truncate before prose";
  first.appearance.assistantAvatarOffsetX = 5;
  first.appearance.assistantAvatarOffsetY = 7;
  first.appearance.userAvatarOffsetX = 11;
  first.appearance.userAvatarOffsetY = 13;
  first.appearance.assistantNicknameOffsetX = 17;
  first.appearance.assistantNicknameOffsetY = 19;
  first.appearance.userNicknameOffsetX = 23;
  first.appearance.userNicknameOffsetY = 28;
  first.appearance.assistantBubbleMaxWidth = 72;
  first.appearance.messageGap = 31;

  runtime.applyConfig(first);

  const rootStyle = dom.window.document.documentElement.style;
  assert.equal(rootStyle.getPropertyValue("--mc-assistant-avatar-offset-x"), "5px");
  assert.equal(rootStyle.getPropertyValue("--mc-assistant-avatar-offset-y"), "7px");
  assert.equal(rootStyle.getPropertyValue("--mc-user-avatar-offset-x"), "11px");
  assert.equal(rootStyle.getPropertyValue("--mc-user-avatar-offset-y"), "13px");
  assert.equal(rootStyle.getPropertyValue("--mc-assistant-nickname-offset-x"), "17px");
  assert.equal(rootStyle.getPropertyValue("--mc-assistant-nickname-offset-y"), "19px");
  assert.equal(rootStyle.getPropertyValue("--mc-user-nickname-offset-x"), "23px");
  assert.equal(rootStyle.getPropertyValue("--mc-user-nickname-offset-y"), "28px");
  assert.equal(rootStyle.getPropertyValue("--mc-assistant-bubble-max-width"), "72%");
  assert.equal(rootStyle.getPropertyValue("--mc-message-gap"), "31px");

  const second = structuredClone(first);
  second.appearance.assistantAvatarOffsetX = -1;
  second.appearance.assistantAvatarOffsetY = 2;
  second.appearance.userAvatarOffsetX = -3;
  second.appearance.userAvatarOffsetY = 4;
  second.appearance.assistantNicknameOffsetX = -5;
  second.appearance.assistantNicknameOffsetY = 6;
  second.appearance.userNicknameOffsetX = -7;
  second.appearance.userNicknameOffsetY = 8;
  second.appearance.assistantBubbleMaxWidth = 55;
  second.appearance.messageGap = 24;
  runtime.applyConfig(second);

  assert.equal(rootStyle.getPropertyValue("--mc-assistant-avatar-offset-x"), "-1px");
  assert.equal(rootStyle.getPropertyValue("--mc-assistant-avatar-offset-y"), "2px");
  assert.equal(rootStyle.getPropertyValue("--mc-user-avatar-offset-x"), "-3px");
  assert.equal(rootStyle.getPropertyValue("--mc-user-avatar-offset-y"), "4px");
  assert.equal(rootStyle.getPropertyValue("--mc-assistant-nickname-offset-x"), "-5px");
  assert.equal(rootStyle.getPropertyValue("--mc-assistant-nickname-offset-y"), "6px");
  assert.equal(rootStyle.getPropertyValue("--mc-user-nickname-offset-x"), "-7px");
  assert.equal(rootStyle.getPropertyValue("--mc-user-nickname-offset-y"), "8px");
  assert.equal(rootStyle.getPropertyValue("--mc-assistant-bubble-max-width"), "55%");
  assert.equal(rootStyle.getPropertyValue("--mc-message-gap"), "24px");

  const runtimeStyle = dom.window.document.querySelector<HTMLStyleElement>(
    "#myco-runtime-style"
  )!.textContent!;
  assert.match(runtimeStyle, /top: var\(--mc-assistant-avatar-offset-y\)/);
  assert.match(runtimeStyle, /top: var\(--mc-user-avatar-offset-y\)/);
  assert.match(runtimeStyle, /left: var\(--mc-assistant-avatar-offset-x\)/);
  assert.match(runtimeStyle, /right: var\(--mc-user-avatar-offset-x\)/);
  assert.match(runtimeStyle, /var\(--mc-message-gap\)/);
  assert.match(
    runtimeStyle,
    /var\(--mc-avatar-size\) \+ max\(22px, var\(--mc-assistant-avatar-offset-y\)\)/
  );
  assert.match(
    runtimeStyle,
    /var\(--mc-avatar-size\) \+ max\(22px, var\(--mc-user-avatar-offset-y\)\)/
  );
  assert.match(runtimeStyle, /max-width: min\(var\(--mc-assistant-bubble-max-width\), 100%\)/);
  assert.match(runtimeStyle, /text-overflow: ellipsis/);
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
  for (const nickname of Array.from(
    dom.window.document.querySelectorAll<HTMLElement>(".mc-nickname")
  )) {
    const style = dom.window.getComputedStyle(nickname);
    assert.equal(style.whiteSpace, "nowrap");
    assert.equal(style.overflow, "hidden");
    assert.equal(style.textOverflow, "ellipsis");
  }
  runtime.destroy();
  for (const property of [
    "--mc-assistant-avatar-offset-x",
    "--mc-assistant-avatar-offset-y",
    "--mc-user-avatar-offset-x",
    "--mc-user-avatar-offset-y",
    "--mc-assistant-nickname-offset-x",
    "--mc-assistant-nickname-offset-y",
    "--mc-user-nickname-offset-x",
    "--mc-user-nickname-offset-y",
    "--mc-assistant-bubble-max-width"
  ]) {
    assert.equal(rootStyle.getPropertyValue(property), "");
  }
});

test("second apply immediately wins on existing streaming and virtualized turns in both modes", () => {
  for (const finalMode of ["Automatic", "Whole"] as const) {
    const dom = new JSDOM(
      `<!doctype html><html><head></head><body><main>
        <article data-message-author-role="assistant"><p id="stream">Opening.</p><p>Detail.</p></article>
        <article data-message-author-role="user"><div data-user-message-bubble><p>Question</p></div></article>
      </main></body></html>`,
      { url: "app://-/index.html", pretendToBeVisual: true }
    );
    const runtime = new MyCORuntime(dom.window.document);
    const first = defaultConfig();
    first.assistant.name = "First assistant";
    first.user.name = "First user";
    first.appearance.bubbleDisplayMode =
      finalMode === "Automatic" ? "Whole" : "Automatic";
    runtime.applyConfig(first);

    dom.window.document.querySelector("#stream")!.textContent =
      "Streaming response expanded. ".repeat(20);
    dom.window.document.querySelector("main")!.innerHTML = `
      <article data-message-author-role="assistant">
        <div class="markdownContent-current"><p>Virtualized final response.</p></div>
      </article>
      <article data-message-author-role="user"><div data-user-message-bubble><p>Virtualized question</p></div></article>`;

    const second = defaultConfig();
    second.assistant.name = "Second assistant";
    second.user.name = "Second user";
    second.appearance.bubbleDisplayMode = finalMode;
    second.appearance.assistantBubbleMaxWidth = 58;
    const diagnostics = runtime.applyConfig(second);

    const assistant = dom.window.document.querySelector(
      '[data-message-author-role="assistant"]'
    )!;
    const user = dom.window.document.querySelector(
      '[data-message-author-role="user"]'
    )!;
    assert.equal(assistant.querySelector(".mc-nickname")!.textContent, "Second assistant");
    assert.equal(user.querySelector(".mc-nickname")!.textContent, "Second user");
    assert.ok(assistant.querySelector('[data-myco-prose="assistant"]'));
    assert.equal(user.querySelector("[data-myco-prose]"), null);
    assert.equal(diagnostics.decoratedAssistantTurns, 1);
    assert.equal(diagnostics.decoratedUserTurns, 1);
    assert.equal(
      dom.window.document.documentElement.style.getPropertyValue(
        "--mc-assistant-bubble-max-width"
      ),
      "58%"
    );
    runtime.destroy();
  }
});

test("mutation observer decorates newly appended turns", async () => {
  const dom = fixture();
  const runtime = new MyCORuntime(dom.window.document);
  runtime.install();
  const article = dom.window.document.createElement("article");
  article.setAttribute("data-message-author-role", "user");
  article.innerHTML = "<p>Later</p>";
  dom.window.document.querySelector("main")!.append(article);
  await new Promise((resolve) => dom.window.setTimeout(resolve, 140));
  assert.equal(article.getAttribute("data-myco-role"), "user");
  runtime.destroy();
});

test("ensureActive repairs a removed style and a replaced conversation root", () => {
  const dom = fixture();
  const runtime = new MyCORuntime(dom.window.document);
  runtime.applyConfig(defaultConfig());

  dom.window.document.querySelector("#myco-runtime-style")!.remove();
  const replacement = dom.window.document.createElement("main");
  replacement.innerHTML = `
    <div data-content-search-unit-key="assistant-unit" class="group flex min-w-0 flex-col">
      <h4 class="sr-only">Assistant</h4>
      <div class="_markdownContent_changed_42"><p>New response</p></div>
    </div>`;
  dom.window.document.querySelector("main")!.replaceWith(replacement);

  const health = runtime.ensureActive();
  const assistant = replacement.querySelector(".group")!;
  assert.equal(health.active, true);
  assert.equal(health.repaired, true);
  assert.ok(dom.window.document.querySelector("#myco-runtime-style"));
  assert.equal(assistant.getAttribute("data-myco-role"), "assistant");
  assert.ok(assistant.querySelector('[data-myco-prose="assistant"]'));
  runtime.destroy();
});

test("virtualized replacement and mode changes keep prose marker counts stable", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body><main>
      <article data-message-author-role="assistant"><p>First response</p></article>
    </main></body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCORuntime(dom.window.document);
  const config = defaultConfig();
  runtime.applyConfig(config);
  const main = dom.window.document.querySelector("main")!;
  assert.equal(main.querySelectorAll('[data-myco-prose="assistant"]').length, 1);

  config.appearance.bubbleDisplayMode = "Whole";
  runtime.applyConfig(config);
  assert.equal(main.querySelectorAll('[data-myco-prose="assistant"]').length, 1);

  main.innerHTML =
    '<article data-message-author-role="assistant"><div class="markdownContent-current"><p>Second response</p></div></article>';
  runtime.refresh();
  assert.equal(main.querySelectorAll('[data-myco-prose="assistant"]').length, 1);
  assert.equal(main.querySelectorAll("[data-myco-turn]").length, 1);
  runtime.destroy();
  assert.equal(main.querySelectorAll("[data-myco-prose], [data-myco-turn], .mc-avatar, .mc-nickname").length, 0);
});

test("streaming text growth does not repeatedly regroup existing blocks", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body><main>
      <article data-message-author-role="assistant">
        <p id="first">Short opening.</p>
        <p id="second">Related detail.</p>
      </article>
    </main></body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCORuntime(dom.window.document);
  runtime.applyConfig(defaultConfig());
  const first = dom.window.document.querySelector("#first")!;
  const second = dom.window.document.querySelector("#second")!;
  assert.equal(first.getAttribute("data-myco-bubble-position"), "start");
  assert.equal(second.getAttribute("data-myco-bubble-position"), "end");

  first.textContent = "Complete sentence. ".repeat(100);
  runtime.refresh();

  assert.equal(first.getAttribute("data-myco-bubble-position"), "start");
  assert.equal(second.getAttribute("data-myco-bubble-position"), "end");
  runtime.destroy();
});

test("a new protected barrier invalidates cached bubble positions", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body><main>
      <article data-message-author-role="assistant">
        <p id="before">Opening prose.</p>
        <p id="after">Closing prose.</p>
      </article>
    </main></body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCORuntime(dom.window.document);
  const config = defaultConfig();
  config.appearance.bubbleDisplayMode = "Whole";
  runtime.applyConfig(config);

  const article = dom.window.document.querySelector("article")!;
  const barrier = dom.window.document.createElement("pre");
  barrier.textContent = "native code";
  article.insertBefore(barrier, dom.window.document.querySelector("#after"));
  runtime.refresh();

  assert.equal(article.querySelector("#before")!.getAttribute("data-myco-bubble-position"), "single");
  assert.equal(article.querySelector("#after")!.getAttribute("data-myco-bubble-position"), "single");
  assert.equal(barrier.hasAttribute("data-myco-prose"), false);
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
  const runtime = new MyCORuntime(dom.window.document);
  const diagnostics = runtime.applyConfig(defaultConfig());
  const user = dom.window.document.querySelector(
    '[data-content-search-unit-key^="unit-user"]'
  )!;
  const assistant = dom.window.document.querySelector(
    '[data-content-search-unit-key^="unit-assistant"]'
  )!;

  assert.equal(user.getAttribute("data-myco-role"), "user");
  assert.equal(assistant.getAttribute("data-myco-role"), "assistant");
  assert.ok(user.querySelector(".mc-avatar"));
  assert.ok(user.querySelector(".mc-nickname"));
  assert.equal(user.querySelector("[data-user-message-bubble]")!.classList.length, 0);
  assert.equal(user.querySelector("[data-myco-prose]"), null);
  assert.equal(
    assistant.querySelectorAll('[data-myco-prose="assistant"]').length,
    1
  );
  assert.equal(
    assistant.querySelector("[data-testid=tool-card]")!.hasAttribute(
      "data-myco-prose"
    ),
    false
  );
  assert.equal(diagnostics.compatibility, "compatible");
  assert.equal(diagnostics.decoratedUserTurns, 1);
  assert.equal(diagnostics.decoratedAssistantTurns, 1);
  assert.equal(diagnostics.assistantBubbleBlocks, 1);
  runtime.destroy();
});

test("each assistant unit owns one identity inside a logical turn", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body>
      <main class="thread-scroll-container">
        <section data-content-search-turn-key="logical-turn">
          <div data-content-search-unit-key="user-unit">
            <div data-user-message-bubble><p>User prompt</p></div>
          </div>
          <div data-content-search-unit-key="assistant-unit-1">
            <div class="markdownContent-first"><p>First assistant section.</p></div>
          </div>
          <div data-testid="tool-card">Tool status</div>
          <div data-content-search-unit-key="assistant-unit-2">
            <div class="markdownContent-second"><p>Second assistant section.</p></div>
          </div>
        </section>
      </main>
    </body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCORuntime(dom.window.document);
  const config = defaultConfig();
  config.appearance.bubbleDisplayMode = "Whole";
  runtime.applyConfig(config);

  const section = dom.window.document.querySelector(
    "[data-content-search-turn-key]"
  )!;
  assert.equal(section.querySelectorAll(".mc-avatar").length, 3);
  assert.equal(section.querySelectorAll(".mc-nickname").length, 3);
  assert.equal(
    section.querySelectorAll('[data-myco-role="assistant"]').length,
    2
  );
  assert.equal(
    section.querySelectorAll('[data-myco-prose="assistant"]').length,
    2
  );
  assert.equal(
    section.querySelectorAll(
      '[data-myco-role="assistant"][data-myco-identity-owner="true"]'
    ).length,
    2
  );
  const tool = section.querySelector("[data-testid=tool-card]")!;
  assert.equal(tool.querySelector(".mc-avatar"), null);
  assert.equal(tool.querySelector(".mc-nickname"), null);

  runtime.refresh();
  runtime.refresh();
  assert.equal(section.querySelectorAll(".mc-avatar").length, 3);
  assert.equal(section.querySelectorAll(".mc-nickname").length, 3);

  runtime.destroy();
  assert.equal(section.querySelector(".mc-avatar"), null);
  assert.equal(section.querySelector(".mc-nickname"), null);
});

test("empty workspace and composer surfaces never receive identities", () => {
  const dom = new JSDOM(
    `<!doctype html><html><head></head><body>
      <nav><p>Project navigation</p></nav>
      <main><div class="group flex min-w-0 flex-col"><p>What should we build?</p></div></main>
      <form data-testid="composer"><textarea></textarea><p>Draft helper</p></form>
    </body></html>`,
    { url: "app://-/index.html", pretendToBeVisual: true }
  );
  const runtime = new MyCORuntime(dom.window.document);
  const diagnostics = runtime.applyConfig(defaultConfig());
  assert.equal(dom.window.document.querySelector(".mc-avatar"), null);
  assert.equal(dom.window.document.querySelector(".mc-nickname"), null);
  assert.equal(dom.window.document.querySelector("[data-myco-turn]"), null);
  assert.equal(diagnostics.decoratedUserTurns, 0);
  assert.equal(diagnostics.decoratedAssistantTurns, 0);
  runtime.destroy();
});

test("reconcile removes duplicate and orphaned identity nodes", () => {
  const dom = fixture();
  const runtime = new MyCORuntime(dom.window.document);
  runtime.applyConfig(defaultConfig());
  const assistant = dom.window.document.querySelector(
    '[data-message-author-role="assistant"]'
  )!;
  assistant.querySelector(".mc-avatar")!.cloneNode(true);
  const duplicate = assistant.querySelector(".mc-avatar")!.cloneNode(true);
  assistant.append(duplicate);
  const orphan = dom.window.document.createElement("span");
  orphan.className = "mc-nickname";
  orphan.dataset.mycoCreated = "true";
  dom.window.document.body.append(orphan);

  runtime.refresh();

  assert.equal(assistant.querySelectorAll(":scope > .mc-avatar").length, 1);
  assert.equal(dom.window.document.body.contains(orphan), false);
  runtime.destroy();
});

test("bubble CSS keeps every rendered surface fully rounded", () => {
  const dom = fixture();
  const runtime = new MyCORuntime(dom.window.document);
  runtime.applyConfig(defaultConfig());
  const css = dom.window.document.querySelector<HTMLStyleElement>(
    "#myco-runtime-style"
  )!.textContent!;

  assert.doesNotMatch(css, /border-(?:top|bottom)-(?:left|right)-radius:\s*0/);
  assert.doesNotMatch(css, /border-radius:\s*0\s*!important/);
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
  } as unknown as MyCORuntimeApi;
  Object.defineProperty(dom.window, RUNTIME_SYMBOL, {
    configurable: true,
    value: legacy
  });
  Object.defineProperty(dom.window, "__MYCO_RUNTIME__", {
    configurable: true,
    value: legacy
  });

  const runtime = bootstrap(dom.window.document);
  assert.equal(legacyDestroyed, true);
  assert.notEqual(runtime, legacy);
  assert.equal(runtime.getVersion().version, RUNTIME_VERSION);
  assert.equal(dom.window.__MYCO_RUNTIME__, runtime);
  runtime.destroy();
});

test("bootstrap destroys the pre-rename runtime before installing MyCO", () => {
  const dom = fixture();
  let legacyDestroyed = false;
  const legacy = {
    getVersion: () => ({ version: "0.99.0", protocolVersion: 1 }),
    destroy: () => {
      legacyDestroyed = true;
    }
  } as unknown as MyCORuntimeApi;
  Object.defineProperty(dom.window, LEGACY_RUNTIME_SYMBOL, {
    configurable: true,
    value: legacy
  });
  Object.defineProperty(dom.window, "__MYCODEX_RUNTIME__", {
    configurable: true,
    value: legacy
  });

  const runtime = bootstrap(dom.window.document);

  assert.equal(legacyDestroyed, true);
  assert.equal(
    (dom.window as unknown as Record<PropertyKey, unknown>)[
      LEGACY_RUNTIME_SYMBOL
    ],
    undefined
  );
  assert.equal(dom.window.__MYCODEX_RUNTIME__, undefined);
  assert.equal(dom.window.__MYCO_RUNTIME__, runtime);
  runtime.destroy();
});
