# Technical Decisions

Last updated: 2026-08-06

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

## D-005 — User minimize always hides to the tray

Decision: every user-triggered minimize path transitions through the one
tray-state machine, sets `ShowInTaskbar=false` before hiding, and preserves the
last Normal/Maximized state. Tray double-click, the tray menu, and the
single-instance activation event restore that state. `TrayService` remains the
sole `NotifyIcon` owner. A localized balloon is shown only for the first
user-triggered minimize in the current Windows boot session; the boot identity
is persisted so restarting MyCO cannot repeat it, while a later Windows reboot
allows one new notification. Background startup and duplicate state events do
not notify.

Reason: the requested product behavior treats minimize as a notification-area
presence rather than a taskbar presence. A persisted uptime-derived boot
identity gives the rule process independence without adding another system
service or touching Windows user data outside MyCO's config.

## D-006 — Calibrate semantic units, then assign one owner per legal unit

Decision: broadcast calibration to positive-evidence renderers, validate the
requested role, reject protected surfaces, store text-free structure evidence,
and decorate exactly one identity owner per distinct legal message unit. When
Codex places multiple independent Assistant progress or final units under one
logical turn, each unit receives its own avatar and nickname. Markdown
paragraphs, list items, and segmented prose inside a unit share that unit's
single identity.

Rejected:

- Click coordinates, dynamic indices, or generated class names: unstable under
  scrolling, virtualization, streaming, and Desktop updates.
- One identity per logical turn: hides identity from later independent progress
  and final units rendered under that turn.
- An identity per Markdown child or bubble segment: duplicates avatars inside
  one semantic message.

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
Avatar circles and progress geometry remain intrinsically circular/pill-shaped;
the two Settings startup switches use the shared rounded-rectangle track and
rounded-rectangle thumb requested by the current UI update.

Request native Windows 11 corners through DWM and retain `WindowChrome` as the
compatibility path. Maximized content is square and restored content regains
the window radius. Do not enable WPF full-window transparency or replace native
resize, drag, maximize, restore, close, tray, or single-instance behavior.

Reason: role-first information architecture matches MyCO's appearance-companion
positioning. Native DWM corners and the existing command surface provide the
requested desktop polish without a new UI dependency, software-rendered
transparent windows, or lifecycle risk.

## D-013 — Use autonomous per-release self-signed Authenticode

Decision: version `0.99.1` uses a tag-gated GitHub Actions release workflow.
Each isolated Windows runner creates an ephemeral RSA 3072-bit code-signing
certificate with subject `CN=Crikok`, signs project-owned PE files directly
from the ephemeral PFX with SignTool and SHA-256, verifies the embedded signer
identity without adding local trust, packages the public certificate, and
destroys the private key. Releases also publish archive/file SHA-256 values, an
SPDX SBOM, and a GitHub build-provenance attestation.

The certificate is explicitly documented as self-signed and not publicly
trusted by Windows. It is not described as a SmartScreen bypass or a stable
publisher identity. Users should verify the public certificate shipped with
the same release, the checksum, source tag, and GitHub attestation.
The default release does not contact a public timestamp authority, so the
signature has no validity extension after certificate expiry.

Reason: the release must be executable without certificate purchase, identity
validation, human approval, or long-lived private-key custody. An ephemeral
self-signed key provides Authenticode integrity inside one release but cannot
provide public CA trust or cross-release reputation; provenance and checksums
cover the reproducible source-to-artifact relationship.

## D-014 — Align the default Manager and preview themes without merging controls

Decision: initialize the session-local Codex preview from the Manager's
effective theme after `ThemeService` resolves Windows `System` mode. While the
preview is in its automatic state, Windows theme changes update both surfaces.
An explicit preview selection opts out for the current session only; the
preview remains non-persistent and does not alter Runtime palettes.

Reason: the default experience must match the user's Windows theme while the
existing independent preview control remains useful for deliberate comparison.

## D-015 — Crop avatars before importing them into managed storage

Decision: both avatar commands use one WPF crop dialog. The dialog returns a
bounded square PNG only after confirmation; the bytes are validated and stored
through `AvatarService`'s managed content-hash path. Cancel performs no config
write and no avatar assignment. First-run Logo seeding uses the same managed
import path and never persists a repository or pack URI.

Reason: one validation/storage boundary prevents external paths and unbounded
image data from entering configuration, while the crop math remains testable
without depending on WPF window state.

## D-016 — Associate only MyCO-owned Codex launch entries

Decision: the optional, default-off association setting creates only a MyCO
Start-menu shortcut, a MyCO desktop shortcut, and the per-user `myco-codex:`
protocol. Each entry launches MyCO with `--codex-launch`, which routes through
the existing mutex, activation event, private CDP pipe, and exact running-state
policy. Existing shortcuts, official Codex files, pinned taskbar entries,
Codex protocols, AUMIDs, profiles, credentials, and configuration are never
rewritten. If a requested path is occupied by another owner, the operation
fails closed and leaves it unchanged. Every existing path component below the
trusted special-folder root must be non-reparse. Mutations and rollback use
sealed generation comparisons, and settings/reset restore an opaque snapshot;
therefore concurrent foreign replacements and partial path-drift state are left
safe or restored exactly.

