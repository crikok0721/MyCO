import {
  createConsensusSignature,
  findBySignature,
  signatureContextMatches
} from "./matcher.js";
import {
  findConversationRoot,
  isConversationRootReady,
  isLegalTurnCandidate
} from "./scanner.js";
import { isInNonConversationRegion } from "./dom-utils.js";
import type { MessageRole } from "./types.js";

const REQUIRED_SAMPLE_COUNT = 3;

// Collects multiple distinct turns and saves only a validated text-free consensus.
export class CalibrationController {
  private hovered: Element | null = null;
  private active = false;
  private role: MessageRole = "user";
  private samples: Element[] = [];
  private onResult: ((role: MessageRole, signature: ReturnType<typeof createConsensusSignature>) => void) | null =
    null;

  start(
    document: Document,
    role: MessageRole,
    onResult: (role: MessageRole, signature: ReturnType<typeof createConsensusSignature>) => void
  ): void {
    this.stop(document);
    this.active = true;
    this.role = role;
    this.samples = [];
    this.onResult = onResult;
    document.addEventListener("pointerover", this.handlePointerOver, true);
    document.addEventListener("click", this.handleClick, true);
    document.addEventListener("keydown", this.handleKeyDown, true);
  }

  stop(document: Document): void {
    if (!this.active) return;
    this.hovered?.removeAttribute("data-mycodex-inspector");
    for (const sample of this.samples) {
      sample.removeAttribute("data-mycodex-inspector");
    }
    this.hovered = null;
    this.samples = [];
    this.active = false;
    document.removeEventListener("pointerover", this.handlePointerOver, true);
    document.removeEventListener("click", this.handleClick, true);
    document.removeEventListener("keydown", this.handleKeyDown, true);
    this.onResult = null;
  }

  private handlePointerOver = (event: Event): void => {
    const target = resolveCalibrationRoot(event.composedPath(), this.role);
    if (this.hovered && !this.samples.includes(this.hovered)) {
      this.hovered.removeAttribute("data-mycodex-inspector");
    }
    if (!target) {
      this.hovered = null;
      return;
    }
    this.hovered = target;
    if (!this.samples.includes(target)) {
      target.setAttribute("data-mycodex-inspector", "hover");
    }
  };

  private handleClick = (event: Event): void => {
    if (!this.active) return;
    // Calibration clicks must not trigger the desktop application's own action.
    event.preventDefault();
    event.stopImmediatePropagation();
    const selected =
      resolveCalibrationRoot(event.composedPath(), this.role) ?? this.hovered;
    if (!selected) return;
    if (this.samples.includes(selected)) return;
    this.samples.push(selected);
    selected.setAttribute("data-mycodex-inspector", "selected");
    if (this.samples.length < REQUIRED_SAMPLE_COUNT) {
      this.hovered = null;
      return;
    }
    const root = findConversationRoot(selected.ownerDocument);
    const signature = createConsensusSignature(this.samples, root);
    if (
      !validateCalibrationSignature(
        root,
        this.role,
        signature,
        new Set(this.samples)
      )
    ) {
      for (const sample of this.samples) {
        sample.removeAttribute("data-mycodex-inspector");
      }
      this.samples = [];
      this.hovered = null;
      return;
    }
    const callback = this.onResult;
    const role = this.role;
    this.stop(selected.ownerDocument);
    callback?.(role, signature);
  };

  private handleKeyDown = (event: Event): void => {
    if (!(event instanceof KeyboardEvent) || event.key !== "Escape") {
      return;
    }
    event.preventDefault();
    this.stop(
      this.hovered?.ownerDocument ??
        (event.currentTarget instanceof Document ? event.currentTarget : document)
    );
  };
}

const EXCLUDED_CALIBRATION_TARGETS =
  "button,[role=button],pre,code,textarea,input,select,svg,path," +
  "[contenteditable=true],[data-mycodex-created=true]";
const PROTECTED_CALIBRATION_SURFACES = [
  EXCLUDED_CALIBRATION_TARGETS,
  "[role=toolbar]",
  "[role=status]",
  "[data-testid*=tool]",
  "[data-testid*=command]",
  "[data-testid*=terminal]",
  "[data-testid*=diff]",
  "[data-testid*=approval]",
  "[data-content-type=tool]",
  "[data-content-type=command]",
  "[data-content-type=terminal]",
  "[data-content-type=diff]",
  "[data-content-type=approval]",
  "[data-content-type=status]"
].join(",");
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
  "div.group.flex.w-full.flex-col.items-end.justify-end",
  "div.group.flex.min-w-0.flex-col",
  "article"
].join(",");

