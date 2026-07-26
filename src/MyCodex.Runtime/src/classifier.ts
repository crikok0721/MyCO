import { layoutOf, stableAttributes } from "./dom-utils.js";
import { scoreSignature } from "./matcher.js";
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
  calibration: CalibrationConfig
): MatchResult {
  const attributes = stableAttributes(element);
  const semanticValues = Object.values(attributes);
  if (semanticValues.some((value) => USER_VALUES.test(value))) {
    return { role: "user", confidence: 0.98, source: "semantic" };
  }
  if (semanticValues.some((value) => ASSISTANT_VALUES.test(value))) {
    return { role: "assistant", confidence: 0.98, source: "semantic" };
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
  const calibrated = classifyFromCalibration(element, calibration);
  if (calibrated) return calibrated;

  const layout = layoutOf(element);
  if (layout.alignment === "right" && layout.widthRatio > 0 && layout.widthRatio < 0.78) {
    return { role: "user", confidence: 0.73, source: "layout" };
  }
  if (
    layout.alignment === "left" &&
    layout.widthRatio > 0 &&
    layout.widthRatio < 0.9 &&
    element.querySelector("p,ul,ol,pre,code")
  ) {
    return { role: "assistant", confidence: 0.73, source: "layout" };
  }

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
    Boolean(
      element.querySelector(
        "p,blockquote,ul,ol,[class*=markdownContent]," +
          "[data-content-type=prose],[data-testid*=markdown]"
      )
    )
  );
}

function hasClasses(element: Element, ...classNames: string[]): boolean {
  return classNames.every((className) => element.classList.contains(className));
}

function classifyFromCalibration(
  element: Element,
  calibration: CalibrationConfig
): MatchResult | null {
  const scores: Array<{ role: MessageRole; confidence: number }> = [];
  if (calibration.userTurn) {
    scores.push({
      role: "user",
      confidence: scoreSignature(calibration.userTurn, element)
    });
  }
  if (calibration.assistantTurn) {
    scores.push({
      role: "assistant",
      confidence: scoreSignature(calibration.assistantTurn, element)
    });
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
