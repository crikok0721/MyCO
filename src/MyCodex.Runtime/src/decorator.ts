import { isInteractiveOrTool } from "./dom-utils.js";
import type { MessageRole, RuntimeConfig } from "./types.js";

const PROSE_SELECTOR = [
  "p",
  "blockquote",
  "ul",
  "ol",
  "h1",
  "h2",
  "h3",
  "h4",
  "[class*=markdownContent]",
  "[data-content-type=prose]",
  "[data-testid*=markdown]"
].join(",");

export class Decorator {
  decorate(turn: Element, role: MessageRole, config: RuntimeConfig): boolean {
    if (turn.getAttribute("data-mycodex-role") === role) {
      this.updateIdentity(turn, role, config);
      if (role === "assistant") this.decorateProse(turn, role);
      else this.clearProse(turn);
      return false;
    }

    this.undecorate(turn);
    turn.setAttribute("data-mycodex-turn", "true");
    turn.setAttribute("data-mycodex-role", role);
    turn.classList.add("mc-turn", `mc-${role}`);
    this.updateIdentity(turn, role, config);
    if (role === "assistant") this.decorateProse(turn, role);
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

  decorateProse(turn: Element, role: MessageRole): number {
    this.clearProse(turn);
    const blocks = findProseBlocks(turn);
    for (const block of blocks) {
      block.setAttribute("data-mycodex-prose", role);
      block.classList.add("mc-prose");
    }
    return blocks.length;
  }

  undecorate(turn: Element): void {
    for (const className of ["mc-avatar", "mc-nickname"]) {
      const identity = directChildByClass(turn, className);
      if (identity?.getAttribute("data-mycodex-created") === "true") {
        identity.remove();
      }
    }
    this.clearProse(turn);
    turn.removeAttribute("data-mycodex-turn");
    turn.removeAttribute("data-mycodex-role");
    turn.classList.remove("mc-turn", "mc-user", "mc-assistant");
  }

  reconcile(root: ParentNode, activeTurns: ReadonlySet<Element>): void {
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
      element.classList.remove("mc-prose");
    }
    for (const turn of Array.from(root.querySelectorAll("[data-mycodex-turn]"))) {
      this.undecorate(turn);
    }
  }

  private clearProse(turn: Element): void {
    for (const element of Array.from(turn.querySelectorAll("[data-mycodex-prose]"))) {
      if (element.closest("[data-mycodex-turn]") !== turn) continue;
      element.removeAttribute("data-mycodex-prose");
      element.classList.remove("mc-prose");
    }
  }
}

function findProseBlocks(turn: Element): Element[] {
  const candidates = Array.from(turn.querySelectorAll(PROSE_SELECTOR));
  const safe = candidates.filter((element) => {
    if (element.closest(".mc-nickname,.mc-avatar")) return false;
    if (isInteractiveOrTool(element)) return false;
    if (element.querySelector("pre,code,button,input,textarea,select,[role=button]")) {
      return false;
    }
    return Boolean(element.textContent?.trim());
  });

  const topLevel = safe.filter(
    (candidate) => !safe.some((other) => other !== candidate && other.contains(candidate))
  );
  if (topLevel.length > 0) return topLevel;

  return Array.from(turn.children).filter((element) => {
    if (element.classList.contains("mc-avatar") || element.classList.contains("mc-nickname")) {
      return false;
    }
    if (isInteractiveOrTool(element)) return false;
    if (element.querySelector("pre,code,button,input,textarea,select,[role=button]")) {
      return false;
    }
    const text = element.textContent?.trim() ?? "";
    return text.length > 0 && text.length <= 20_000;
  });
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
