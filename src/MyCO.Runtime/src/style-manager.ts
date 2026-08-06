import {
  APPEARANCE_ASSISTANT_AVATAR_OFFSET_Y_BASELINE,
  APPEARANCE_AVATAR_SIZE_BASELINE,
  APPEARANCE_USER_AVATAR_OFFSET_Y_BASELINE
} from "./types.js";
import type {
  AppearanceConfig,
  BubblePalette,
  HostTheme,
  LanguageCode
} from "./types.js";

// Owns one style element and the CSS variables used by all runtime decorations.
const STYLE_ID = "myco-runtime-style";
const FONT_STACKS: Record<LanguageCode, string> = {
  "en-US": '"Segoe UI Variable", "Segoe UI", Arial, sans-serif',
  "zh-CN":
    '"Segoe UI Variable", "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Noto Sans SC", sans-serif',
  "zh-TW":
    '"Segoe UI Variable", "Microsoft JhengHei UI", "Microsoft JhengHei", "Noto Sans CJK TC", "Noto Sans TC", sans-serif',
  "ja-JP":
    '"Segoe UI Variable", "Yu Gothic UI", "Yu Gothic", Meiryo, "Noto Sans CJK JP", "Noto Sans JP", sans-serif'
};

export class StyleManager {
  private styleElement: HTMLStyleElement | null = null;
  private documentLanguageCaptured = false;
  private originalDocumentLanguage: string | null = null;

  install(
    document: Document,
    appearance: AppearanceConfig,
    theme: Exclude<HostTheme, "unknown"> = "dark",
    language: LanguageCode = "en-US"
  ): void {
    // Installation is idempotent because health checks may call it after navigation.
    this.captureDocumentLanguage(document);
    this.styleElement = document.getElementById(STYLE_ID) as HTMLStyleElement | null;
    if (!this.styleElement) {
      this.styleElement = document.createElement("style");
      this.styleElement.id = STYLE_ID;
      this.styleElement.dataset.mycoCreated = "true";
      (document.head ?? document.documentElement).append(this.styleElement);
    }
    this.styleElement.textContent = runtimeCss();
    this.applyVariables(document, appearance, theme, language);
  }

  applyVariables(
    document: Document,
    appearance: AppearanceConfig,
    theme: Exclude<HostTheme, "unknown">,
    language: LanguageCode
  ): void {
    const root = document.documentElement.style;
    const palette =
      theme === "light"
        ? appearance.lightBubblePalette
        : appearance.darkBubblePalette;
    const values: Record<string, string> = {
      "--mc-avatar-size": `${appearance.avatarSize}px`,
      "--mc-assistant-avatar-offset-x": `${appearance.assistantAvatarOffsetX}px`,
      "--mc-assistant-avatar-offset-y": `${appearance.assistantAvatarOffsetY}px`,
      "--mc-user-avatar-offset-x": `${appearance.userAvatarOffsetX}px`,
      "--mc-user-avatar-offset-y": `${appearance.userAvatarOffsetY}px`,
      "--mc-assistant-nickname-offset-x": `${appearance.assistantNicknameOffsetX}px`,
      "--mc-assistant-nickname-offset-y": `${appearance.assistantNicknameOffsetY}px`,
      "--mc-user-nickname-offset-x": `${appearance.userNicknameOffsetX}px`,
      "--mc-user-nickname-offset-y": `${appearance.userNicknameOffsetY}px`,
      "--mc-bubble-radius": `${appearance.bubbleRadius}px`,
      ...paletteVariables(palette),
      "--mc-assistant-bubble-max-width": `${appearance.assistantBubbleMaxWidth}%`,
      "--mc-message-gap": `${appearance.messageGap}px`,
      "--mc-bubble-padding-x": `${appearance.bubblePaddingX}px`,
      "--mc-bubble-padding-y": `${appearance.bubblePaddingY}px`,
      "--mc-nickname-display": appearance.nicknameVisible ? "block" : "none",
      "--mc-font-family": FONT_STACKS[language]
    };
    for (const [property, value] of Object.entries(values)) {
      root.setProperty(property, value);
    }
    document.documentElement.setAttribute("data-myco-host-theme", theme);
    document.documentElement.setAttribute("lang", language);
  }

