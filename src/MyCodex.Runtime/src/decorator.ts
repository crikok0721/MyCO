import { segmentAssistantProse } from "./bubble-segmenter.js";
import type { MessageRole, RuntimeConfig } from "./types.js";

// Adds MyCodex-owned identity/prose markers while preserving native tool controls.
export class Decorator {
  private segmentState = new WeakMap<
    Element,
    { mode: RuntimeConfig["appearance"]["bubbleDisplayMode"]; elements: Element[] }
  >();

  decorate(turn: Element, role: MessageRole, config: RuntimeConfig): boolean {
    // Repeated scans are expected; update existing decorations without duplicating nodes.
    if (turn.getAttribute("data-mycodex-role") === role) {
      this.updateIdentity(turn, role, config);
      if (role === "assistant") this.decorateProse(turn, role, config);
      else this.clearProse(turn);
      return false;
    }

    this.undecorate(turn);
    turn.setAttribute("data-mycodex-turn", "true");
    turn.setAttribute("data-mycodex-role", role);
    turn.classList.add("mc-turn", `mc-${role}`);
    this.updateIdentity(turn, role, config);
    if (role === "assistant") this.decorateProse(turn, role, config);
    return true;
  }

  updateIdentity(turn: Element, role: MessageRole, config: RuntimeConfig): void {
    const person = role === "assistant" ? config.assistant : config.user;
    let avatar = directChildByClass<HTMLImageElement>(turn, "mc-avatar");
    if (!avatar) {
      avatar = turn.ownerDocument.createElement("img");
      avatar.className = "mc-avatar";
      avatar.dataset.mycodexCreated = "true";
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

    let nickname = directChildByClass<HTMLElement>(turn, "mc-nickname");
    if (!nickname) {
      nickname = turn.ownerDocument.createElement("span");
      nickname.className = "mc-nickname";
      nickname.dataset.mycodexCreated = "true";
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
    const previous = this.segmentState.get(turn);
    if (
      previous?.mode === config.appearance.bubbleDisplayMode &&
      sameElements(previous.elements, segments.map((segment) => segment.element)) &&
      segments.every(
        ({ element }) =>
          element.getAttribute("data-mycodex-prose") === role &&
          element.hasAttribute("data-mycodex-bubble-position")
      )
    ) {
      return segments.length;
    }
    for (const element of Array.from(turn.querySelectorAll("[data-mycodex-prose]"))) {
      if (element.closest("[data-mycodex-turn]") !== turn || active.has(element)) {
        continue;
      }
      clearProseMarker(element);
    }
    for (const segment of segments) {
      const block = segment.element;
      block.setAttribute("data-mycodex-prose", role);
      block.setAttribute("data-mycodex-bubble-group", String(segment.group));
      block.setAttribute("data-mycodex-bubble-position", segment.position);
      block.classList.add("mc-prose");
    }
    this.segmentState.set(turn, {
      mode: config.appearance.bubbleDisplayMode,
      elements: segments.map((segment) => segment.element)
    });
    return segments.length;
  }

  undecorate(turn: Element): void {
    for (const className of ["mc-avatar", "mc-nickname"]) {
      const identity = directChildByClass(turn, className);
      if (identity?.getAttribute("data-mycodex-created") === "true") {
        identity.remove();
      }
    }
    this.clearProse(turn);
    this.segmentState.delete(turn);
    turn.removeAttribute("data-mycodex-turn");
    turn.removeAttribute("data-mycodex-role");
    turn.classList.remove("mc-turn", "mc-user", "mc-assistant");
  }

  reconcile(root: ParentNode, activeTurns: ReadonlySet<Element>): void {
    // Remove stale markers after virtualized or replaced conversation nodes disappear.
    for (const turn of Array.from(root.querySelectorAll("[data-mycodex-turn]"))) {
      if (!activeTurns.has(turn)) this.undecorate(turn);
    }
  }

  destroy(root: ParentNode): void {
    for (const element of Array.from(
      root.querySelectorAll("[data-mycodex-created=true]")
    )) {
      element.remove();
    }
    for (const element of Array.from(root.querySelectorAll("[data-mycodex-prose]"))) {
      element.removeAttribute("data-mycodex-prose");
      clearProseMarker(element);
    }
    for (const turn of Array.from(root.querySelectorAll("[data-mycodex-turn]"))) {
      this.undecorate(turn);
    }
    this.segmentState = new WeakMap();
  }

  private clearProse(turn: Element): void {
    for (const element of Array.from(turn.querySelectorAll("[data-mycodex-prose]"))) {
      if (element.closest("[data-mycodex-turn]") !== turn) continue;
      clearProseMarker(element);
    }
  }
}

function sameElements(left: Element[], right: Element[]): boolean {
  return (
    left.length === right.length &&
    left.every((element, index) => element === right[index])
  );
}

function clearProseMarker(element: Element): void {
  element.removeAttribute("data-mycodex-prose");
  element.removeAttribute("data-mycodex-bubble-group");
  element.removeAttribute("data-mycodex-bubble-position");
  element.classList.remove("mc-prose");
}

function directChildByClass<T extends Element>(
  parent: Element,
  className: string
): T | null {
  return (
    Array.from(parent.children).find((child) => child.classList.contains(className)) as
      | T
      | undefined
  ) ?? null;
}
