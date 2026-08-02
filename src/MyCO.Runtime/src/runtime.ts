import { RuntimeBridge } from "./bridge.js";
import { CalibrationController } from "./calibration.js";
import { classifyTurn } from "./classifier.js";
import { Decorator } from "./decorator.js";
import { Diagnostics } from "./diagnostics.js";
import { HostThemeDetector } from "./host-theme-detector.js";
import { RuntimeObserver } from "./observer.js";
import { findConversationRoot, scanTurnCandidates } from "./scanner.js";
import { StyleManager } from "./style-manager.js";
import {
  CALIBRATION_SCHEMA_VERSION,
  CONFIG_SCHEMA_VERSION,
  PROTOCOL_VERSION,
  RUNTIME_VERSION,
  defaultConfig,
  type MessageRole,
  type HostTheme,
  type MyCORuntimeApi,
  type RuntimeConfig,
  type RuntimeDiagnostics,
  type RuntimeHealth,
  type RuntimeVersion
} from "./types.js";

// Coordinates style, scanning, decoration, calibration, diagnostics, and self-repair.
export class MyCORuntime implements MyCORuntimeApi {
  private config: RuntimeConfig = defaultConfig();
  private readonly styles = new StyleManager();
  private readonly decorator = new Decorator();
  private readonly observer = new RuntimeObserver();
  private readonly calibration = new CalibrationController();
  private readonly diagnostics = new Diagnostics();
  private readonly themeDetector: HostThemeDetector;
  private bridge = new RuntimeBridge(undefined);
  private installed = false;
  private root: ParentNode | null = null;
  private compatibility: string | null = null;
  private hostTheme: Exclude<HostTheme, "unknown"> = "dark";

  constructor(private readonly document: Document) {
    this.themeDetector = new HostThemeDetector(document);
  }

  install(): RuntimeVersion {
    if (this.installed) {
      this.ensureActive();
      return this.getVersion();
    }
    try {
      this.themeDetector.start((result) => {
        if (result.theme === "unknown") return;
        this.hostTheme = result.theme;
        if (this.installed && this.styles.isInstalled(this.document)) {
          this.styles.applyTheme(
            this.document,
            this.config.appearance,
            this.hostTheme
          );
        }
      });
      this.styles.install(
        this.document,
        this.config.appearance,
        this.hostTheme
      );
      this.root = findConversationRoot(this.document);
      this.installed = true;
      this.diagnostics.setInstalled(true);
      this.observer.start(this.root as Node, () => this.refresh());
      this.refresh();
      this.bridge.emit("runtimeReady", this.getVersion());
    } catch (error) {
      this.diagnostics.addError("install", error);
      this.destroy();
      throw error;
    }
    return this.getVersion();
  }

  ensureActive(): RuntimeHealth {
    // Renderer navigation can replace the root or remove injected style; repair both in place.
    let repaired = false;
    if (!this.installed) {
      this.install();
      repaired = true;
      return this.health(repaired);
    }

    const nextRoot = findConversationRoot(this.document);
    if (!this.styles.isInstalled(this.document)) {
      this.styles.install(
        this.document,
        this.config.appearance,
        this.hostTheme
      );
      repaired = true;
    }
    if (this.root !== nextRoot || !this.observer.observes(nextRoot as Node)) {
      this.root = nextRoot;
      this.observer.start(nextRoot as Node, () => this.refresh());
      repaired = true;
    }
    if (repaired) this.refresh();
    return this.health(repaired);
  }

