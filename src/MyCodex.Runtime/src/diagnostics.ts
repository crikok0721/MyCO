import {
  PROTOCOL_VERSION,
  RUNTIME_VERSION,
  type CompatibilityState,
  type RuntimeDiagnostics
} from "./types.js";

// Tracks technical health counters only; it never stores message or DOM text.
export class Diagnostics {
  private state: RuntimeDiagnostics = {
    version: RUNTIME_VERSION,
    protocolVersion: PROTOCOL_VERSION,
    installed: false,
    compatibility: "safeMode",
    scannedTurns: 0,
    identifiedUserTurns: 0,
    decoratedUserTurns: 0,
    decoratedAssistantTurns: 0,
    unknownTurns: 0,
    averageConfidence: 0,
    observerActive: false,
    lastRefreshAt: null,
    errors: []
  };

  setInstalled(installed: boolean): void {
    this.state.installed = installed;
    if (!installed) this.state.observerActive = false;
  }

  updateScan(
    scannedTurns: number,
    userTurns: number,
    assistantTurns: number,
    unknownTurns: number,
    confidences: number[],
    observerActive: boolean
  ): void {
    const average =
      confidences.length === 0
        ? 0
        : confidences.reduce((sum, value) => sum + value, 0) / confidences.length;
    this.state.scannedTurns = scannedTurns;
    this.state.identifiedUserTurns = userTurns;
    this.state.decoratedUserTurns = 0;
    this.state.decoratedAssistantTurns = assistantTurns;
    this.state.unknownTurns = unknownTurns;
    this.state.averageConfidence = Math.round(average * 1000) / 1000;
    this.state.observerActive = observerActive;
    this.state.lastRefreshAt = new Date().toISOString();
    this.state.compatibility = compatibilityFrom(average, userTurns, assistantTurns);
  }

  addError(code: string, _error: unknown): void {
    // Page exception messages can contain DOM text, so retain only a technical code.
    this.state.errors = [
      ...this.state.errors.slice(-9),
      { code, at: new Date().toISOString() }
    ];
  }

  snapshot(): RuntimeDiagnostics {
    return structuredClone(this.state);
  }
}

function compatibilityFrom(
  confidence: number,
  userTurns: number,
  assistantTurns: number
): CompatibilityState {
  if (userTurns + assistantTurns === 0) return "safeMode";
  if (confidence >= 0.85) return "compatible";
  if (confidence >= 0.68) return "degraded";
  return "safeMode";
}
