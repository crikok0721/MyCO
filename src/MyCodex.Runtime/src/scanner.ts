import { findBySignature } from "./matcher.js";
import type { CalibrationConfig } from "./types.js";

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

export function findConversationRoot(document: Document): ParentNode {
  return (
    document.querySelector(
      "main,[role=main],[data-testid*=conversation],[data-content-type=conversation]"
    ) ??
    document.body ??
    document.documentElement
  );
}

export function scanTurnCandidates(
  root: ParentNode,
  calibration: CalibrationConfig
): Element[] {
  const nativeCandidates = new Set<Element>(
    Array.from(root.querySelectorAll(SEMANTIC_TURN_SELECTOR))
      .filter(isPlausibleTurn)
  );
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
    .filter((element) => !element.closest("[data-mycodex-inspector]"))
    .filter((element) => !hasCandidateAncestor(element, candidates))
    .slice(0, 800);
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
