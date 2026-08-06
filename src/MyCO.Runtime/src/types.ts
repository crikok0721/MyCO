// Values are injected from eng/MyCO.Version.props by build.mjs.
declare const __MYCO_VERSION__: string;
declare const __MYCO_PROTOCOL_VERSION__: number;
declare const __MYCO_CONFIG_SCHEMA_VERSION__: number;
declare const __MYCO_CALIBRATION_SCHEMA_VERSION__: number;

export const RUNTIME_VERSION = __MYCO_VERSION__;
export const PROTOCOL_VERSION = __MYCO_PROTOCOL_VERSION__;
export const CONFIG_SCHEMA_VERSION = __MYCO_CONFIG_SCHEMA_VERSION__;
export const CALIBRATION_SCHEMA_VERSION =
  __MYCO_CALIBRATION_SCHEMA_VERSION__;
export const RUNTIME_SYMBOL = Symbol.for("myco.runtime.protocol.1");
// Read only during hot upgrade so an injected pre-rename runtime can clean itself up.
export const LEGACY_RUNTIME_SYMBOL = Symbol.for("mycodex.runtime.protocol.1");

export type MessageRole = "user" | "assistant";
export type MatchRole = MessageRole | "unknown";
export type CompatibilityState = "compatible" | "degraded" | "safeMode";
export type LanguageCode = "en-US" | "zh-CN" | "zh-TW" | "ja-JP";

export interface PersonConfig {
  name: string;
  avatar: string;
}

export type HostTheme = "light" | "dark" | "unknown";

export interface HostThemeResult {
  theme: HostTheme;
  confidence: number;
  evidence: string[];
}

export interface BubblePalette {
  assistantBubble: string;
  assistantText: string;
  nicknameColor: string;
  avatarBackground: string;
  avatarBorder: string;
}

export interface AppearanceConfig {
  preset: "ReferenceDark" | "Minimal";
  bubbleDisplayMode: "Automatic" | "Whole";
  avatarSize: number;
  assistantAvatarOffsetX: number;
  assistantAvatarOffsetY: number;
  userAvatarOffsetX: number;
  userAvatarOffsetY: number;
  assistantNicknameOffsetX: number;
  assistantNicknameOffsetY: number;
  userNicknameOffsetX: number;
  userNicknameOffsetY: number;
  bubbleRadius: number;
  bubblePaddingX: number;
  bubblePaddingY: number;
  nicknameVisible: boolean;
  messageGap: number;
  assistantBubbleMaxWidth: number;
  darkBubblePalette: BubblePalette;
  lightBubblePalette: BubblePalette;
}

export interface ElementSignature {
  schemaVersion: number;
  sampleCount: number;
  contextFingerprint: string;
  tagName: string;
  role: string | null;
  stableAttributes: Record<string, string>;
  stableClasses: string[];
  ancestorChain: Array<{ tagName: string; role: string | null }>;
  childTagHistogram: Record<string, number>;
  capabilities: {
    hasMarkdown: boolean;
    hasCode: boolean;
    hasButtons: boolean;
  };
  layout: {
    alignment: "left" | "center" | "right" | "unknown";
    widthRatio: number;
  };
  fingerprint: string;
}

export interface CalibrationConfig {
  schemaVersion: number;
  userTurn: ElementSignature | null;
  assistantTurn: ElementSignature | null;
}

export interface RuntimeConfig {
  schemaVersion: number;
  protocolVersion: number;
  language: LanguageCode;
  assistant: PersonConfig;
  user: PersonConfig;
  appearance: AppearanceConfig;
  calibration: CalibrationConfig;
  bridgeBindingName?: string;
}

export interface MatchResult {
  role: MatchRole;
  confidence: number;
  source: "calibration" | "semantic" | "layout" | "unknown";
}

export interface RuntimeDiagnostics {
  version: string;
  protocolVersion: number;
  installed: boolean;
  compatibility: CompatibilityState;
  scannedTurns: number;
  identifiedUserTurns: number;
  decoratedUserTurns: number;
  decoratedAssistantTurns: number;
  assistantBubbleBlocks: number;
  unknownTurns: number;
  averageConfidence: number;
  observerActive: boolean;
  lastRefreshAt: string | null;
  errors: Array<{ code: string; at: string }>;
}

export interface RuntimeHealth {
  active: boolean;
  installed: boolean;
  stylePresent: boolean;
  observerActive: boolean;
  rootConnected: boolean;
  repaired: boolean;
}

export interface RuntimeVersion {
  version: string;
  protocolVersion: number;
}

export interface MyCORuntimeApi {
  install(): RuntimeVersion;
  ensureActive(): RuntimeHealth;
  refresh(): RuntimeDiagnostics;
  applyConfig(config: RuntimeConfig): RuntimeDiagnostics;
  startCalibration(role: MessageRole): void;
  stopCalibration(): void;
  destroy(): void;
  getDiagnostics(): RuntimeDiagnostics;
  getVersion(): RuntimeVersion;
}

export function defaultConfig(): RuntimeConfig {
  // Defaults make the bundle safe to install before the host sends user settings.
  return {
    schemaVersion: CONFIG_SCHEMA_VERSION,
    protocolVersion: PROTOCOL_VERSION,
    language: "en-US",
    assistant: { name: "Codex", avatar: "" },
    user: { name: "You", avatar: "" },
    appearance: {
      preset: "ReferenceDark",
      bubbleDisplayMode: "Automatic",
      avatarSize: 40,
      assistantAvatarOffsetX: 0,
      assistantAvatarOffsetY: 11,
      userAvatarOffsetX: 0,
      userAvatarOffsetY: 11,
      assistantNicknameOffsetX: 0,
      assistantNicknameOffsetY: 0,
      userNicknameOffsetX: 0,
      userNicknameOffsetY: 0,
      bubbleRadius: 14,
      bubblePaddingX: 14,
      bubblePaddingY: 10,
      nicknameVisible: true,
      messageGap: 28,
      assistantBubbleMaxWidth: 66,
      darkBubblePalette: {
        assistantBubble: "#222222",
        assistantText: "#f2f2f2",
        nicknameColor: "#9a9a9a",
        avatarBackground: "#303030",
        avatarBorder: "#FFFFFF14"
      },
      lightBubblePalette: {
        assistantBubble: "#f1f3f5",
        assistantText: "#202124",
        nicknameColor: "#5f6672",
        avatarBackground: "#e5e7eb",
        avatarBorder: "#00000024"
      }
    },
    calibration: {
      schemaVersion: CALIBRATION_SCHEMA_VERSION,
      userTurn: null,
      assistantTurn: null
    }
  };
}
