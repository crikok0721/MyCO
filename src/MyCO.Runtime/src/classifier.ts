import { stableAttributes } from "./dom-utils.js";
import { scoreSignature, signatureContextMatches } from "./matcher.js";
import type {
  CalibrationConfig,
  MatchResult,
  MessageRole
} from "./types.js";

// Classifies a DOM element using semantics first, calibration second, and layout last.
const USER_VALUES = /^(user|human|me|self|prompt)$/i;
const ASSISTANT_VALUES = /^(assistant|codex|chatgpt|ai|model)$/i;

export function classifyTurn(
  element: Element,
  calibration: CalibrationConfig,
  conversationRoot?: Element
): MatchResult {
  const attributes = stableAttributes(element);
  const semanticValues = Object.values(attributes);
  if (semanticValues.some((value) => USER_VALUES.test(value))) {
    return { role: "user", confidence: 0.98, source: "semantic" };
  }
  if (semanticValues.some((value) => ASSISTANT_VALUES.test(value))) {
    return { role: "assistant", confidence: 0.98, source: "semantic" };
  }

  // Current Codex builds expose a stable marker on the native user bubble and
  // stable unit keys around both roles. These are more resilient than utility
  // classes and contain no message text.
  if (
    element.matches("[data-user-message-bubble]") ||
    element.querySelector("[data-user-message-bubble]")
  ) {
    return { role: "user", confidence: 0.99, source: "semantic" };
  }
  if (
    element.matches("[data-content-search-unit-key]") &&
    hasAssistantProse(element) &&
    !element.querySelector("[data-user-message-bubble]")
  ) {
    return { role: "assistant", confidence: 0.96, source: "semantic" };
  }

  const ariaLabel = element.getAttribute("aria-label") ?? "";
  if (/\b(user|you|your message)\b/i.test(ariaLabel)) {
    return { role: "user", confidence: 0.88, source: "semantic" };
  }
  if (/\b(assistant|codex|chatgpt|response)\b/i.test(ariaLabel)) {
    return { role: "assistant", confidence: 0.88, source: "semantic" };
  }

  if (isCurrentCodexUserTurn(element)) {
    return { role: "user", confidence: 0.97, source: "semantic" };
  }
  if (isCurrentCodexAssistantTurn(element)) {
    return { role: "assistant", confidence: 0.94, source: "semantic" };
  }

  // Saved calibration is useful across generated-class changes but is weaker than semantics.
  const calibrated = classifyFromCalibration(
    element,
    calibration,
    conversationRoot
  );
  if (calibrated) return calibrated;

  return { role: "unknown", confidence: 0, source: "unknown" };
}

function isCurrentCodexUserTurn(element: Element): boolean {
  return hasClasses(
    element,
    "group",
    "flex",
    "w-full",
    "flex-col",
    "items-end",
    "justify-end"
  );
}

function isCurrentCodexAssistantTurn(element: Element): boolean {
  return (
    hasClasses(element, "group", "flex", "min-w-0", "flex-col") &&
    !element.classList.contains("items-end") &&
    hasAssistantProse(element)
  );
}

function hasAssistantProse(element: Element): boolean {
  return Boolean(
    element.querySelector(
      "p,blockquote,ul,ol,h1,h2,h3,h4,[class*=markdownContent]," +
        "[data-content-type=prose],[data-testid*=markdown]"
    )
  );
}

function hasClasses(element: Element, ...classNames: string[]): boolean {
  return classNames.every((className) => element.classList.contains(className));
}

function classifyFromCalibration(
  element: Element,
  calibration: CalibrationConfig,
  conversationRoot?: Element
): MatchResult | null {
  const scores: Array<{ role: MessageRole; confidence: number }> = [];
  if (calibration.userTurn) {
    if (
      (calibration.userTurn.sampleCount ?? 0) < 3 ||
      (conversationRoot &&
        !signatureContextMatches(calibration.userTurn, conversationRoot))
    ) {
      // Legacy single-sample or structurally stale calibration fails closed.
    } else {
    scores.push({
      role: "user",
      confidence: scoreSignature(calibration.userTurn, element)
    });
    }
  }
  if (calibration.assistantTurn) {
    if (
      (calibration.assistantTurn.sampleCount ?? 0) < 3 ||
      (conversationRoot &&
        !signatureContextMatches(calibration.assistantTurn, conversationRoot))
    ) {
      // Legacy single-sample or structurally stale calibration fails closed.
    } else {
    scores.push({
      role: "assistant",
      confidence: scoreSignature(calibration.assistantTurn, element)
    });
    }
  }
  scores.sort((left, right) => right.confidence - left.confidence);
  const best = scores[0];
  const second = scores[1];
  // Fail closed when confidence is low or both roles look almost equally likely.
  if (!best || best.confidence < 0.72) return null;
  if (second && best.confidence - second.confidence < 0.08) {
    return { role: "unknown", confidence: best.confidence, source: "unknown" };
  }
  return {
    role: best.role,
    confidence: best.confidence,
    source: "calibration"
  };
}
