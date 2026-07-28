import { createSignature } from "./matcher.js";
import type { MessageRole } from "./types.js";

// Lets the user click one visible turn and converts it into a text-free DOM signature.
export class CalibrationController {
  private hovered: Element | null = null;
  private active = false;
  private role: MessageRole = "user";
  private onResult: ((role: MessageRole, signature: ReturnType<typeof createSignature>) => void) | null =
    null;

  start(
    document: Document,
    role: MessageRole,
    onResult: (role: MessageRole, signature: ReturnType<typeof createSignature>) => void
  ): void {
    this.stop(document);
    this.active = true;
    this.role = role;
    this.onResult = onResult;
    document.addEventListener("pointerover", this.handlePointerOver, true);
    document.addEventListener("click", this.handleClick, true);
    document.addEventListener("keydown", this.handleKeyDown, true);
  }

  stop(document: Document): void {
    if (!this.active) return;
    this.hovered?.removeAttribute("data-mycodex-inspector");
    this.hovered = null;
    this.active = false;
    document.removeEventListener("pointerover", this.handlePointerOver, true);
    document.removeEventListener("click", this.handleClick, true);
    document.removeEventListener("keydown", this.handleKeyDown, true);
    this.onResult = null;
  }

  private handlePointerOver = (event: Event): void => {
    const target = resolveCalibrationRoot(event.composedPath());
    if (!target) return;
    this.hovered?.removeAttribute("data-mycodex-inspector");
    this.hovered = target;
    target.setAttribute("data-mycodex-inspector", "hover");
  };

  private handleClick = (event: Event): void => {
    if (!this.active) return;
    const selected = resolveCalibrationRoot(event.composedPath()) ?? this.hovered;
    if (!selected) return;
    // Calibration clicks must not trigger the desktop application's own action.
    event.preventDefault();
    event.stopImmediatePropagation();
    const signature = createSignature(selected);
    const callback = this.onResult;
    const role = this.role;
    this.stop(selected.ownerDocument);
    callback?.(role, signature);
  };

  private handleKeyDown = (event: Event): void => {
    if (!(event instanceof KeyboardEvent) || event.key !== "Escape" || !this.hovered) {
      return;
    }
    event.preventDefault();
    this.stop(this.hovered.ownerDocument);
  };
}

const EXCLUDED_CALIBRATION_TARGETS =
  "button,[role=button],pre,code,textarea,input,select,svg,path";
const SEMANTIC_TURN_TARGETS = [
  "[data-content-search-unit-key]",
  "[data-user-message-bubble]",
  "[data-message-author-role]",
  "[data-role=user]",
  "[data-role=assistant]",
  "[data-author=user]",
  "[data-author=assistant]",
  "[data-testid*=user-message]",
  "[data-testid*=assistant-message]",
  "article"
].join(",");

export function resolveCalibrationRoot(path: EventTarget[]): Element | null {
  const elements = path.filter(
    (item): item is Element => item instanceof Element
  );
  // Prefer an explicit message container; otherwise rank nearby structural ancestors.
  const semantic = elements.find(
    (element) =>
      !element.matches(EXCLUDED_CALIBRATION_TARGETS) &&
      element.matches(SEMANTIC_TURN_TARGETS)
  );
  if (semantic) {
    if (semantic.matches("[data-user-message-bubble]")) {
      return semantic.closest("[data-content-search-unit-key]") ?? semantic;
    }
    return semantic;
  }

  const candidates = elements
    .filter((element) => !element.matches(EXCLUDED_CALIBRATION_TARGETS))
    .filter((element) => !["HTML", "BODY", "MAIN"].includes(element.tagName))
    .map((element, index) => ({
      element,
      score: scoreCandidate(element, index)
    }))
    .sort((left, right) => right.score - left.score);

  return candidates[0]?.element ?? elements[0] ?? null;
}

function scoreCandidate(element: Element, pathIndex: number): number {
  const childCount = element.children.length;
  const hasProse = Boolean(element.querySelector("p,ul,ol,blockquote,h1,h2,h3,h4"));
  const hasInteractive = Boolean(
    element.querySelector("button,[role=button],pre,code")
  );
  const role = element.getAttribute("role");
  let score = -Math.min(pathIndex, 8) * 0.5;
  if (role === "article" || role === "listitem") score += 18;
  if (hasProse) score += 10;
  if (hasInteractive) score += 4;
  if (childCount >= 1 && childCount <= 12) score += 5;
  if (element.tagName === "P" || element.tagName === "SPAN") score -= 14;
  return score;
}
