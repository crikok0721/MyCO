import { segmentAssistantProse } from "./bubble-segmenter.js";
import type { MessageRole, RuntimeConfig } from "./types.js";

// Adds MyCO-owned identity/prose markers while preserving native tool controls.
export class Decorator {
  private segmentState = new WeakMap<
    Element,
    {
      mode: RuntimeConfig["appearance"]["bubbleDisplayMode"];
      elements: Element[];
      structureFingerprint: string;
    }
  >();

  decorate(
    turn: Element,
    role: MessageRole,
    config: RuntimeConfig,
    identityOwner = true
  ): boolean {
    // Repeated scans are expected; update existing decorations without duplicating nodes.
    if (turn.getAttribute("data-myco-role") === role) {
      this.updateIdentity(turn, role, config, identityOwner);
      if (role === "assistant") this.decorateProse(turn, role, config);
      else this.clearProse(turn);
      return false;
    }

    this.undecorate(turn);
    turn.setAttribute("data-myco-turn", "true");
    turn.setAttribute("data-myco-role", role);
    turn.classList.add("mc-turn", `mc-${role}`);
    this.updateIdentity(turn, role, config, identityOwner);
    if (role === "assistant") this.decorateProse(turn, role, config);
    return true;
  }

  updateIdentity(
    turn: Element,
    role: MessageRole,
    config: RuntimeConfig,
    identityOwner = true
  ): void {
    if (!identityOwner) {
      this.clearIdentity(turn);
      turn.setAttribute("data-myco-identity-owner", "false");
      return;
    }
    turn.setAttribute("data-myco-identity-owner", "true");
    const person = role === "assistant" ? config.assistant : config.user;
    const avatars = directChildrenByClass<HTMLImageElement>(turn, "mc-avatar");
    for (const duplicate of avatars.slice(1)) duplicate.remove();
    let avatar = avatars[0] ?? null;
    if (!avatar) {
      avatar = turn.ownerDocument.createElement("img");
      avatar.className = "mc-avatar";
      avatar.dataset.mycoCreated = "true";
      avatar.alt = "";
      avatar.setAttribute("aria-hidden", "true");
      turn.prepend(avatar);
    }
    if (person.avatar) {
      avatar.src = person.avatar;
      avatar.hidden = false;
    } else {
      avatar.removeAttribute("src");
      avatar.hidden = true;
    }

    const nicknames = directChildrenByClass<HTMLElement>(turn, "mc-nickname");
    for (const duplicate of nicknames.slice(1)) duplicate.remove();
    let nickname = nicknames[0] ?? null;
    if (!nickname) {
      nickname = turn.ownerDocument.createElement("span");
      nickname.className = "mc-nickname";
      nickname.dataset.mycoCreated = "true";
      nickname.setAttribute("aria-hidden", "true");
      turn.prepend(nickname);
    }
    nickname.textContent = person.name;
  }

  decorateProse(
    turn: Element,
    role: MessageRole,
    config: RuntimeConfig
  ): number {
    const segments = segmentAssistantProse(
      turn,
      config.appearance.bubbleDisplayMode
    );
    const active = new Set(segments.map((segment) => segment.element));
    const elements = segments.map((segment) => segment.element);
    const structureFingerprint = buildStructureFingerprint(turn, elements);
    const previous = this.segmentState.get(turn);
    if (
      previous?.mode === config.appearance.bubbleDisplayMode &&
      sameElements(previous.elements, elements) &&
      previous.structureFingerprint === structureFingerprint &&
      segments.every(
        ({ element }) =>
          element.getAttribute("data-myco-prose") === role &&
          element.hasAttribute("data-myco-bubble-position")
      )
    ) {
      return segments.length;
    }
    for (const element of Array.from(turn.querySelectorAll("[data-myco-prose]"))) {
      if (element.closest("[data-myco-turn]") !== turn || active.has(element)) {
        continue;
      }
      clearProseMarker(element);
    }
    for (const segment of segments) {
      const block = segment.element;
      block.setAttribute("data-myco-prose", role);
      block.setAttribute("data-myco-bubble-group", String(segment.group));
      block.setAttribute("data-myco-bubble-position", segment.position);
      block.classList.add("mc-prose");
    }
    this.segmentState.set(turn, {
      mode: config.appearance.bubbleDisplayMode,
      elements,
      structureFingerprint
    });
    return segments.length;
  }