export function resolveCalibrationRoot(
  path: EventTarget[],
  expectedRole?: MessageRole
): Element | null {
  const elements = path.filter(
    (item): item is Element => item instanceof Element
  );
  const origin = elements[0];
  if (
    !origin ||
    origin.closest(PROTECTED_CALIBRATION_SURFACES) ||
    isInNonConversationRegion(origin)
  ) {
    return null;
  }
  const conversationRoot = findConversationRoot(origin.ownerDocument);
  if (!isConversationRootReady(conversationRoot)) return null;
  // Prefer an explicit message container; otherwise rank nearby structural ancestors.
  const semantic = elements.find(
    (element) =>
      !element.matches(EXCLUDED_CALIBRATION_TARGETS) &&
      element.matches(SEMANTIC_TURN_TARGETS) &&
      roleMatches(element, expectedRole)
  );
  if (semantic) {
    if (semantic.matches("[data-user-message-bubble]")) {
      const unit = semantic.closest("[data-content-search-unit-key]") ?? semantic;
      return roleMatches(unit, expectedRole) &&
        isLegalTurnCandidate(conversationRoot, unit)
        ? unit
        : null;
    }
    return isLegalTurnCandidate(conversationRoot, semantic) ? semantic : null;
  }

  const candidates = elements
    .filter((element) => !element.matches(EXCLUDED_CALIBRATION_TARGETS))
    .filter((element) => !["HTML", "BODY", "MAIN"].includes(element.tagName))
    .filter((element) => roleMatches(element, expectedRole))
    .filter((element) => isLegalTurnCandidate(conversationRoot, element))
    .map((element, index) => ({
      element,
      score: scoreCandidate(element, index)
    }))
    .filter((candidate) => candidate.score >= 8)
    .sort((left, right) => right.score - left.score);

  return candidates[0]?.element ?? null;
}

function validateCalibrationSignature(
  root: Element,
  role: MessageRole,
  signature: ReturnType<typeof createConsensusSignature>,
  selectedSamples: ReadonlySet<Element>
): boolean {
  if (!signatureContextMatches(signature, root)) return false;
  const matches = findBySignature(root, signature, 0.76)
    .map((match) => match.element)
    .filter((element) => isLegalTurnCandidate(root, element))
    .filter((element) => roleMatches(element, role));
  const unique = matches.filter(
    (element, index) =>
      !matches.some(
        (other, otherIndex) =>
          otherIndex !== index && other.contains(element)
      )
  );
  return (
    unique.length >= REQUIRED_SAMPLE_COUNT &&
    unique.some((element) => !selectedSamples.has(element))
  );
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

function roleMatches(
  element: Element,
  expectedRole?: MessageRole
): boolean {
  if (!expectedRole) return true;
  const values = [
    element.getAttribute("data-message-author-role"),
    element.getAttribute("data-role"),
    element.getAttribute("data-author")
  ].filter((value): value is string => Boolean(value));
  const explicitUser =
    values.some((value) => /^(user|human|me|self|prompt)$/i.test(value)) ||
    element.matches("[data-user-message-bubble]") ||
    Boolean(element.querySelector("[data-user-message-bubble]")) ||
    element.matches("div.group.flex.w-full.flex-col.items-end.justify-end");
  const explicitAssistant =
    values.some((value) => /^(assistant|codex|chatgpt|ai|model)$/i.test(value)) ||
    element.matches("[data-testid*=assistant-message]") ||
    (element.matches("div.group.flex.min-w-0.flex-col") &&
      !element.classList.contains("items-end")) ||
    (element.matches("[data-content-search-unit-key]") &&
      !explicitUser &&
      Boolean(
        element.querySelector(
          "p,blockquote,ul,ol,h1,h2,h3,h4," +
            "[data-content-type=prose],[data-testid*=markdown]," +
            "[class*=markdownContent]"
        )
      ));
  if (explicitUser) return expectedRole === "user";
  if (explicitAssistant) return expectedRole === "assistant";
  return true;
}
