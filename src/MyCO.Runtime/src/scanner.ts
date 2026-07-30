import { findBySignature } from "./matcher.js";
import { isInNonConversationRegion } from "./dom-utils.js";
import type { CalibrationConfig } from "./types.js";

// Finds a bounded, non-overlapping set of likely conversation turn elements.
const MODERN_TURN_SELECTOR = [
  "[data-content-search-unit-key]",
  "[data-user-message-bubble]",
  "[data-message-author-role]",
  "[data-role=user]",
  "[data-role=assistant]",
  "[data-author=user]",
  "[data-author=assistant]",
  "[data-testid*=user-message]",
  "[data-testid*=assistant-message]"
].join(",");

const SEMANTIC_TURN_SELECTOR = [
  "[data-message-author-role]",
  "[data-role=user]",
  "[data-role=assistant]",
  "[data-author=user]",
  "[data-author=assistant]",
  "[data-testid*=user-message]",
  "[data-testid*=assistant-message]",
  "div.group.flex.w-full.flex-col.items-end.justify-end",
  "div.group.flex.min-w-0.flex-col"
].join(",");

export function findConversationRoot(document: Document): Element {
  // Codex can render multiple <main> elements and background panes. Select the
  // connected root with the strongest text-free conversation evidence.
  const roots = Array.from(
    document.querySelectorAll(
      "main,[role=main],.thread-scroll-container," +
        "[data-testid*=conversation],[data-content-type=conversation]"
    )
  ).filter((element) => element.isConnected);
  if (document.body) roots.push(document.body);

  const ranked = roots
    .map((element, index) => ({
      element,
      index,
      score: isConversationRootReady(element)
        ? scoreConversationRoot(element)
        : Number.NEGATIVE_INFINITY
    }))
    .sort(
      (left, right) =>
        right.score - left.score ||
        Number(right.element.tagName === "MAIN") -
          Number(left.element.tagName === "MAIN") ||
        left.index - right.index
    );
  const selected = ranked.find((candidate) => Number.isFinite(candidate.score));
  return selected?.element ?? document.documentElement;
}

export function scanTurnCandidates(
  root: ParentNode,
  calibration: CalibrationConfig
): Element[] {
  if (!isElementNode(root) || !isConversationRootReady(root)) {
    return [];
  }
  // Modern stable Codex anchors win; legacy semantic/class adapters remain as
  // compatibility fallbacks for older desktop builds.
  const modernCandidates = modernTurnCandidates(root);
  const nativeCandidates = new Set<Element>(modernCandidates);
  for (const element of Array.from(root.querySelectorAll(SEMANTIC_TURN_SELECTOR))) {
    if (
      isPlausibleTurn(element) &&
      !overlapsNativeCandidate(element, nativeCandidates)
    ) {
      nativeCandidates.add(element);
    }
  }
  for (const article of Array.from(root.querySelectorAll("article"))) {
    if (!overlapsNativeCandidate(article, nativeCandidates)) {
      nativeCandidates.add(article);
    }
  }
  const candidates = new Set<Element>(nativeCandidates);

  if (calibration.userTurn) {
    for (const match of findBySignature(root, calibration.userTurn)) {
      if (!overlapsNativeCandidate(match.element, nativeCandidates)) {
        candidates.add(match.element);
      }
    }
  }
  if (calibration.assistantTurn) {
    for (const match of findBySignature(root, calibration.assistantTurn)) {
      if (!overlapsNativeCandidate(match.element, nativeCandidates)) {
        candidates.add(match.element);
      }
    }
  }

  return Array.from(candidates)
    .filter((element) => !element.closest("[data-myco-inspector]"))
    .filter((element) => isLegalTurnCandidate(root, element))
    .filter((element) => !hasCandidateAncestor(element, candidates))
    // Bound the scan so malformed pages cannot make each refresh unreasonably expensive.
    .slice(0, 800);
}

function isElementNode(node: ParentNode): node is Element {
  return (node as Node).nodeType === 1;
}

export function isConversationRootReady(element: Element): boolean {
  return boundedCount(
    element,
    [
      "[data-content-search-turn-key]",
      "[data-content-search-unit-key]",
      "[data-user-message-bubble]",
      "[data-message-author-role]",
      "[data-role=user]",
      "[data-role=assistant]",
      "[data-author=user]",
      "[data-author=assistant]",
      "[data-testid*=user-message]",
      "[data-testid*=assistant-message]"
    ].join(","),
    2
  ) >= 1;
}