  refresh(): RuntimeDiagnostics {
    if (!this.installed) {
      this.install();
      return this.diagnostics.snapshot();
    }

    try {
      const nextRoot = findConversationRoot(this.document);
      if (this.root !== nextRoot || !this.observer.observes(nextRoot as Node)) {
        this.root = nextRoot;
        this.observer.start(nextRoot as Node, () => this.refresh());
      }
      if (!this.styles.isInstalled(this.document)) {
        this.styles.install(
          this.document,
          this.config.appearance,
          this.hostTheme
        );
      }
      // Every refresh rebuilds the active set, then removes decorations from stale matches.
      const candidates = scanTurnCandidates(this.root, this.config.calibration);
      const activeTurns = new Set<Element>();
      let userTurns = 0;
      let assistantTurns = 0;
      let assistantBubbleBlocks = 0;
      let unknownTurns = 0;
      const confidences: number[] = [];
      const identityRoles = new WeakMap<Element, Set<MessageRole>>();

      for (const turn of candidates) {
        const result = classifyTurn(
          turn,
          this.config.calibration,
          this.root && (this.root as Node).nodeType === 1
            ? (this.root as Element)
            : undefined
        );
        if (result.role === "unknown" || result.confidence < 0.72) {
          this.decorator.undecorate(turn);
          unknownTurns++;
          continue;
        }
        confidences.push(result.confidence);
        activeTurns.add(turn);
        // Codex can render several independent assistant progress/final units
        // inside one logical turn. Identity belongs to the semantic unit, not
        // the enclosing turn; classifier gates still exclude tools and chrome.
        const identityAnchor =
          turn.closest("[data-content-search-unit-key]") ?? turn;
        let roles = identityRoles.get(identityAnchor);
        if (!roles) {
          roles = new Set<MessageRole>();
          identityRoles.set(identityAnchor, roles);
        }
        const identityOwner = !roles.has(result.role);
        roles.add(result.role);
        this.decorator.decorate(
          turn,
          result.role,
          this.config,
          identityOwner
        );
        if (result.role === "user") userTurns++;
        else {
          assistantTurns++;
          assistantBubbleBlocks += turn.querySelectorAll(
            '[data-myco-prose="assistant"]'
          ).length;
        }
      }
      this.decorator.reconcile(this.document, activeTurns);

      this.diagnostics.updateScan(
        candidates.length,
        userTurns,
        assistantTurns,
        assistantBubbleBlocks,
        unknownTurns,
        confidences,
        this.observer.active
      );
      const snapshot = this.diagnostics.snapshot();
      if (snapshot.compatibility !== this.compatibility) {
        this.compatibility = snapshot.compatibility;
        this.bridge.emit("compatibilityChanged", {
          state: snapshot.compatibility,
          averageConfidence: snapshot.averageConfidence
        });
      }
      return snapshot;
    } catch (error) {
      this.diagnostics.addError("refresh", error);
      this.bridge.emit("error", { code: "refresh" });
      return this.diagnostics.snapshot();
    }
  }

  applyConfig(config: RuntimeConfig): RuntimeDiagnostics {
    // Clone host data so later page mutations cannot change the saved configuration object.
    validateConfig(config);
    this.config = structuredClone(config);
    this.bridge.updateBinding(config.bridgeBindingName);
    if (!this.installed) this.install();
    this.styles.install(
      this.document,
      this.config.appearance,
      this.hostTheme
    );
    for (const turn of Array.from(
      this.document.querySelectorAll("[data-myco-turn=true]")
    )) {
      const role = turn.getAttribute("data-myco-role");
      if (role === "user" || role === "assistant") {
        this.decorator.updateIdentity(
          turn,
          role,
          this.config,
          turn.getAttribute("data-myco-identity-owner") !== "false"
        );
      }
    }
    return this.refresh();
  }

  startCalibration(role: MessageRole): void {
    if (role !== "user" && role !== "assistant") {
      throw new TypeError("Calibration role must be user or assistant.");
    }
    this.calibration.start(this.document, role, (selectedRole, signature) => {
      if (selectedRole === "user") this.config.calibration.userTurn = signature;
      else this.config.calibration.assistantTurn = signature;
      this.bridge.emit("calibrationResult", {
        role: selectedRole,
        signature
      });
      this.refresh();
    });
  }

  stopCalibration(): void {
    this.calibration.stop(this.document);
  }

  destroy(): void {
    this.calibration.stop(this.document);
    this.themeDetector.destroy();
    this.observer.stop();
    this.decorator.destroy(this.document);
    this.styles.destroy(this.document);
    this.installed = false;
    this.root = null;
    this.compatibility = null;
    this.hostTheme = "dark";
    this.diagnostics.setInstalled(false);
  }

  getDiagnostics(): RuntimeDiagnostics {
    return this.diagnostics.snapshot();
  }

  getVersion(): RuntimeVersion {
    return { version: RUNTIME_VERSION, protocolVersion: PROTOCOL_VERSION };
  }

