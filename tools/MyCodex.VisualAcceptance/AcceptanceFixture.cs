using System.Text.Json;

namespace MyCodex.VisualAcceptanceTool;

// Synthetic-only fixture: no real conversation text or profile data is read.
internal static class AcceptanceFixture
{
    public static string BuildInstallScript(string runId)
    {
        var runIdJson = JsonSerializer.Serialize(runId);
        return FixtureTemplate.Replace(
            "__RUN_ID_JSON__",
            runIdJson,
            StringComparison.Ordinal);
    }

    public static string BuildThemeScript(string theme)
    {
        var themeJson = JsonSerializer.Serialize(theme);
        return ThemeTemplate.Replace(
            "__THEME_JSON__",
            themeJson,
            StringComparison.Ordinal);
    }

    public const string AutomatedCheckScript =
        """
        (() => {
          const root = document.getElementById("mycodex-visual-acceptance");
          const user = document.getElementById("acceptance-user");
          const assistant = document.getElementById("acceptance-assistant");
          const avatar = assistant?.querySelector(":scope > .mc-avatar");
          const avatarStyle = avatar ? getComputedStyle(avatar) : null;
          const code = document.getElementById("acceptance-code");
          const tool = document.getElementById("acceptance-tool");
          const diff = document.getElementById("acceptance-diff");
          const status = document.getElementById("acceptance-status");
          const toolbar = document.getElementById("acceptance-toolbar");
          const nativeUserBubble = document.getElementById("acceptance-native-user-bubble");
          return {
            fixtureVisible: !!root && getComputedStyle(root).display !== "none",
            runMarkerVisible: !!document.getElementById("acceptance-run-id"),
            styleInstalled: !!document.getElementById("mycodex-runtime-style"),
            userRole: user?.getAttribute("data-mycodex-role") === "user",
            assistantRole:
              assistant?.getAttribute("data-mycodex-role") === "assistant",
            userIdentity:
              (user?.querySelectorAll(":scope > .mc-avatar,:scope > .mc-nickname")
                .length ?? 0) === 2,
            assistantIdentity:
              (assistant?.querySelectorAll(":scope > .mc-avatar,:scope > .mc-nickname")
                .length ?? 0) === 2,
            assistantBubble:
              document.getElementById("acceptance-markdown")
                ?.querySelector('[data-mycodex-prose="assistant"]') != null,
            userBubblePreserved:
              !nativeUserBubble?.hasAttribute("data-mycodex-prose") &&
              nativeUserBubble?.classList.contains("acceptance-native-user-bubble"),
            avatarCircular:
              avatarStyle?.borderRadius === "50%" &&
              avatarStyle?.objectFit === "cover",
            codePreserved: !code?.hasAttribute("data-mycodex-prose"),
            toolPreserved: !tool?.hasAttribute("data-mycodex-prose"),
            diffPreserved: !diff?.hasAttribute("data-mycodex-prose"),
            statusPreserved: !status?.hasAttribute("data-mycodex-prose"),
            toolbarPreserved: !toolbar?.hasAttribute("data-mycodex-prose")
          };
        })()
        """;

    public const string DestroyCheckScript =
        """
        (() => ({
          runtimeStyleRemoved: !document.getElementById("mycodex-runtime-style"),
          createdNodesRemoved:
            document.querySelectorAll("[data-mycodex-created=true]").length === 0,
          turnMarkersRemoved:
            document.querySelectorAll("[data-mycodex-turn]").length === 0,
          proseMarkersRemoved:
            document.querySelectorAll("[data-mycodex-prose]").length === 0,
          fixtureStillVisible:
            !!document.getElementById("mycodex-visual-acceptance")
        }))()
        """;

    private const string ThemeTemplate =
        """
        (async () => {
          const theme = __THEME_JSON__;
          if (theme !== "dark" && theme !== "light") {
            throw new TypeError("Unsupported fixture theme");
          }
          document.documentElement.dataset.theme = theme;
          document.documentElement.style.background =
            theme === "light" ? "#f7f7f8" : "#0f1012";
          await new Promise(resolve => setTimeout(resolve, 180));
          const prose = document.getElementById("acceptance-markdown");
          return {
            hostAttributeApplied:
              document.documentElement.dataset.theme === theme,
            runtimeThemeApplied:
              document.documentElement.getAttribute("data-mycodex-host-theme") === theme,
            styleSingleton:
              document.querySelectorAll("#mycodex-runtime-style").length === 1,
            assistantBubbleReadable:
              !!prose &&
              getComputedStyle(prose).color !==
                getComputedStyle(prose).backgroundColor
          };
        })()
        """;

