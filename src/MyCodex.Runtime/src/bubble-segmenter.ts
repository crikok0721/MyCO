import { isInteractiveOrTool } from "./dom-utils.js";
import type { AppearanceConfig } from "./types.js";

export type BubbleGroupPosition = "single" | "start" | "middle" | "end";

export interface BubbleSegment {
  element: Element;
  group: number;
  position: BubbleGroupPosition;
}

const SEMANTIC_BLOCK_SELECTOR =
  "h1,h2,h3,h4,h5,h6,p,blockquote,ul,ol,[data-content-type=prose]";
const WHOLE_SURFACE_SELECTOR = [
  "[data-content-type=prose]",
  "[data-testid*=markdown]",
  "[class*=markdownContent]"
].join(",");
const PROTECTED_SELECTOR = [
  "pre",
  "code",
  "table",
  "[role=table]",
  "math",
  ".katex",
  ".katex-display",
  "[data-math]",
  "[data-testid*=tool]",
  "[data-testid*=command]",
  "[data-testid*=terminal]",
  "[data-testid*=diff]",
  "[data-testid*=approval]",
  "[data-content-type=tool]",
  "[data-content-type=command]",
  "[data-content-type=terminal]",
  "[data-content-type=diff]",
  "[data-content-type=approval]"
].join(",");

const SOFT_MINIMUM = 120;
const TARGET_LENGTH = 480;
const SOFT_MAXIMUM = 900;

// Produces a marker-only plan. It never copies, moves, or rewrites host content.
export function segmentAssistantProse(
  turn: Element,
  mode: AppearanceConfig["bubbleDisplayMode"]
): BubbleSegment[] {
  const blocks =
    mode === "Whole"
      ? findWholeResponseSurfaces(turn)
      : findSafeBlocks(turn);
  if (blocks.length === 0) return [];

  const groups: Element[][] =
    mode === "Whole" ? groupWhole(blocks) : groupAutomatic(blocks);
  const segments: BubbleSegment[] = [];
  groups.forEach((group, groupIndex) => {
    group.forEach((element, index) => {
      const position: BubbleGroupPosition =
        group.length === 1
          ? "single"
          : index === 0
            ? "start"
            : index === group.length - 1
              ? "end"
              : "middle";
      segments.push({ element, group: groupIndex, position });
    });
  });
  return segments;
}

function findWholeResponseSurfaces(turn: Element): Element[] {
  const candidates = Array.from(
    turn.querySelectorAll(WHOLE_SURFACE_SELECTOR)
  ).filter((element) => isWholeResponseSurface(element, turn));
  const outermost = candidates.filter(
    (candidate) =>
      !candidates.some(
        (other) => other !== candidate && other.contains(candidate)
      )
  );
  return outermost.length > 0 ? outermost : findSafeBlocks(turn);
}

function isWholeResponseSurface(element: Element, turn: Element): boolean {
  if (!element.textContent?.trim()) return false;
  const owner = element.closest("[data-mycodex-turn]");
  if (owner && owner !== turn) return false;
  if (element.closest(".mc-nickname,.mc-avatar")) return false;
  // Styling an existing Markdown/prose container does not move or rewrite its
  // code, tables, links, or controls. Reject only when the container itself is
  // a native tool/status surface.
  return !isInteractiveOrTool(element);
}

function findSafeBlocks(turn: Element): Element[] {
  const candidates = Array.from(turn.querySelectorAll(SEMANTIC_BLOCK_SELECTOR));
  const safe = candidates.filter(isSafeProseBlock);
  const specific = safe.filter((candidate) => {
    if (candidate.getAttribute("data-content-type") !== "prose") return true;
    return !Array.from(candidate.children).some((child) =>
      child.matches(SEMANTIC_BLOCK_SELECTOR)
    );
  });
  return specific.filter(
    (candidate) =>
      !specific.some(
        (other) =>
          other !== candidate &&
          other.contains(candidate) &&
          other.getAttribute("data-content-type") !== "prose"
      )
  );
}

function isSafeProseBlock(element: Element): boolean {
  if (!element.textContent?.trim()) return false;
  if (element.closest(".mc-nickname,.mc-avatar")) return false;
  if (isInteractiveOrTool(element)) return false;
  if (element.matches(PROTECTED_SELECTOR) || element.querySelector(PROTECTED_SELECTOR)) {
    return false;
  }
  return !element.querySelector(
    "button,input,textarea,select,[contenteditable=true],[role=button]"
  );
}

function groupWhole(blocks: Element[]): Element[][] {
  const groups: Element[][] = [];
  let current: Element[] = [];
  for (const block of blocks) {
    if (current.length > 0 && hasProtectedBarrier(current.at(-1)!, block)) {
      groups.push(current);
      current = [];
    }
    current.push(block);
  }
  if (current.length > 0) groups.push(current);
  return groups;
}

function groupAutomatic(blocks: Element[]): Element[][] {
  const groups: Element[][] = [];
  let current: Element[] = [];
  let currentLength = 0;

  const flush = (): void => {
    if (current.length === 0) return;
    groups.push(current);
    current = [];
    currentLength = 0;
  };

  for (let index = 0; index < blocks.length; index++) {
    const block = blocks[index]!;
    const length = normalizedLength(block);
    const previous = current.at(-1);
    const barrier = previous ? hasProtectedBarrier(previous, block) : false;
    const heading = isHeading(block);
    const previousIsHeading = previous ? isHeading(previous) : false;
    const projected = currentLength + length;

    if (
      barrier ||
      (current.length > 0 &&
        !previousIsHeading &&
        !heading &&
        projected > SOFT_MAXIMUM) ||
      (currentLength >= TARGET_LENGTH && !previousIsHeading)
    ) {
      flush();
    }

    current.push(block);
    currentLength += length;

    // A single structurally safe long block remains intact. Lists and quotes are
    // atomic, and a heading always waits for its following block.
    if (
      !heading &&
      currentLength >= TARGET_LENGTH &&
      (currentLength >= SOFT_MINIMUM || index === blocks.length - 1)
    ) {
      flush();
    }
  }
  flush();
  return groups;
}

function normalizedLength(element: Element): number {
  return (element.textContent ?? "").replace(/\s+/g, " ").trim().length;
}

function isHeading(element: Element): boolean {
  return /^H[1-6]$/.test(element.tagName);
}

function hasProtectedBarrier(left: Element, right: Element): boolean {
  if (left.parentElement !== right.parentElement) return false;
  let sibling = left.nextElementSibling;
  while (sibling && sibling !== right) {
    if (
      sibling.matches(PROTECTED_SELECTOR) ||
      sibling.querySelector(PROTECTED_SELECTOR)
    ) {
      return true;
    }
    sibling = sibling.nextElementSibling;
  }
  return false;
}
