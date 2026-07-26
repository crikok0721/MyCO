import { RuntimeBridge } from "./bridge.js";
import { CalibrationController } from "./calibration.js";
import { classifyTurn } from "./classifier.js";
import { Decorator } from "./decorator.js";
import { Diagnostics } from "./diagnostics.js";
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
  type MyCodexRuntimeApi,
  type RuntimeConfig,
  type RuntimeDiagnostics,
  type RuntimeHealth,
  type RuntimeVersion
} from "./types.js";

// Coordinates style, scanning, decoration, calibration, diagnostics, and self-repair.
export class MyCodexRuntime implements MyCodexRuntimeApi {
  private config: RuntimeConfig = defaultConfig();
  private readonly styles = new StyleManager();
  private readonly decorator = new Decorator();
  private readonly observer = new RuntimeObserver();
  private readonly calibration = new CalibrationController();
  private readonly diagnostics = new Diagnostics();
  private bridge = new RuntimeBridge(undefined);
  private installed = false;
  private root: ParentNode | null = null;
  private compatibility: string | null = null;

  constructor(private readonly document: Document) {}

  install(): RuntimeVersion {
    if (this.installed) {
      this.ensureActive();
      return this.getVersion();
    }
    try {
      this.styles.install(this.document, this.config.appearance);
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
      this.styles.install(this.document, this.config.appearance);
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
        this.styles.install(this.document, this.config.appearance);
      }
      // Every refresh rebuilds the active set, then removes decorations from stale matches.
      const candidates = scanTurnCandidates(this.root, this.config.calibration);
      const activeTurns = new Set<Element>();
      let userTurns = 0;
      let assistantTurns = 0;
      let unknownTurns = 0;
      const confidences: number[] = [];

      for (const turn of candidates) {
        const result = classifyTurn(turn, this.config.calibration);
        if (result.role === "unknown" || result.confidence < 0.72) {
          this.decorator.undecorate(turn);
          unknownTurns++;
          continue;
        }
        confidences.push(result.confidence);
        activeTurns.add(turn);
        this.decorator.decorate(turn, result.role, this.config);
        if (result.role === "user") userTurns++;
        else assistantTurns++;
      }
      this.decorator.reconcile(this.document, activeTurns);

      this.diagnostics.updateScan(
        candidates.length,
        userTurns,
        assistantTurns,
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
    this.styles.install(this.document, this.config.appearance);
    for (const turn of Array.from(
      this.document.querySelectorAll("[data-mycodex-turn=true]")
    )) {
      const role = turn.getAttribute("data-mycodex-role");
      if (role === "user" || role === "assistant") {
        this.decorator.updateIdentity(turn, role, this.config);
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

  destroy(): void {
    this.calibration.stop(this.document);
    this.observer.stop();
    this.decorator.destroy(this.document);
    this.styles.destroy(this.document);
    this.installed = false;
    this.root = null;
    this.compatibility = null;
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
    throw new TypeError("Unsupported MyCodex runtime configuration schema.");
  }
  if (!config.user.name.trim() || !config.assistant.name.trim()) {
    throw new TypeError("Nicknames must not be empty.");
  }
}
