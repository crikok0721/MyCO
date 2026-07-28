import type {
  AppearanceConfig,
  BubblePalette,
  HostTheme
} from "./types.js";

// Owns one style element and the CSS variables used by all runtime decorations.
const STYLE_ID = "mycodex-runtime-style";

export class StyleManager {
  private styleElement: HTMLStyleElement | null = null;

  install(
    document: Document,
    appearance: AppearanceConfig,
    theme: Exclude<HostTheme, "unknown"> = "dark"
  ): void {
    // Installation is idempotent because health checks may call it after navigation.
    this.styleElement = document.getElementById(STYLE_ID) as HTMLStyleElement | null;
    if (!this.styleElement) {
      this.styleElement = document.createElement("style");
      this.styleElement.id = STYLE_ID;
      this.styleElement.dataset.mycodexCreated = "true";
      (document.head ?? document.documentElement).append(this.styleElement);
    }
    this.styleElement.textContent = runtimeCss();
    this.applyVariables(document, appearance, theme);
  }

  applyVariables(
    document: Document,
    appearance: AppearanceConfig,
    theme: Exclude<HostTheme, "unknown">
  ): void {
    const root = document.documentElement.style;
    const palette =
      theme === "light"
        ? appearance.lightBubblePalette
        : appearance.darkBubblePalette;
    const values: Record<string, string> = {
      "--mc-avatar-size": `${appearance.avatarSize}px`,
      "--mc-avatar-offset-x": `${appearance.avatarOffsetX}px`,
      "--mc-avatar-offset-y": `${appearance.avatarOffsetY}px`,
      "--mc-bubble-radius": `${appearance.bubbleRadius}px`,
      ...paletteVariables(palette),
      "--mc-message-max-width": `${appearance.messageMaxWidth}%`,
      "--mc-message-gap": `${appearance.messageGap}px`,
      "--mc-bubble-padding-x": `${appearance.bubblePaddingX}px`,
      "--mc-bubble-padding-y": `${appearance.bubblePaddingY}px`,
      "--mc-nickname-display": appearance.nicknameVisible ? "block" : "none"
    };
    for (const [property, value] of Object.entries(values)) {
      root.setProperty(property, value);
    }
    document.documentElement.setAttribute("data-mycodex-host-theme", theme);
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
    document.documentElement.setAttribute("data-mycodex-host-theme", theme);
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
      "--mc-avatar-offset-x",
      "--mc-avatar-offset-y",
      "--mc-bubble-radius",
      "--mc-assistant-bubble",
      "--mc-assistant-text",
      "--mc-nickname-color",
      "--mc-avatar-background",
      "--mc-avatar-border",
      "--mc-message-max-width",
      "--mc-message-gap",
      "--mc-bubble-padding-x",
      "--mc-bubble-padding-y",
      "--mc-nickname-display"
    ]) {
      document.documentElement.style.removeProperty(property);
    }
    document.documentElement.removeAttribute("data-mycodex-host-theme");
  }
}

function runtimeCss(): string {
  // Assistant prose receives a bubble; user turns keep the desktop application's native bubble.
  return `
:root {
  --mc-avatar-size: 40px;
  --mc-avatar-offset-x: 0px;
  --mc-avatar-offset-y: 11px;
  --mc-bubble-radius: 14px;
  --mc-assistant-bubble: #222222;
  --mc-assistant-text: #f2f2f2;
  --mc-nickname-color: #9a9a9a;
  --mc-avatar-background: #303030;
  --mc-avatar-border: #FFFFFF14;
  --mc-message-max-width: 66%;
  --mc-message-gap: 28px;
  --mc-bubble-padding-x: 14px;
  --mc-bubble-padding-y: 10px;
  --mc-nickname-display: block;
}
[data-mycodex-turn="true"] {
  position: relative !important;
  box-sizing: border-box !important;
  min-height: calc(var(--mc-avatar-size) + 22px) !important;
  padding-top: 22px !important;
  overflow: visible !important;
}
[data-mycodex-role="assistant"] {
  padding-left: calc(var(--mc-avatar-size) + 12px) !important;
}
[data-mycodex-role="user"] {
  padding-right: calc(var(--mc-avatar-size) + 12px) !important;
}
.mc-avatar {
  position: absolute !important;
  top: var(--mc-avatar-offset-y) !important;
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
[data-mycodex-role="assistant"] > .mc-avatar {
  left: var(--mc-avatar-offset-x) !important;
}
[data-mycodex-role="user"] > .mc-avatar {
  right: var(--mc-avatar-offset-x) !important;
}
.mc-nickname {
  display: var(--mc-nickname-display) !important;
  position: absolute !important;
  top: 0 !important;
  color: var(--mc-nickname-color) !important;
  font: 400 13.5px/18px system-ui, "Segoe UI", sans-serif !important;
  letter-spacing: 0 !important;
  pointer-events: none !important;
  user-select: none !important;
  white-space: nowrap !important;
  z-index: 2 !important;
}
[data-mycodex-role="assistant"] > .mc-nickname {
  left: calc(var(--mc-avatar-size) + 12px) !important;
  top: 0 !important;
}
[data-mycodex-role="user"] > .mc-nickname {
  right: calc(var(--mc-avatar-size) + 12px) !important;
  top: 0 !important;
}
[data-mycodex-prose="assistant"] {
  display: block !important;
  width: fit-content !important;
  max-width: min(var(--mc-message-max-width), 100%) !important;
  box-sizing: border-box !important;
  margin-left: 0 !important;
  margin-right: auto !important;
  padding: var(--mc-bubble-padding-y) var(--mc-bubble-padding-x) !important;
  border-radius: var(--mc-bubble-radius) !important;
  background: var(--mc-assistant-bubble) !important;
  color: var(--mc-assistant-text) !important;
  overflow-wrap: anywhere !important;
}
[data-mycodex-bubble-position="start"] {
  width: min(var(--mc-message-max-width), 100%) !important;
  margin-bottom: 0 !important;
  padding-bottom: calc(var(--mc-bubble-padding-y) / 2) !important;
  border-bottom-left-radius: 0 !important;
  border-bottom-right-radius: 0 !important;
}
[data-mycodex-bubble-position="middle"] {
  width: min(var(--mc-message-max-width), 100%) !important;
  margin-top: 0 !important;
  margin-bottom: 0 !important;
  padding-top: calc(var(--mc-bubble-padding-y) / 2) !important;
  padding-bottom: calc(var(--mc-bubble-padding-y) / 2) !important;
  border-radius: 0 !important;
}
[data-mycodex-bubble-position="end"] {
  width: min(var(--mc-message-max-width), 100%) !important;
  margin-top: 0 !important;
  padding-top: calc(var(--mc-bubble-padding-y) / 2) !important;
  border-top-left-radius: 0 !important;
  border-top-right-radius: 0 !important;
}
[data-mycodex-prose] > :first-child { margin-top: 0 !important; }
[data-mycodex-prose] > :last-child { margin-bottom: 0 !important; }
@media (max-width: 760px) {
  [data-mycodex-prose="assistant"] {
    max-width: 100% !important;
  }
}
[data-mycodex-inspector="hover"] {
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
