# Compatibility

Compatibility is the project's primary maintenance boundary. The design assumes
that official Desktop versions and their renderer DOM can change.

## Application adapter

An application adapter converts an independently discovered installation into
launch arguments and identity metadata. The initial catalog supports the new
packaged ChatGPT Desktop family and a legacy Codex Desktop family. Candidate
scoring uses running process information, package/uninstall registration,
architecture, publisher/display identity, executable shape, and version.

Store install paths include the application version and can disappear after an
in-place update. Candidates are therefore grouped by stable Package Family
launch identity, ranked by a currently existing executable and newest version,
and resolved again immediately before launch. A failed path check triggers one
fresh discovery attempt rather than retaining the stale version directory.

Adapters do not contain DOM selectors.

## Injection backend

`IInjectionBackend` separates renderer access from the skin engine. Version
`0.2.0-alpha.1` uses `CdpInjectionBackend` over a transport-neutral connection.
Private pipe is preferred, while loopback TCP requires explicit consent. If a future official application
removes the remote-debugging capability, MyCodex reports injection unavailable;
it does not modify `app.asar` or patch binaries.

Any future backend must preserve the same local-only security boundary and
versioned runtime protocol.

## DOM adapter and matcher

The generic DOM layer uses:

- stable semantic attributes such as roles, author identity, and test/content
  categories;
- ancestor tag/role chains;
- child tag histograms;
- left/right layout category and relative width;
- capabilities such as Markdown, code, buttons, and tool surfaces;
- a structural fingerprint containing no text.

Generated tokens with hash-like, numeric, or build-specific shapes are excluded.
No minified CSS class is a required selector.

The current Codex renderer also exposes stable compositional turn shapes:
right-aligned `group/flex/items-end` containers for user turns and
`group/flex/min-w-0` Markdown containers for assistant turns. These strong
signals are evaluated before saved calibration, so an older ambiguous signature
cannot override a current high-confidence role. Calibration remains the fallback.

## Signature

An `ElementSignature` is data, not application-version code. Schema version 1
contains tag, role, filtered attributes/classes, up to five ancestors,
child-tag shape, capabilities, layout, and fingerprint. Calibration stores
separate user and assistant signatures in `calibration.json`.

The scorer weights stable attributes and element shape more heavily than class
tokens. A competing user/assistant score within `0.08` is ambiguous and remains
unknown.

## Capability detection

Target discovery first verifies that a candidate behaves like a page renderer.
The runtime then probes for a conversation root, turn candidates, role matches,
mutation support, installation success, and cleanup support.

While connected, the manager also asks each Runtime to verify that its stylesheet,
observer, and current conversation root are still active. A missing style or an
SPA root replacement is repaired in place; a missing or unhealthy Runtime is
reloaded, and a failed CDP session is removed and reinjected instead of remaining
reported as active.

Application version is diagnostic context. It is not the compatibility decision.

## Compatibility states

```mermaid
stateDiagram-v2
  [*] --> Probe
  Probe --> InjectionUnavailable: CDP unavailable
  Probe --> ProtocolMismatch: protocol differs
  Probe --> SafeMode: runtime error or no matches
  Probe --> Compatible: confidence >= 0.85
  Probe --> Degraded: 0.68 <= confidence < 0.85
  Probe --> SafeMode: confidence < 0.68
  Degraded --> Compatible: recalibration succeeds
  SafeMode --> Compatible: calibration/update succeeds
```

The DOM decorator itself uses a stricter per-element threshold of `0.72`.
Unknown nodes are never modified.

## Safe mode

Safe mode is a successful protective outcome, not permission to guess. MyCodex
retains settings and prior signatures, reports diagnostics, and permits
calibration, but skips skin mutation on unknown structures.

## Calibration

Pointer events are captured in the renderer. `event.composedPath()` is inspected
instead of treating a nested text/span target as the whole turn. Semantic turn
containers are preferred; a bounded structural heuristic chooses a likely
wrapper otherwise. Hover applies only a temporary namespaced outline.

Clicking produces a structural signature in memory and returns it through the
event-only bridge. Escape cancels. Repeating a step is retry/undo for that role.
No `textContent` is serialized, logged, or fingerprinted.

## Update lifecycle

1. Launch and discover renderer capability.
2. Load the last schema-compatible profile.
3. Run a read-only structural probe.
4. If confidence is high, enable skin automatically.
5. If confidence drops, preserve settings/profile and recommend calibration.
6. If the structure is unknown, remain in safe mode.
7. If the injection boundary changes, publish a backend-compatible MyCodex
   release.

Expected recovery:

- small class/wrapper update: automatic matching;
- attribute/layout/depth changes: user recalibration;
- medium DOM redesign: MyCodex DOM matcher/profile update;
- large renderer or CDP change: MyCodex backend release.

Permanent compatibility with every future Desktop update is not promised.

## Regression fixtures

Runtime tests use project-authored synthetic fixtures only. They vary generated
classes, wrappers, attributes, depth, margins/layout, code/tool/status surfaces,
and dynamic mutations. Real application DOM snapshots must never be committed.
