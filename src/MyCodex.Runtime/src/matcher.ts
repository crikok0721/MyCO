import {
  childTagHistogram,
  layoutOf,
  rootContextFingerprint,
  stableAttributes,
  stableClassTokens,
  structuralFingerprint
} from "./dom-utils.js";
import type { ElementSignature } from "./types.js";
import { CALIBRATION_SCHEMA_VERSION } from "./types.js";

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
    schemaVersion: CALIBRATION_SCHEMA_VERSION,
    sampleCount: 1,
    contextFingerprint: "",
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

export function createConsensusSignature(
  elements: Element[],
  conversationRoot: Element
): ElementSignature {
  if (elements.length < 3) {
    throw new TypeError("Calibration requires at least three distinct samples.");
  }
  const signatures = elements.map(createSignature);
  const required = Math.ceil(signatures.length * 0.67);
  const first = signatures[0]!;
  const stableAttributes = consensusRecord(
    signatures.map((signature) => signature.stableAttributes),
    required
  );
  const stableClasses = consensusValues(
    signatures.map((signature) => signature.stableClasses),
    required
  );
  const childTagHistogram = consensusHistogram(
    signatures.map((signature) => signature.childTagHistogram),
    required
  );
  const ancestorChain = first.ancestorChain.filter((ancestor, index) => {
    return (
      signatures.filter((signature) => {
        const candidate = signature.ancestorChain[index];
        return (
          candidate?.tagName === ancestor.tagName &&
          candidate.role === ancestor.role
        );
      }).length >= required
    );
  });
  const signature: ElementSignature = {
    schemaVersion: CALIBRATION_SCHEMA_VERSION,
    sampleCount: signatures.length,
    contextFingerprint: rootContextFingerprint(conversationRoot),
    tagName: majority(signatures.map((item) => item.tagName)),
    role: majority(signatures.map((item) => item.role)),
    stableAttributes,
    stableClasses,
    ancestorChain,
    childTagHistogram,
    capabilities: {
      hasMarkdown:
        signatures.filter((item) => item.capabilities.hasMarkdown).length >= required,
      hasCode:
        signatures.filter((item) => item.capabilities.hasCode).length >= required,
      hasButtons:
        signatures.filter((item) => item.capabilities.hasButtons).length >= required
    },
    // Coordinates and current window geometry are deliberately not persisted.
    layout: { alignment: "unknown", widthRatio: 0 },
    fingerprint: ""
  };
  signature.fingerprint = signatureFingerprint(signature);
  return signature;
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

export function signatureContextMatches(
  signature: ElementSignature,
  root: Element
): boolean {
  return (
    signature.sampleCount >= 3 &&
    Boolean(signature.contextFingerprint) &&
    signature.contextFingerprint === rootContextFingerprint(root)
  );
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

function signatureFingerprint(signature: ElementSignature): string {
  const attributes = Object.entries(signature.stableAttributes)
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([key, value]) => `${key}=${value}`)
    .join("|");
  const children = Object.entries(signature.childTagHistogram)
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([tag, count]) => `${tag}:${count}`)
    .join(",");
  return [
    signature.tagName,
    signature.role ?? "",
    attributes,
    children,
    signature.capabilities.hasMarkdown ? "markdown" : "",
    signature.capabilities.hasCode ? "code" : "",
    signature.capabilities.hasButtons ? "buttons" : ""
  ].join(";");
}

function consensusRecord(
  records: Array<Record<string, string>>,
  required: number
): Record<string, string> {
  const pairs = records.flatMap((record) => Object.entries(record));
  const result: Record<string, string> = {};
  for (const [key, value] of pairs) {
    if (
      records.filter((record) => record[key] === value).length >= required
    ) {
      result[key] = value;
    }
  }
  return result;
}

function consensusValues(values: string[][], required: number): string[] {
  return Array.from(new Set(values.flat()))
    .filter((value) => values.filter((items) => items.includes(value)).length >= required)
    .sort();
}

function consensusHistogram(
  histograms: Array<Record<string, number>>,
  required: number
): Record<string, number> {
  const result: Record<string, number> = {};
  const tags = new Set(histograms.flatMap((histogram) => Object.keys(histogram)));
  for (const tag of tags) {
    const values = histograms
      .map((histogram) => histogram[tag])
      .filter((value): value is number => value !== undefined)
      .sort((left, right) => left - right);
    if (values.length >= required) {
      result[tag] = values[Math.floor(values.length / 2)] ?? 0;
    }
  }
  return result;
}

function majority<T>(values: T[]): T {
  const counts = new Map<T, number>();
  for (const value of values) counts.set(value, (counts.get(value) ?? 0) + 1);
  return Array.from(counts.entries()).sort(
    (left, right) => right[1] - left[1]
  )[0]![0];
}
