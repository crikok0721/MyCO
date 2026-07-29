// Debounces relevant DOM mutations and ignores nodes created by MyCodex itself.
export class RuntimeObserver {
  private observer: MutationObserver | null = null;
  private refreshTimer: number | null = null;
  private observedRoot: Node | null = null;

  start(root: Node, refresh: () => void): void {
    this.stop();
    const window = root.ownerDocument?.defaultView;
    if (!window) return;
    this.observedRoot = root;
    this.observer = new window.MutationObserver((mutations) => {
      const relevant = mutations.some((mutation) => {
        const mutationElement =
          mutation.target instanceof window.Element
            ? mutation.target
            : mutation.target.parentElement;
        if (mutationElement?.closest("[data-mycodex-created=true]")) {
          return false;
        }
        if (mutation.type === "characterData") {
          const parent = mutation.target.parentElement;
          return Boolean(
            parent && !parent.closest("[data-mycodex-created=true]")
          );
        }
        if (mutation.removedNodes.length > 0) return true;
        return Array.from(mutation.addedNodes).some(
          (node) =>
            !(node instanceof window.Element) ||
            !node.matches("[data-mycodex-created=true]")
        );
      });
      // Collapse a burst of streamed-message mutations into one refresh.
      if (!relevant || this.refreshTimer !== null) return;
      this.refreshTimer = window.setTimeout(() => {
        this.refreshTimer = null;
        refresh();
      }, 80);
    });
    this.observer.observe(root, {
      childList: true,
      characterData: true,
      subtree: true
    });
  }

  stop(): void {
    this.observer?.disconnect();
    this.observer = null;
    this.observedRoot = null;
    if (this.refreshTimer !== null) {
      globalThis.clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }
  }

  get active(): boolean {
    return this.observer !== null && Boolean(this.observedRoot?.isConnected);
  }

  observes(root: Node): boolean {
    return this.active && this.observedRoot === root;
  }
}