Reason: Windows does not provide a safe standard-user API to redirect an
already pinned official taskbar item. A reversible MyCO-owned entry gives
useful coverage without claiming control over an external owner. Since no
foreign shortcut is overwritten, the precise backup set for user-owned entries
is intentionally empty; rollback removes only entries created by MyCO.

## D-017 — Verify releases before external replacement

Decision: update checks use only the official GitHub Releases API for
`crikok0721/MyCO`, select the newest non-draft, non-prerelease semantic release,
and accept only `MyCO-win-x64.zip` plus `MyCO-win-x64.zip.sha256` over validated
HTTPS asset URLs. Downloads are bounded and staged in a unique child of
`%TEMP%\MyCO\Updates`; the updater accepts no cleanup or staging path outside
that private update area.
The archive, hash, paths, reparse points, expanded file list, and required
`MyCO.exe` are validated before an external `MyCO.Updater.exe` waits for the
exact current PID/path/start-time identity, renames the old install to a
temporary backup, swaps the staged directory, verifies the new executable,
rolls back on failure, and starts only the new MyCO. AppData and Codex data are
outside the replacement directory.

Reason: a running executable cannot safely overwrite itself. A project-owned
minimal updater separates replacement from the process being replaced and
keeps the failure boundary explicit without silent elevation.

## D-018 — Select the innermost safe Markdown surface

Decision: Whole-mode segmentation prefers the most specific safe existing
Markdown surface when the renderer exposes both a prose/layout shell and a
Markdown child. A candidate containing code, tables, tools, status, or controls
is rejected and segmentation falls back to legal prose blocks. The bubble CSS
uses `fit-content(100%)`, a zero flex minimum, the available-space maximum, and
natural height; it never wraps or rewrites host DOM.

Reason: the reproducible long-bar fixture showed the outer `data-content-type`
prose shell being selected around a Markdown child. Host flex sizing then made
the shell a narrow, page-height surface. Choosing the child fixes ownership at
the boundary; the bounded width rule prevents the same layout combination from
collapsing a valid surface.

## D-019 — Treat appearance apply as a verified all-renderer transaction

Decision: serialize consecutive config transactions, fan each transaction out
to every current renderer, require valid Runtime diagnostics, and report zero
sessions or partial failure explicitly. Register the replacement navigation
script before best-effort removal of the prior registration.

Reason: saving configuration is not evidence that the visible renderer applied
it. Explicit counts and diagnostics prevent false success, while ordering makes
the latest save deterministic.

## D-020 — Migrate shared placement to schema 6 role-specific controls

Decision: store independent X/Y offsets for Assistant avatar, Assistant name,
User avatar and User name. Rename the legacy width to
`assistantBubbleMaxWidth`; migrate valid old offsets to both roles, start name
offsets at zero, and clamp width to the safe new range.

Reason: role identity elements must move independently without resetting older
appearance, calibration or startup preferences. The width remains limited to
Assistant ordinary prose so protected native surfaces keep their own layout.

## D-021 — Keep NotifyIcon BalloonTip as the notification compatibility route

Decision: keep the existing one-per-boot BalloonTip, use the invariant title
`It's MyCO!!!!!`, localize the body, and maintain a project-owned Start-menu
identity shortcut with the packaged icon and AUMID. Do not add Windows App SDK
or a self-drawn notification window.

Reason: BalloonTip is already available on the supported desktop stack and is
the smallest dependency-free route. Toast APIs can provide richer templates but
add identity, packaging or runtime-deployment obligations that are unnecessary
for this incremental change.

## D-022 — Persist appearance geometry as baseline-relative deltas

Decision: schema 7 stores one `AppearanceGeometryDeltas` object. A versioned
safe baseline is resolved in Core and the resulting effective geometry is sent
to the Runtime and used by the WPF preview. Existing absolute schema-6 fields
are migration inputs only; compatibility properties remain ignored by storage
and are populated after normalization. Every slider has a symmetric range with
zero at its midpoint, and reset writes zero deltas. Baseline version 2 uses a
35px avatar and a -4px User avatar vertical offset (Assistant remains 11px).
Schema-7 baseline-1 deltas are resolved against the old 40px/11px values and
then converted to the new delta representation.

Reason: absolute values made the UI imply that a machine-specific 34px or 11px
value was the neutral point and caused preview/runtime drift. A single resolver
keeps persistence, preview and CSS variables semantically aligned without
reading chat content or adding a renderer protocol dependency.

## D-023 — Hard barriers plus structural fingerprints for bubble refresh

Decision: inline code remains native but does not reject its safe paragraph.
Block code, tables, math, tools, command/terminal, Diff, approval and status
surfaces are hard barriers. Whole mode selects the innermost safe Markdown
surface and falls back to safe semantic blocks. Decorator cache fingerprints
structure and protected-node count while excluding text length, so streaming
text does not regroup but inserted barriers/mode changes do.

Reason: the previous broad protected selector dropped paragraphs containing
inline code, and a same-element/mode cache kept stale positions after a new
protected child appeared. The repair is marker-only and leaves DOM order and
native surfaces untouched.
