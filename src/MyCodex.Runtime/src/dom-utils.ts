const GENERATED_PATTERNS = [
  /^css-[a-z0-9]{5,}$/i,
  /^_[a-z0-9]{6,}$/i,
  /^[a-f0-9]{6,}$/i,
  /^[a-z]{1,3}[0-9][a-z0-9_-]{4,}$/i,
  /^[a-z0-9_-]{12,}$/i
];

const SAFE_ATTRIBUTE_NAMES = new Set([
  "role",
  "aria-label",
  "aria-live",
  "data-message-author-role",
  "data-testid",
  "data-role",
  "data-author",
  "data-content-type"
]);

export function isLikelyGeneratedClass(token: string): boolean {
  const value = token.trim();
  if (!value || value.length > 80) return true;
  return GENERATED_PATTERNS.some((pattern) => pattern.test(value));
}

export function stableClassTokens(element: Element): string[] {
  return Array.from(element.classList)
    .filter((token) => !isLikelyGeneratedClass(token))
    .filter((token) => !token.startsWith("mc-"))
    .sort();
}

export function stableAttributes(element: Element): Record<string, string> {
  const result: Record<string, string> = {};
  for (const attribute of Array.from(element.attributes)) {
    if (attribute.name.startsWith("data-mycodex")) continue;
    const keep =
      SAFE_ATTRIBUTE_NAMES.has(attribute.name) ||
      (attribute.name.startsWith("data-") &&
        /^(user|assistant|message|turn|thread|prose|tool|action|status)$/i.test(
          attribute.value
        ));
    if (!keep || attribute.value.length > 80) continue;
    result[attribute.name] = attribute.value;
  }
  return result;
}

export function childTagHistogram(element: Element): Record<string, number> {
  const histogram: Record<string, number> = {};
  for (const child of Array.from(element.children)) {
    const tag = child.tagName.toLowerCase();
    histogram[tag] = (histogram[tag] ?? 0) + 1;
  }
  return histogram;
}

export function structuralFingerprint(element: Element): string {
  const attributes = Object.entries(stableAttributes(element))
    .map(([key, value]) => `${key}=${value}`)
    .join("|");
  const children = Object.entries(childTagHistogram(element))
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([tag, count]) => `${tag}:${count}`)
    .join(",");
  return [
    element.tagName.toLowerCase(),
    element.getAttribute("role") ?? "",
    attributes,
    children,
    element.querySelector("pre,code") ? "code" : "",
    element.querySelector("button,[role=button]") ? "buttons" : ""
  ].join(";");
}

export function layoutOf(element: Element): {
  alignment: "left" | "center" | "right" | "unknown";
  widthRatio: number;
} {
  const rect = element.getBoundingClientRect();
  const viewportWidth =
    element.ownerDocument.defaultView?.innerWidth ??
    element.ownerDocument.documentElement.clientWidth;
  if (!viewportWidth || rect.width <= 0) {
    return { alignment: "unknown", widthRatio: 0 };
  }

  const center = rect.left + rect.width / 2;
  const normalized = center / viewportWidth;
  const alignment = normalized < 0.42 ? "left" : normalized > 0.58 ? "right" : "center";
  return {
    alignment,
    widthRatio: Math.round((rect.width / viewportWidth) * 1000) / 1000
  };
}

export function isInteractiveOrTool(element: Element): boolean {
  if (element.matches("pre,code,button,input,textarea,select,[contenteditable=true]")) {
    return true;
  }
  if (
    element.closest(
      "pre,code,[data-mycodex-exclude],[role=toolbar],[role=status],[data-testid*=tool],[data-testid*=command],[data-testid*=terminal],[data-testid*=diff],[data-testid*=approval],[data-content-type=tool],[data-content-type=command],[data-content-type=terminal],[data-content-type=diff],[data-content-type=approval],[data-content-type=status]"
    )
  ) {
    return true;
  }
  const role = element.getAttribute("role") ?? "";
  const testId = element.getAttribute("data-testid") ?? "";
  const contentType = element.getAttribute("data-content-type") ?? "";
  return /button|toolbar|status|dialog|menu|log/i.test(role) ||
    /tool|command|terminal|diff|approval|action|status/i.test(testId) ||
    /tool|command|terminal|diff|approval|status/i.test(contentType);
}