  undecorate(turn: Element): void {
    this.clearIdentity(turn);
    this.clearProse(turn);
    this.segmentState.delete(turn);
    turn.removeAttribute("data-myco-turn");
    turn.removeAttribute("data-myco-role");
    turn.removeAttribute("data-myco-identity-owner");
    turn.classList.remove("mc-turn", "mc-user", "mc-assistant");
  }

  reconcile(root: ParentNode, activeTurns: ReadonlySet<Element>): void {
    // Remove stale markers after virtualized or replaced conversation nodes disappear.
    for (const turn of Array.from(root.querySelectorAll("[data-myco-turn]"))) {
      if (!activeTurns.has(turn)) this.undecorate(turn);
    }
    for (const created of Array.from(
      root.querySelectorAll(
        ".mc-avatar[data-myco-created=true]," +
          ".mc-nickname[data-myco-created=true]"
      )
    )) {
      const owner = created.parentElement;
      if (!owner || !activeTurns.has(owner)) created.remove();
    }
  }

  destroy(root: ParentNode): void {
    for (const element of Array.from(
      root.querySelectorAll("[data-myco-created=true]")
    )) {
      element.remove();
    }
    for (const element of Array.from(root.querySelectorAll("[data-myco-prose]"))) {
      element.removeAttribute("data-myco-prose");
      clearProseMarker(element);
    }
    for (const turn of Array.from(root.querySelectorAll("[data-myco-turn]"))) {
      this.undecorate(turn);
    }
    this.segmentState = new WeakMap();
  }

  private clearProse(turn: Element): void {
    for (const element of Array.from(turn.querySelectorAll("[data-myco-prose]"))) {
      if (element.closest("[data-myco-turn]") !== turn) continue;
      clearProseMarker(element);
    }
  }

  private clearIdentity(turn: Element): void {
    for (const className of ["mc-avatar", "mc-nickname"]) {
      for (const identity of directChildrenByClass(turn, className)) {
        if (identity.getAttribute("data-myco-created") === "true") {
          identity.remove();
        }
      }
    }
  }
}

function sameElements(left: Element[], right: Element[]): boolean {
  return (
    left.length === right.length &&
    left.every((element, index) => element === right[index])
  );
}

function buildStructureFingerprint(turn: Element, elements: Element[]): string {
  // Text length is deliberately excluded: streaming text growth must not
  // reshuffle an existing group until the DOM structure or mode changes.
  const protectedCount = turn.querySelectorAll(
    "pre,table,[role=table],math,.katex,.katex-display,[data-math]," +
      "[data-testid*=tool],[data-testid*=command],[data-testid*=terminal]," +
      "[data-testid*=diff],[data-testid*=approval],[data-content-type=tool]," +
      "[data-content-type=command],[data-content-type=terminal]," +
      "[data-content-type=diff],[data-content-type=approval],[data-content-type=status]"
  ).length;
  const elementShape = elements.map((element) => {
    const ancestors: string[] = [];
    let current = element.parentElement;
    while (current && current !== turn) {
      ancestors.push(
        `${current.tagName}:${current.getAttribute("data-content-type") ?? ""}:${current.childElementCount}`
      );
      current = current.parentElement;
    }
    return `${element.tagName}:${element.childElementCount}:${ancestors.join("/")}`;
  });
  return `${protectedCount}|${elementShape.join(";")}`;
}

function clearProseMarker(element: Element): void {
  element.removeAttribute("data-myco-prose");
  element.removeAttribute("data-myco-bubble-group");
  element.removeAttribute("data-myco-bubble-position");
  element.classList.remove("mc-prose");
}

function directChildrenByClass<T extends Element>(
  parent: Element,
  className: string
): T[] {
  return Array.from(parent.children).filter((child) =>
    child.classList.contains(className)
  ) as T[];
}