  applyTheme(
    document: Document,
    appearance: AppearanceConfig,
    theme: Exclude<HostTheme, "unknown">
  ): void {
    const root = document.documentElement.style;
    for (const [property, value] of Object.entries(
      paletteVariables(
        theme === "light"
          ? appearance.lightBubblePalette
          : appearance.darkBubblePalette
      )
    )) {
      root.setProperty(property, value);
    }
    document.documentElement.setAttribute("data-myco-host-theme", theme);
  }

  isInstalled(document: Document): boolean {
    const current = document.getElementById(STYLE_ID);
    const StyleElement = document.defaultView?.HTMLStyleElement;
    return Boolean(StyleElement && current instanceof StyleElement && current.isConnected);
  }

  destroy(document: Document): void {
    this.styleElement?.remove();
    this.styleElement = null;
    for (const property of [
      "--mc-avatar-size",
      "--mc-assistant-avatar-offset-x",
      "--mc-assistant-avatar-offset-y",
      "--mc-user-avatar-offset-x",
      "--mc-user-avatar-offset-y",
      "--mc-assistant-nickname-offset-x",
      "--mc-assistant-nickname-offset-y",
      "--mc-user-nickname-offset-x",
      "--mc-user-nickname-offset-y",
      "--mc-bubble-radius",
      "--mc-assistant-bubble",
      "--mc-assistant-text",
      "--mc-nickname-color",
      "--mc-avatar-background",
      "--mc-avatar-border",
      "--mc-assistant-bubble-max-width",
      "--mc-message-gap",
      "--mc-bubble-padding-x",
      "--mc-bubble-padding-y",
      "--mc-nickname-display",
      "--mc-font-family"
    ]) {
      document.documentElement.style.removeProperty(property);
    }
    document.documentElement.removeAttribute("data-myco-host-theme");
    if (this.documentLanguageCaptured) {
      if (this.originalDocumentLanguage === null) {
        document.documentElement.removeAttribute("lang");
      } else {
        document.documentElement.setAttribute(
          "lang",
          this.originalDocumentLanguage
        );
      }
    }
    this.documentLanguageCaptured = false;
    this.originalDocumentLanguage = null;
  }

  private captureDocumentLanguage(document: Document): void {
    if (this.documentLanguageCaptured) return;
    this.originalDocumentLanguage =
      document.documentElement.getAttribute("lang");
    this.documentLanguageCaptured = true;
  }
}