export function isLegalTurnCandidate(
  root: ParentNode,
  element: Element
): boolean {
  if (!element.isConnected || isInNonConversationRegion(element)) return false;
  if (!isWithinRoot(root, element)) return false;
  if (
    element.matches(
      "html,body,main,[role=main],[role=navigation],[role=toolbar]," +
        "[role=status],[role=dialog],[contenteditable=true]"
    )
  ) {
    return false;
  }
  const explicit =
    element.matches(
      "[data-content-search-unit-key],[data-user-message-bubble]," +
        "[data-message-author-role],[data-role=user],[data-role=assistant]," +
        "[data-author=user],[data-author=assistant]," +
        "[data-testid*=user-message],[data-testid*=assistant-message]"
    ) ||
    (element.tagName === "ARTICLE" &&
      Boolean(
        element.getAttribute("data-message-author-role") ||
          element.getAttribute("data-role") ||
          element.getAttribute("data-author")
      ));
  const legacy =
    isConversationRootReady(root as Element) &&
    element.matches(
      "div.group.flex.w-full.flex-col.items-end.justify-end," +
        "div.group.flex.min-w-0.flex-col"
    );
  return (
    (explicit || legacy) &&
    Boolean(
      element.querySelector(
        "[data-user-message-bubble],p,blockquote,ul,ol,h1,h2,h3,h4," +
          "[class*=markdownContent],[data-content-type=prose]," +
          "[data-testid*=markdown]"
      )
    )
  );
}

function modernTurnCandidates(root: ParentNode): Element[] {
  const candidates = new Set<Element>();

  for (const unit of Array.from(
    root.querySelectorAll("[data-content-search-unit-key]")
  )) {
    if (isPlausibleModernUnit(unit)) candidates.add(unit);
  }

  for (const userBubble of Array.from(
    root.querySelectorAll("[data-user-message-bubble]")
  )) {
    const unit = userBubble.closest("[data-content-search-unit-key]");
    candidates.add(unit && isWithinRoot(root, unit) ? unit : userBubble);
  }

  for (const semantic of Array.from(root.querySelectorAll(MODERN_TURN_SELECTOR))) {
    if (
      isPlausibleTurn(semantic) &&
      !overlapsNativeCandidate(semantic, candidates)
    ) {
      candidates.add(semantic);
    }
  }

  // Some ChatGPT-backed pages expose a turn key but not unit keys. In that
  // shape, use the narrow child that owns the user bubble or assistant prose.
  for (const turn of Array.from(
    root.querySelectorAll("[data-content-search-turn-key]")
  )) {
    if (turn.querySelector("[data-content-search-unit-key]")) continue;
    for (const child of Array.from(turn.children)) {
      if (isPlausibleModernUnit(child)) candidates.add(child);
    }
  }

  return Array.from(candidates).filter(
    (element) =>
      !element.matches("[aria-hidden=true][data-virtualized-turn-content]") &&
      !hasCandidateAncestor(element, candidates)
  );
}

function isPlausibleModernUnit(element: Element): boolean {
  if (element.matches("[aria-hidden=true][data-virtualized-turn-content]")) {
    return false;
  }
  if (
    element.matches("[data-user-message-bubble]") ||
    element.querySelector("[data-user-message-bubble]")
  ) {
    return true;
  }
  return Boolean(
    element.querySelector(
      "p,blockquote,ul,ol,h1,h2,h3,h4,[class*=markdownContent]," +
        "[data-content-type=prose],[data-testid*=markdown]"
    )
  );
}

function isPlausibleTurn(element: Element): boolean {
  if (
    element.matches(
      "div.group.flex.w-full.flex-col.items-end.justify-end," +
        "div.group.flex.min-w-0.flex-col"
    )
  ) {
    return Boolean(
      element.querySelector(
        "p,blockquote,ul,ol,[class*=markdownContent]," +
          "[data-content-type=prose],[data-testid*=markdown]"
      )
    );
  }
  return true;
}

function scoreConversationRoot(element: Element): number {
  const turnKeys = boundedCount(element, "[data-content-search-turn-key]", 20);
  const unitKeys = boundedCount(element, "[data-content-search-unit-key]", 40);
  const userBubbles = boundedCount(element, "[data-user-message-bubble]", 20);
  const semanticTurns = boundedCount(element, SEMANTIC_TURN_SELECTOR, 40);
  const prose = boundedCount(
    element,
    "p,[class*=markdownContent],[data-content-type=prose],[data-testid*=markdown]",
    40
  );
  let score =
    turnKeys * 16 +
    unitKeys * 12 +
    userBubbles * 14 +
    semanticTurns * 8 +
    prose * 2;
  if (element.tagName === "MAIN" || element.getAttribute("role") === "main") {
    score += 5;
  }
  if (element.classList.contains("thread-scroll-container")) score += 8;
  return score;
}

function boundedCount(root: Element, selector: string, maximum: number): number {
  return Math.min(root.querySelectorAll(selector).length, maximum);
}

function isWithinRoot(root: ParentNode, element: Element): boolean {
  const node = root as Node;
  return typeof node.contains === "function" ? node.contains(element) : true;
}

function overlapsNativeCandidate(
  candidate: Element,
  nativeCandidates: ReadonlySet<Element>
): boolean {
  for (const native of nativeCandidates) {
    if (native === candidate || native.contains(candidate) || candidate.contains(native)) {
      return true;
    }
  }
  return false;
}

function hasCandidateAncestor(
  element: Element,
  candidates: ReadonlySet<Element>
): boolean {
  let current = element.parentElement;
  while (current) {
    if (candidates.has(current)) return true;
    current = current.parentElement;
  }
  return false;
}
