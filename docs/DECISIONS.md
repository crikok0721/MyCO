# Technical Decisions

Last updated: 2026-07-30

This file records current decisions. Historical detail remains in
`docs/development-notes.md` and `docs/CODEX_HANDOFF.md`.

## D-000 — Rename the product without breaking upgrades

Decision: use MyCO for current user-visible names, executable metadata, release
artifacts, repository/solution/project names, namespaces, and repository-owned
brand asset filenames. Keep `%APPDATA%\Myco`, lowercase Runtime protocol/package
identifiers, transitional `Myco` migration inputs, and legacy `MyCodex`
compatibility identifiers stable. Copy the legacy user-data tree only when the
`%APPDATA%\Myco` tree does not exist; migrate the known `MyCodex` and `Myco`
per-user Run values to the visible `MyCO` value; destroy the pre-rename injected
Runtime during hot upgrade; retain the legacy single-instance kernel names.

Reason: repository and engineering names can be case-normalized with explicit
two-step Git renames and synchronized references. Configuration paths,
serialization keys, IPC/protocol names, migration inputs, and single-instance
names must remain stable to preserve upgrades and prevent old and new launchers
from running concurrently. These retained values are compatibility identifiers,
not the displayed product brand.

## D-001 — Preserve official Desktop boundaries

Decision: use CDP and a project-scoped reversible Runtime. Do not patch
`app.asar`, official binaries, profiles, credentials, cookies, or traffic.

Reason: keeps MyCO detectable, reversible, and compatible with the security
model.

## D-002 — Private pipe first

Decision: use inherited private CDP pipes. Permit only an explicit per-attempt
user-approved random `127.0.0.1` TCP fallback.

Reason: avoids a persistent remote-debugging port and narrows the attack
surface.

## D-003 — Fail closed on process identity and DOM uncertainty

Decision: revalidate PID, executable, start time, and process-tree ownership
before a production close; require sufficient semantic DOM confidence before
decoration.

Reason: avoiding an incorrect kill or UI binding is more important than forcing
the feature to appear.

## D-004 — Restart is one staged transaction

Decision: explicit Restart owns graceful close, automatic exact-identity force
fallback, stable quiescence, bounded early-exit/readiness retry, Runtime
reapplication, and recovery to freshly detected state.

Rejected:

- A second force-confirmation dialog: breaks one-click behavior without adding
  safety beyond the identity guard.
- Longer fixed sleeps: hide races and slow every successful restart.
- Process-name-wide termination: can close unrelated or controlling sessions.

## D-005 — Keep Windows and tray states separate

Decision: caption/taskbar minimize uses `SystemCommands.MinimizeWindow`; only an
explicit in-app Minimize-to-tray choice hides the window. `WindowChrome`
preserves native maximize/restore, drag, resize, and hit testing.

Reason: a WPF `WindowState.Minimized` event does not identify whether the user
wants a tray transition.

## D-006 — Calibrate semantic units, then assign one logical owner

Decision: broadcast calibration to positive-evidence renderers, validate the
requested role, reject protected surfaces, store text-free structure evidence,
and decorate one identity owner per role per logical turn.

Rejected:

- Click coordinates, dynamic indices, or generated class names: unstable under
  scrolling, virtualization, streaming, and Desktop updates.
- An identity per renderer content unit: duplicates avatars when one reply is
  split into several units.

## D-007 — Bubble existing content surfaces

Decision: Whole mode prefers the existing Markdown prose surface and styles a
single outer surface without moving or rewriting native nodes. Every marked
surface keeps a complete radius; native User, code, Diff, tool, status, toolbar,
and input surfaces remain untouched.

Reason: parent/child background splitting caused partial and square-topped
bubbles, while DOM restructuring risks breaking native interaction.

## D-008 — One canonical official icon source

Decision: preserve the uploaded official icon byte-for-byte as
`assets/MyCO-source.ico`, then generate the UI PNG and multi-frame packaged
ICO from that source. During generation, apply one deterministic,
anti-aliased rounded-rectangle alpha mask to every derived frame without
redrawing or recoloring the portrait. Use the PNG for WPF content images and
the generated ICO for executable, window, taskbar, and tray surfaces.

Reason: one repository-owned source prevents old artwork or local upload paths
from reappearing, while the derived mask gives every Windows icon surface the
same modern silhouette and leaves the source artwork recoverable.

## D-009 — Log state transitions, not every poll

Decision: deduplicate unchanged target-discovery and Runtime-evidence snapshots.

Reason: preserves diagnosability while preventing bounded privacy-safe logs from
being dominated by identical polling events.

## D-010 — Require conversation evidence and multi-sample calibration

Decision: decorate only legal message roots inside a conversation container
with explicit structural evidence. Each role calibration uses three distinct
samples, stores a text-free consensus plus context fingerprint, and must
validate against other current messages. Legacy single-sample signatures are
invalidated while unrelated preferences are preserved.

Rejected:

- Screen coordinates, left/right layout, or full DOM paths: drift under
  scrolling, resize, streaming, virtualization, and renderer updates.
- Body-wide or generic `main` fallback: can classify the composer, empty state,
  title bar, or sidebar as a message.
- Reusing low-confidence legacy calibration: a missing avatar is safer than an
  identity attached to the wrong native surface.

## D-011 — Use a neutral, mint-accented premium Manager design system

Decision: position the Manager as the control surface of a lightly anime-styled
AI appearance plugin, not an agent workspace or developer dashboard. Build the
WPF visual system as primitive, semantic, and component resources. Use black,
white, and warm neutral surfaces for most of the interface; reserve a complete
low-saturation mint scale for primary actions, selection, progress, focus, and
small state accents. Express character through avatars, names, conversation
preview, and restrained motion rather than decorative clutter.

The Manager home page composes existing application, session, identity, theme,
and command state. It does not introduce a second lifecycle path. Home and
Appearance use one compact shared preview with exactly one Assistant and one
User message. One session-local Codex preview-theme state atomically controls
both roles and remains independent from the Manager theme and persisted Runtime
palette. Windows animation preferences disable non-essential motion. Expensive
full-window transparency and a new UI framework are not used.

Reason: this provides a consistent premium identity and a lower-cognitive-load
workflow while preserving WPF performance, Windows 10/11 compatibility, the
existing Runtime decoration boundary, and all upgrade-sensitive behavior.

## D-012 — Make characters the home-page center and keep native window behavior

Decision: the home page is a role-and-appearance control center. Assistant and
User identities occupy one primary surface and are keyboard-accessible buttons
that open the existing Appearance page. Theme, appearance, and calibration
summaries share that surface. Connection state, target selection, and the
existing lifecycle commands live in one compact sidebar dock; no duplicate
controller path is introduced.

Use solid semantic surfaces and whitespace for hierarchy. Default cards have
no decorative outline, only the primary role surface receives a restrained
shadow, and status chips use a small rounded rectangle rather than a pill.
Avatar circles and intrinsically pill-shaped switch/progress geometry remain.

Request native Windows 11 corners through DWM and retain `WindowChrome` as the
compatibility path. Maximized content is square and restored content regains
the window radius. Do not enable WPF full-window transparency or replace native
resize, drag, maximize, restore, close, tray, or single-instance behavior.

Reason: role-first information architecture matches MyCO's appearance-companion
positioning. Native DWM corners and the existing command surface provide the
requested desktop polish without a new UI dependency, software-rendered
transparent windows, or lifecycle risk.
