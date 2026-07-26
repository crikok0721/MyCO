import {
  childTagHistogram,
  layoutOf,
  stableAttributes,
  stableClassTokens,
  structuralFingerprint
} from "./dom-utils.js";
import type { ElementSignature } from "./types.js";

// Creates and scores text-free DOM signatures for calibration-based recovery.
export function createSignature(element: Element): ElementSignature {
  const ancestors: ElementSignature["ancestorChain"] = [];
  let current = element.parentElement;
  while (current && ancestors.length < 5) {
    ancestors.push({
      tagName: current.tagName.toLowerCase(),
      role: current.getAttribute("role")
    });
    current = current.parentElement;
  }

  return {
    schemaVersion: 1,
    tagName: element.tagName.toLowerCase(),
    role: element.getAttribute("role"),
    stableAttributes: stableAttributes(element),
    stableClasses: stableClassTokens(element),
    ancestorChain: ancestors,
    childTagHistogram: childTagHistogram(element),
    capabilities: {
      hasMarkdown: Boolean(element.querySelector("p,ul,ol,blockquote,h1,h2,h3,h4")),
      hasCode: Boolean(element.querySelector("pre,code")),
      hasButtons: Boolean(element.querySelector("button,[role=button]"))
    },
    layout: layoutOf(element),
    fingerprint: structuralFingerprint(element)
  };
}

export function scoreSignature(
  signature: ElementSignature,
  element: Element
): number {
  const candidate = createSignature(element);
  let score = 0;
  let weight = 0;

  // Weighted partial matches tolerate small upstream DOM changes.
  add(signature.tagName === candidate.tagName, 0.13);
  add(signature.role === candidate.role, signature.role ? 0.12 : 0.04);

  const expectedAttributes = Object.entries(signature.stableAttributes);
  if (expectedAttributes.length > 0) {
    const matching = expectedAttributes.filter(
      ([key, value]) => candidate.stableAttributes[key] === value
    ).length;
    addRatio(matching / expectedAttributes.length, 0.25);
  }

  if (signature.stableClasses.length > 0) {
    const candidateClasses = new Set(candidate.stableClasses);
    const matching = signature.stableClasses.filter((token) =>
      candidateClasses.has(token)
    ).length;
    addRatio(matching / signature.stableClasses.length, 0.06);
  }

  const ancestorCount = Math.min(
    signature.ancestorChain.length,
    candidate.ancestorChain.length
  );
  if (ancestorCount > 0) {
    let ancestorScore = 0;
    for (let index = 0; index < ancestorCount; index++) {
      const expected = signature.ancestorChain[index];
      const actual = candidate.ancestorChain[index];
      if (!expected || !actual) continue;
      ancestorScore += expected.tagName === actual.tagName ? 0.7 : 0;
      ancestorScore += expected.role === actual.role ? 0.3 : 0;
    }
    addRatio(ancestorScore / ancestorCount, 0.12);
  }

  const expectedTags = Object.keys(signature.childTagHistogram);
  if (expectedTags.length > 0) {
    const matching = expectedTags.filter(
      (tag) => candidate.childTagHistogram[tag] !== undefined
    ).length;
    addRatio(matching / expectedTags.length, 0.08);
  }

  const capabilityMatches = [
    signature.capabilities.hasMarkdown === candidate.capabilities.hasMarkdown,
    signature.capabilities.hasCode === candidate.capabilities.hasCode,
    signature.capabilities.hasButtons === candidate.capabilities.hasButtons
  ].filter(Boolean).length;
  addRatio(capabilityMatches / 3, 0.1);

  if (signature.layout.alignment !== "unknown") {
    add(signature.layout.alignment === candidate.layout.alignment, 0.08);
  }
  if (signature.layout.widthRatio > 0 && candidate.layout.widthRatio > 0) {
    const delta = Math.abs(signature.layout.widthRatio - candidate.layout.widthRatio);
    addRatio(Math.max(0, 1 - delta * 2), 0.06);
  }

  return weight === 0 ? 0 : Math.round((score / weight) * 1000) / 1000;

  function add(matches: boolean, itemWeight: number): void {
    weight += itemWeight;
    if (matches) score += itemWeight;
  }

  function addRatio(ratio: number, itemWeight: number): void {
    weight += itemWeight;
    score += Math.max(0, Math.min(1, ratio)) * itemWeight;
  }
}

export function findBySignature(
  root: ParentNode,
  signature: ElementSignature,
  minimumConfidence = 0.72
): Array<{ element: Element; confidence: number }> {
  const selector = signature.role
    ? `${signature.tagName}[role="${escapeAttribute(signature.role)}"],${signature.tagName}`
    : signature.tagName;
  // Cap work per refresh so a bad calibration cannot scan an unbounded document.
  const candidates = Array.from(root.querySelectorAll(selector)).slice(0, 600);
  return candidates
    .map((element) => ({ element, confidence: scoreSignature(signature, element) }))
    .filter((result) => result.confidence >= minimumConfidence)
    .sort((left, right) => right.confidence - left.confidence);
}

function escapeAttribute(value: string): string {
  return value.replaceAll("\\", "\\\\").replaceAll('"', '\\"');
}