  private health(repaired: boolean): RuntimeHealth {
    const rootConnected = Boolean((this.root as Node | null)?.isConnected);
    const stylePresent = this.styles.isInstalled(this.document);
    const observerActive =
      this.root !== null && this.observer.observes(this.root as Node);
    return {
      active: this.installed && stylePresent && observerActive && rootConnected,
      installed: this.installed,
      stylePresent,
      observerActive,
      rootConnected,
      repaired
    };
  }
}

function validateConfig(config: RuntimeConfig): void {
  if (
    config.schemaVersion !== CONFIG_SCHEMA_VERSION ||
    config.protocolVersion !== PROTOCOL_VERSION ||
    config.calibration.schemaVersion !== CALIBRATION_SCHEMA_VERSION
  ) {
    throw new TypeError("Unsupported MyCO runtime configuration schema.");
  }
  if (!config.user.name.trim() || !config.assistant.name.trim()) {
    throw new TypeError("Nicknames must not be empty.");
  }
  if (
    config.appearance.bubbleDisplayMode !== "Automatic" &&
    config.appearance.bubbleDisplayMode !== "Whole"
  ) {
    throw new TypeError("Bubble display mode is not supported.");
  }
  if (
    config.calibration.userTurn &&
    config.calibration.assistantTurn &&
    config.calibration.userTurn.fingerprint ===
      config.calibration.assistantTurn.fingerprint
  ) {
    throw new TypeError("Calibration roles must have distinct signatures.");
  }
  validatePalette(config.appearance.darkBubblePalette);
  validatePalette(config.appearance.lightBubblePalette);
  ensureReadablePalette(
    config.appearance.darkBubblePalette,
    "#111214"
  );
  ensureReadablePalette(
    config.appearance.lightBubblePalette,
    "#ffffff"
  );
}

function validatePalette(palette: RuntimeConfig["appearance"]["darkBubblePalette"]): void {
  for (const color of [
    palette.assistantBubble,
    palette.assistantText,
    palette.nicknameColor,
    palette.avatarBackground,
    palette.avatarBorder
  ]) {
    if (!/^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(color)) {
      throw new TypeError("Bubble palette colors must be hexadecimal values.");
    }
  }
}

function ensureReadablePalette(
  palette: RuntimeConfig["appearance"]["darkBubblePalette"],
  hostBackground: string
): void {
  const host = parseHexColor(hostBackground);
  const background = composite(parseHexColor(palette.assistantBubble), host);
  const foreground = composite(parseHexColor(palette.assistantText), background);
  const light = Math.max(luminance(foreground), luminance(background));
  const dark = Math.min(luminance(foreground), luminance(background));
  if ((light + 0.05) / (dark + 0.05) < 4.5) {
    throw new TypeError(
      "Assistant text contrast must be at least 4.5:1."
    );
  }
}

type Rgba = { red: number; green: number; blue: number; alpha: number };

function parseHexColor(value: string): Rgba {
  return {
    red: Number.parseInt(value.slice(1, 3), 16) / 255,
    green: Number.parseInt(value.slice(3, 5), 16) / 255,
    blue: Number.parseInt(value.slice(5, 7), 16) / 255,
    alpha:
      value.length === 9
        ? Number.parseInt(value.slice(7, 9), 16) / 255
        : 1
  };
}

function composite(foreground: Rgba, background: Rgba): Rgba {
  const alpha =
    foreground.alpha + background.alpha * (1 - foreground.alpha);
  if (alpha <= 0) {
    return { red: 0, green: 0, blue: 0, alpha: 0 };
  }
  return {
    red:
      (foreground.red * foreground.alpha +
        background.red * background.alpha * (1 - foreground.alpha)) /
      alpha,
    green:
      (foreground.green * foreground.alpha +
        background.green * background.alpha * (1 - foreground.alpha)) /
      alpha,
    blue:
      (foreground.blue * foreground.alpha +
        background.blue * background.alpha * (1 - foreground.alpha)) /
      alpha,
    alpha
  };
}

function luminance(color: Rgba): number {
  const linear = (component: number): number =>
    component <= 0.04045
      ? component / 12.92
      : ((component + 0.055) / 1.055) ** 2.4;
  return (
    0.2126 * linear(color.red) +
    0.7152 * linear(color.green) +
    0.0722 * linear(color.blue)
  );
}