    private const string FixtureTemplate =
        """
        (() => {
          const runId = __RUN_ID_JSON__;
          document.title = `MyCodex Visual Acceptance ${runId}`;
          document.documentElement.dataset.theme = "dark";
          document.documentElement.style.background = "#0f1012";
          document.body.innerHTML = `
            <div id="mycodex-visual-acceptance">
              <style id="mycodex-acceptance-fixture-style">
                html, body { min-width: 420px; min-height: 100%; }
                body {
                  margin: 0;
                  background: #0f1012;
                  color: #ececf1;
                  font-family: system-ui, "Segoe UI", sans-serif;
                }
                #mycodex-visual-acceptance {
                  min-height: 100vh;
                  background:
                    radial-gradient(circle at 15% 0%, #22283a 0, transparent 34rem),
                    #0f1012;
                }
                .acceptance-header {
                  position: sticky;
                  top: 0;
                  z-index: 50;
                  display: flex;
                  align-items: center;
                  justify-content: space-between;
                  gap: 16px;
                  padding: 14px 22px;
                  border-bottom: 1px solid #34363d;
                  background: rgba(15,16,18,.96);
                  backdrop-filter: blur(10px);
                }
                .acceptance-title { font-size: 16px; font-weight: 700; }
                #acceptance-run-id {
                  border: 1px solid #7c6df2;
                  border-radius: 999px;
                  padding: 5px 10px;
                  color: #c9c2ff;
                  font: 600 12px/1.2 ui-monospace, Consolas, monospace;
                }
                .acceptance-subtitle {
                  max-width: 920px;
                  margin: 18px auto 0;
                  padding: 0 24px;
                  color: #9da2ae;
                  font-size: 13px;
                }
                .thread-scroll-container {
                  box-sizing: border-box;
                  width: min(920px, calc(100% - 42px));
                  margin: 0 auto;
                  padding: 28px 36px 80px;
                }
                .acceptance-unit { margin: 0 0 30px; }
                .acceptance-user-unit {
                  display: flex;
                  flex-direction: column;
                  align-items: flex-end;
                }
                .acceptance-native-user-bubble {
                  max-width: 70%;
                  border: 1px solid #353840;
                  border-radius: 18px;
                  padding: 10px 14px;
                  background: #25272c;
                  color: #f5f5f7;
                }
                .acceptance-native-user-bubble p { margin: 0; }
                .acceptance-markdown { line-height: 1.65; }
                .acceptance-markdown p { margin: 0 0 12px; }
                .acceptance-markdown p:last-child { margin-bottom: 0; }
                .acceptance-native-panel {
                  margin-top: 12px;
                  border: 1px solid #34363d;
                  border-radius: 10px;
                  background: #17191d;
                  overflow: hidden;
                }
                .acceptance-panel-title {
                  padding: 8px 11px;
                  border-bottom: 1px solid #34363d;
                  color: #aeb3bf;
                  font-size: 12px;
                }
                pre {
                  margin: 0;
                  padding: 13px;
                  overflow: auto;
                  background: #111318;
                  color: #cdd6f4;
                  font: 13px/1.55 ui-monospace, Consolas, monospace;
                }
                .acceptance-tool-body, .acceptance-diff-body {
                  padding: 12px;
                  font: 13px/1.5 ui-monospace, Consolas, monospace;
                }
                .acceptance-diff-add { color: #9bd59b; }
                .acceptance-diff-del { color: #ef9a9a; }
                .acceptance-status {
                  margin-top: 12px;
                  color: #9da2ae;
                  font-size: 12px;
                }
                .acceptance-toolbar {
                  display: flex;
                  gap: 8px;
                  margin-top: 10px;
                }
                .acceptance-toolbar button, .acceptance-tool-body button {
                  border: 1px solid #444852;
                  border-radius: 7px;
                  padding: 6px 10px;
                  background: #22252b;
                  color: #e7e8ec;
                }
                @media (max-width: 720px) {
                  .thread-scroll-container {
                    width: calc(100% - 18px);
                    padding: 24px 12px 60px;
                  }
                  .acceptance-native-user-bubble { max-width: 86%; }
                  .acceptance-header { align-items: flex-start; flex-direction: column; }
                }
                html[data-theme="light"] body {
                  background: #f7f7f8;
                  color: #202124;
                }
                html[data-theme="light"] #mycodex-visual-acceptance {
                  background:
                    radial-gradient(circle at 15% 0%, #e7eafb 0, transparent 34rem),
                    #f7f7f8;
                }
                html[data-theme="light"] .acceptance-header {
                  border-bottom-color: #d7d9df;
                  background: rgba(247,247,248,.96);
                }
                html[data-theme="light"] .acceptance-subtitle {
                  color: #646b77;
                }
                html[data-theme="light"] .acceptance-native-user-bubble {
                  border-color: #d6d9e0;
                  background: #e9eaed;
                  color: #202124;
                }
                html[data-theme="light"] .acceptance-native-panel {
                  border-color: #d7d9df;
                  background: #ffffff;
                }
                html[data-theme="light"] .acceptance-panel-title {
                  border-bottom-color: #d7d9df;
                  color: #606773;
                }
                html[data-theme="light"] pre {
                  background: #f2f3f5;
                  color: #24262b;
                }
                html[data-theme="light"] .acceptance-status {
                  color: #646b77;
                }
                html[data-theme="light"] .acceptance-toolbar button,
                html[data-theme="light"] .acceptance-tool-body button {
                  border-color: #cfd2d9;
                  background: #f5f6f8;
                  color: #25272c;
                }
              </style>
              <header class="acceptance-header">
                <div>
                  <div class="acceptance-title">MyCodex 双实例视觉验收 · Codex B</div>
                  <div style="color:#8f95a3;font-size:12px;margin-top:3px">
                    合成内容，不读取用户聊天或真实 Profile
                  </div>
                </div>
                <div id="acceptance-run-id">RUN ${runId}</div>
              </header>
              <div class="acceptance-subtitle">
                请检查头像、昵称、Assistant 气泡，以及 User 原生气泡、代码、工具、
                Diff、状态和操作栏是否保持独立。
              </div>
              <main id="acceptance-thread" class="thread-scroll-container">
                <section data-content-search-turn-key="synthetic-turn-user">
                  <div id="acceptance-user"
                       class="acceptance-unit acceptance-user-unit"
                       data-content-search-unit-key="synthetic-unit-user">
                    <div id="acceptance-native-user-bubble"
                         class="acceptance-native-user-bubble"
                         data-user-message-bubble>
                      <p>这是一条合成 User 消息，原生气泡不应被 MyCodex 重绘</p>
                    </div>
                  </div>
                </section>

                <section data-content-search-turn-key="synthetic-turn-assistant">
                  <div id="acceptance-assistant"
                       class="acceptance-unit"
                       data-content-search-unit-key="synthetic-unit-assistant">
                    <div id="acceptance-markdown" class="acceptance-markdown markdownContent-acceptance">
                      <p>这是 Assistant 的普通 Markdown 正文。它应当获得清晰、圆角适中且不遮挡头像与昵称的气泡。</p>
                      <p>第二段用于检查多段落间距。下面是一段较长文本，用来确认窄窗口和宽窗口下都能自然换行，不会溢出、截断或覆盖旁边的头像：MyCodex visual acceptance keeps the official application shell intact while applying a scoped presentation layer only to assistant prose.</p>
                    </div>

                    <div id="acceptance-code" class="acceptance-native-panel">
                      <div class="acceptance-panel-title">code / pre · 应保持原样</div>
                      <pre><code>function safeTarget(runId) {
                        return ownedPid &amp;&amp; isolatedProfile &amp;&amp; runId;
                      }</code></pre>
                    </div>

                    <div id="acceptance-tool" class="acceptance-native-panel" data-testid="tool-card">
                      <div class="acceptance-panel-title">工具卡片 · 应保持官方样式边界</div>
                      <div class="acceptance-tool-body">
                        synthetic_tool completed
                        <button type="button">查看详情</button>
                      </div>
                    </div>

                    <div id="acceptance-diff" class="acceptance-native-panel" data-content-type="diff">
                      <div class="acceptance-panel-title">Diff · 不应套用普通 prose 气泡</div>
                      <div class="acceptance-diff-body">
                        <div class="acceptance-diff-del">- unsafe global process shutdown</div>
                        <div class="acceptance-diff-add">+ exact owned PID shutdown</div>
                      </div>
                    </div>

                    <div id="acceptance-status" class="acceptance-status" role="status">
                      已处理 2.4s · synthetic acceptance status
                    </div>
                    <div id="acceptance-toolbar" class="acceptance-toolbar" role="toolbar">
                      <button type="button">复制</button>
                      <button type="button">赞</button>
                      <button type="button">踩</button>
                    </div>
                  </div>
                </section>
              </main>
            </div>`;
          return {
            runId,
            title: document.title,
            root: !!document.getElementById("mycodex-visual-acceptance")
          };
        })()
        """;
}
