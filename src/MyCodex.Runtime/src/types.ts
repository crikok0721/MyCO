// Shared protocol types. Keep schema and protocol numbers aligned with the C# host.
export const RUNTIME_VERSION = "0.1.1-alpha";
export const PROTOCOL_VERSION = 1;
export const RUNTIME_SYMBOL = Symbol.for("mycodex.runtime.protocol.1");

export type MessageRole = "user" | "assistant";
export type MatchRole = MessageRole | "unknown";
export type CompatibilityState = "compatible" | "degraded" | "safeMode";

export interface PersonConfig {
  name: string;
  avatar: string;
}

export interface AppearanceConfig {
  preset: "ReferenceDark" | "Minimal";
  avatarSize: number;
  bubbleRadius: number;
  bubblePaddingX: number;
  bubblePaddingY: number;
  nicknameVisible: boolean;
  messageGap: number;
  messageMaxWidth: number;
  userBubble: string;
  assistantBubble: string;
  userText: string;
  assistantText: string;
  nicknameColor: string;
}

export interface ElementSignature {
  schemaVersion: 1;
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
  schemaVersion: 1;
  userTurn: ElementSignature | null;
  assistantTurn: ElementSignature | null;
}

export interface RuntimeConfig {
  schemaVersion: 1;
  protocolVersion: 1;
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
  unknownTurns: number;
  averageConfidence: number;
  observerActive: boolean;
  lastRefreshAt: string | null;
  errors: Array<{ code: string; message: string; at: string }>;
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

export interface MyCodexRuntimeApi {
  install(): RuntimeVersion;
  ensureActive(): RuntimeHealth;
  refresh(): RuntimeDiagnostics;
  applyConfig(config: RuntimeConfig): RuntimeDiagnostics;
  startCalibration(role: MessageRole): void;
  destroy(): void;
  getDiagnostics(): RuntimeDiagnostics;
  getVersion(): RuntimeVersion;
}

export function defaultConfig(): RuntimeConfig {
  // Defaults make the bundle safe to install before the host sends user settings.
  return {
    schemaVersion: 1,
    protocolVersion: 1,
    assistant: { name: "Codex", avatar: "" },
    user: { name: "You", avatar: "" },
    appearance: {
      preset: "ReferenceDark",
      avatarSize: 40,
      bubbleRadius: 14,
      bubblePaddingX: 14,
      bubblePaddingY: 10,
      nicknameVisible: true,
      messageGap: 28,
      messageMaxWidth: 66,
      userBubble: "#242424",
      assistantBubble: "#222222",
      userText: "#f5f5f5",
      assistantText: "#f2f2f2",
      nicknameColor: "#9a9a9a"
    },
    calibration: {
      schemaVersion: 1,
      userTurn: null,
      assistantTurn: null
    }
  };
}