function runtimeCss(): string {
  // Assistant prose receives a bubble; user turns keep the desktop application's native bubble.
  return `
:root {
  --mc-avatar-size: ${APPEARANCE_AVATAR_SIZE_BASELINE}px;
  --mc-assistant-avatar-offset-x: 0px;
  --mc-assistant-avatar-offset-y: ${APPEARANCE_ASSISTANT_AVATAR_OFFSET_Y_BASELINE}px;
  --mc-user-avatar-offset-x: 0px;
  --mc-user-avatar-offset-y: ${APPEARANCE_USER_AVATAR_OFFSET_Y_BASELINE}px;
  --mc-assistant-nickname-offset-x: 0px;
  --mc-assistant-nickname-offset-y: 0px;
  --mc-user-nickname-offset-x: 0px;
  --mc-user-nickname-offset-y: 0px;
  --mc-bubble-radius: 14px;
  --mc-assistant-bubble: #222222;
  --mc-assistant-text: #f2f2f2;
  --mc-nickname-color: #9a9a9a;
  --mc-avatar-background: #303030;
  --mc-avatar-border: #FFFFFF14;
  --mc-assistant-bubble-max-width: 66%;
  --mc-message-gap: 28px;
  --mc-bubble-padding-x: 14px;
  --mc-bubble-padding-y: 10px;
  --mc-nickname-display: block;
  --mc-font-family: "Segoe UI Variable", "Segoe UI", Arial, sans-serif;
}
[data-myco-turn="true"] {
  position: relative !important;
  box-sizing: border-box !important;
  overflow: visible !important;
  margin-block-end: var(--mc-message-gap) !important;
}
[data-myco-identity-owner="true"] {
  min-height: calc(var(--mc-avatar-size) + 22px) !important;
  padding-top: 22px !important;
}
[data-myco-role="assistant"] {
  padding-left: calc(
    var(--mc-avatar-size) + 12px +
    max(0px, var(--mc-assistant-avatar-offset-x), var(--mc-assistant-nickname-offset-x))
  ) !important;
}
[data-myco-role="user"] {
  padding-right: calc(
    var(--mc-avatar-size) + 12px +
    max(0px, var(--mc-user-avatar-offset-x), var(--mc-user-nickname-offset-x))
  ) !important;
}
[data-myco-role="assistant"][data-myco-identity-owner="true"] {
  min-height: calc(
    var(--mc-avatar-size) + max(22px, var(--mc-assistant-avatar-offset-y))
  ) !important;
  padding-top: max(
    22px,
    calc(18px + max(0px, var(--mc-assistant-nickname-offset-y)))
  ) !important;
}
[data-myco-role="user"][data-myco-identity-owner="true"] {
  min-height: calc(
    var(--mc-avatar-size) + max(22px, var(--mc-user-avatar-offset-y))
  ) !important;
  padding-top: max(
    22px,
    calc(18px + max(0px, var(--mc-user-nickname-offset-y)))
  ) !important;
}
.mc-avatar {
  position: absolute !important;
  width: var(--mc-avatar-size) !important;
  height: var(--mc-avatar-size) !important;
  border-radius: 50% !important;
  object-fit: cover !important;
  background: var(--mc-avatar-background) !important;
  box-shadow: 0 0 0 1px var(--mc-avatar-border) !important;
  pointer-events: none !important;
  user-select: none !important;
  z-index: 2 !important;
}
[data-myco-role="assistant"] > .mc-avatar {
  top: var(--mc-assistant-avatar-offset-y) !important;
  left: var(--mc-assistant-avatar-offset-x) !important;
}
[data-myco-role="user"] > .mc-avatar {
  top: var(--mc-user-avatar-offset-y) !important;
  right: var(--mc-user-avatar-offset-x) !important;
}
.mc-nickname {
  display: var(--mc-nickname-display) !important;
  position: absolute !important;
  top: 0 !important;
  color: var(--mc-nickname-color) !important;
  font: 400 13.5px/18px var(--mc-font-family) !important;
  letter-spacing: 0 !important;
  pointer-events: none !important;
  user-select: none !important;
  white-space: nowrap !important;
  overflow: hidden !important;
  text-overflow: ellipsis !important;
  z-index: 2 !important;
}
[data-myco-role="assistant"] > .mc-nickname {
  left: calc(
    var(--mc-avatar-size) + 12px + var(--mc-assistant-nickname-offset-x)
  ) !important;
  top: var(--mc-assistant-nickname-offset-y) !important;
  max-width: calc(
    100% - var(--mc-avatar-size) - 12px -
    max(0px, var(--mc-assistant-nickname-offset-x))
  ) !important;
}
[data-myco-role="user"] > .mc-nickname {
  right: calc(
    var(--mc-avatar-size) + 12px + var(--mc-user-nickname-offset-x)
  ) !important;
  top: var(--mc-user-nickname-offset-y) !important;
  max-width: calc(
    100% - var(--mc-avatar-size) - 12px -
    max(0px, var(--mc-user-nickname-offset-x))
  ) !important;
}
[data-myco-prose="assistant"] {
  display: block !important;
  width: fit-content(100%) !important;
  min-width: 0 !important;
  max-width: min(var(--mc-assistant-bubble-max-width), 100%) !important;
  box-sizing: border-box !important;
  margin-left: 0 !important;
  margin-right: auto !important;
  padding: var(--mc-bubble-padding-y) var(--mc-bubble-padding-x) !important;
  border-radius: var(--mc-bubble-radius) !important;
  background: var(--mc-assistant-bubble) !important;
  color: var(--mc-assistant-text) !important;
  overflow-wrap: anywhere !important;
  background-clip: padding-box !important;
}
[data-myco-prose] > :first-child { margin-top: 0 !important; }
[data-myco-prose] > :last-child { margin-bottom: 0 !important; }
@media (max-width: 760px) {
  [data-myco-prose="assistant"] {
    max-width: 100% !important;
  }
}
[data-myco-inspector="hover"] {
  outline: 2px solid #8b7cf6 !important;
  outline-offset: 2px !important;
}
`;
}

function paletteVariables(palette: BubblePalette): Record<string, string> {
  return {
    "--mc-assistant-bubble": palette.assistantBubble,
    "--mc-assistant-text": palette.assistantText,
    "--mc-nickname-color": palette.nicknameColor,
    "--mc-avatar-background": palette.avatarBackground,
    "--mc-avatar-border": palette.avatarBorder
  };
}
