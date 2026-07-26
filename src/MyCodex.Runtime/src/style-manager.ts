import type { AppearanceConfig } from "./types.js";

// Owns one style element and the CSS variables used by all runtime decorations.
const STYLE_ID = "mycodex-runtime-style";

export class StyleManager {
  private styleElement: HTMLStyleElement | null = null;

  install(document: Document, appearance: AppearanceConfig): void {
    // Installation is idempotent because health checks may call it after navigation.
    this.styleElement = document.getElementById(STYLE_ID) as HTMLStyleElement | null;
    if (!this.styleElement) {
      this.styleElement = document.createElement("style");
      this.styleElement.id = STYLE_ID;
      this.styleElement.dataset.mycodexCreated = "true";
      (document.head ?? document.documentElement).append(this.styleElement);
    }
    this.styleElement.textContent = runtimeCss();
    this.applyVariables(document, appearance);
  }

  applyVariables(document: Document, appearance: AppearanceConfig): void {
    const root = document.documentElement.style;
    const values: Record<string, string> = {
      "--mc-avatar-size": `${appearance.avatarSize}px`,
      "--mc-bubble-radius": `${appearance.bubbleRadius}px`,
      "--mc-user-bubble": appearance.userBubble,
      "--mc-assistant-bubble": appearance.assistantBubble,
      "--mc-user-text": appearance.userText,
      "--mc-assistant-text": appearance.assistantText,
      "--mc-nickname-color": appearance.nicknameColor,
      "--mc-message-max-width": `${appearance.messageMaxWidth}%`,
      "--mc-message-gap": `${appearance.messageGap}px`,
      "--mc-bubble-padding-x": `${appearance.bubblePaddingX}px`,
      "--mc-bubble-padding-y": `${appearance.bubblePaddingY}px`,
      "--mc-nickname-display": appearance.nicknameVisible ? "block" : "none"
    };
    for (const [property, value] of Object.entries(values)) {
      root.setProperty(property, value);
    }
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
      "--mc-bubble-radius",
      "--mc-user-bubble",
      "--mc-assistant-bubble",
      "--mc-user-text",
      "--mc-assistant-text",
      "--mc-nickname-color",
      "--mc-message-max-width",
      "--mc-message-gap",
      "--mc-bubble-padding-x",
      "--mc-bubble-padding-y",
      "--mc-nickname-display"
    ]) {
      document.documentElement.style.removeProperty(property);
    }
  }
}

function runtimeCss(): string {
  // Assistant prose receives a bubble; user turns keep the desktop application's native bubble.
  return `
:root {
  --mc-avatar-size: 40px;
  --mc-bubble-radius: 14px;
  --mc-user-bubble: #242424;
  --mc-assistant-bubble: #222222;
  --mc-user-text: #f5f5f5;
  --mc-assistant-text: #f2f2f2;
  --mc-nickname-color: #9a9a9a;
  --mc-message-max-width: 66%;
  --mc-message-gap: 28px;
  --mc-bubble-padding-x: 14px;
  --mc-bubble-padding-y: 10px;
  --mc-nickname-display: block;
}
[data-mycodex-turn="true"] {
  position: relative !important;
  box-sizing: border-box !important;
}
.mc-avatar {
  position: absolute !important;
  top: 0 !important;
  width: var(--mc-avatar-size) !important;
  height: var(--mc-avatar-size) !important;
  border-radius: 50% !important;
  object-fit: cover !important;
  background: #303030 !important;
  box-shadow: 0 0 0 1px rgba(255,255,255,.08) !important;
  pointer-events: none !important;
  user-select: none !important;
  z-index: 2 !important;
}
[data-mycodex-role="assistant"] > .mc-avatar {
  left: calc(-1 * (var(--mc-avatar-size) + 12px)) !important;
}
[data-mycodex-role="user"] > .mc-avatar {
  right: calc(-1 * (var(--mc-avatar-size) + 12px)) !important;
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
  left: 0 !important;
  top: -22px !important;
}
[data-mycodex-role="user"] > .mc-nickname {
  right: 0 !important;
  top: -22px !important;
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
[data-mycodex-prose] > :first-child { margin-top: 0 !important; }
[data-mycodex-prose] > :last-child { margin-bottom: 0 !important; }
@media (max-width: 760px) {
  [data-mycodex-role="assistant"] > .mc-avatar {
    left: 0 !important;
  }
  [data-mycodex-prose="assistant"] {
    margin-left: calc(var(--mc-avatar-size) + 12px) !important;
    max-width: calc(100% - var(--mc-avatar-size) - 12px) !important;
  }
  [data-mycodex-role="user"] > .mc-avatar,
  [data-mycodex-role="user"] > .mc-nickname {
    display: none !important;
  }
}
[data-mycodex-inspector="hover"] {
  outline: 2px solid #8b7cf6 !important;
  outline-offset: 2px !important;
}
`;
}
