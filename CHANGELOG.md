# Changelog

All notable project changes are documented here.

## [Unreleased]

## [0.99.2] - 2026-08-06

### Added

- Add schema 7 baseline-plus-delta geometry with independent Assistant/User
  avatar and nickname offsets and an Assistant ordinary-prose width limit.
- Add the packaged `MyCO-logo.png` as the safe first-run Assistant avatar while
  preserving the default nickname `菲叶子` and existing custom avatars.
- Add per-MyCO-process tray minimize reminders with the exact title
  `It's MyCO!!!!!`, a native ToastGeneric route, a packaged 64px information
  mark, and a BalloonTip compatibility fallback.

### Changed

- Unify Home and Appearance preview geometry and give Assistant and User
  bubbles the same visible theme-aware chrome and adaptive content sizing.
- Make the Automatic/Whole runtime grouping and appearance application paths
  invalidate stale state and report zero/partial renderer failures accurately.
- Keep startup associations transactional and MyCO-owned, with precise
  single-instance forwarding and no changes to official Codex shortcuts.

### Fixed

- Prevent long Whole-mode Markdown from becoming a narrow, empty, page-height
  bubble by rejecting stretching shells and selecting the safe content surface.
- Make short Assistant replies shrink to their measured content width while
  retaining maximum-width wrapping for long content.
- Keep Close-to-tray and direct minimize on the same first-minimize notification
  path without consuming the claim when presentation fails.

## [0.99.1] - 2026-08-02

### Added

- Add Japanese (`ja-JP`) Manager strings and project README support.
- Add a localized factory-reset flow that restores the first-launch state.

### Changed

- Unify light/dark preview bubble geometry and startup option spacing.
- Assign a stable identity to every legal Assistant progress and final message
  unit, including pre-output work updates.
- Refresh calibration and onboarding surfaces with the shared minimal desktop
  design language.

### Fixed

- Preserve per-message avatar and nickname ownership across Runtime updates.
- Keep release metadata, Runtime package metadata, SBOM metadata, and localized
  documentation on the same canonical product version.

## [0.99.0] - 2026-07-30

### Release

- Promote the unified MyCO codebase to version `0.99.0`.
- Add a tag-gated workflow that builds, tests, signs, verifies, attests, and
  publishes the Windows x64 package.
- Add per-release ephemeral Authenticode signing as `CN=Crikok`, SHA-256
  checksums, an SPDX 2.2 SBOM, and GitHub build-provenance attestations.
- Add CodeQL and Dependabot coverage for Actions, npm, and NuGet.
- Synchronize the Simplified Chinese and English project documentation,
  screenshots, disclaimer, and release-integrity guidance.

### Changed

- Rename the current product, solution, projects, namespaces, Runtime bundle,
  executable, assets, documentation, build paths, and release archive from
  MyCodex to MyCO.
- Normalize the final user-visible brand, executable metadata, startup display
  name, executable, and release archive to MyCO; add the slogan "It's MyCO!!!!!"
  to the existing About and README brand areas.
- Advance Config schema to 4 for the renamed startup field.

### Compatibility

- Copy the legacy `%APPDATA%\MyCodex` tree to `%APPDATA%\Myco` only when the
  new directory does not exist; preserve the old directory and continue startup
  after a migration failure.
- Migrate the known legacy `MyCodex` and transitional `Myco` per-user Run
  values to `MyCO` without touching other startup entries.
- Keep the legacy single-instance kernel identifiers so old and renamed builds
  cannot run together, and destroy the pre-rename injected Runtime before
  installing MyCO hooks.

Historical entries below retain the product name and paths that were accurate
for those releases.

## [0.3.0-beta.1] - 2026-07-28

### Added

- Adopt the supplied artwork as a square, nine-frame Windows icon embedded in
  the Manager executable and reused by every WPF window and notification icon.
- Add explicit Exit MyCodex / Minimize to tray / Cancel close choices plus
  Start/apply, verified Restart, and Exit commands in the tray menu.
- Add persisted Automatic grouping and Whole response bubble modes with
  Config schema 3 migration.

### Fixed

- Replace single-sample/layout-fallback calibration with three-sample
  structural consensus and current-conversation validation; legacy signatures
  safely expire without resetting identity or appearance preferences.
- Restrict recognition to legal message roots in a positively identified
  conversation, fail closed on headers, sidebars, empty states, dialogs, and
  composers, and reconcile duplicate/orphan identity nodes during streaming.
- Make explicit Restart a single transaction: graceful close, automatic
  exact-identity force fallback, stable process-tree quiescence, bounded
  launch/readiness retry, and refreshed recoverable state after failure.
- Broadcast calibration to all renderer sessions with conversation evidence,
  accept only a role-correct semantic message unit, reject protected native
  surfaces, and stop other sessions after the first valid result.
- Keep one avatar/nickname owner per role and logical turn even when Codex
  renders one reply as multiple content units.
- Prefer the existing Markdown surface for Whole-response bubbles and preserve
  a fully rounded outer contour for every marked bubble surface.
- Restore native taskbar minimize, standard maximize/restore behavior, caption
  hit testing, and explicit-only minimize-to-tray state transitions.
- Replace the redundant close window with one concise, keyboard-accessible
  three-choice prompt.
- Generate the executable/tray multi-frame ICO and the WPF header PNG from the
  repository-owned official `mycodex-source.ico`.
- Deduplicate unchanged renderer discovery and Runtime evidence events in the
  privacy-safe diagnostics log.
- Keep Codex alive after MyCodex exits by creating the exact pipe target
  suspended and duplicating non-inheritable peer pipe handles into that target
  before resume.
- Make restart wait for consecutive empty process snapshots, detect early
  process exit and renderer readiness, apply the Runtime before success, and
  retry only bounded early-exit races.
- Replace destructive prose-marker refreshes with incremental, streaming-stable
  semantic grouping. Headings stay with following prose; lists and quotes stay
  atomic; code, tables, math, tools, status, and controls remain native.

### Validation

- Add exact-force restart, fail-closed restart, text-free role signature,
  protected calibration target, one-identity-per-turn, full-radius bubble,
  standard window-state, simplified close, icon, detach-survival, strict
  isolated acceptance, Config schema 3, quiescence, segmentation, and streaming
  regressions.

## [0.2.0-alpha.5] - 2026-07-28

### Added

- Follow the active Codex renderer's light/dark theme through a bounded hybrid
  detector with confidence/evidence, last-trusted fallback, and separate
  contrast-validated bubble palettes.
- Add independent Manager Dark, Light, and Windows System themes through
  semantic WPF resource dictionaries and a disposable system-theme service.
- Complete the App-owned notification-area lifecycle, minimize-to-tray,
  restore/focus actions, and hidden single-instance activation.
- Add independent per-user login startup and post-Manager Codex launch
  settings. Login startup uses an exact `HKCU Run` value with background mode,
  path-drift correction, and no administrator requirement.
- Add Config schema 2 migration, startup abstraction/policy tests, theme and
  tray state tests, and Runtime theme-change/leak regression tests.

### Security and compatibility

- Keep renderer theme changes CSS-variable-only: no full conversation rescan,
  User bubble override, or new host capability.
- Keep background launch non-interactive and private-pipe-only; an uncontrolled
  running Codex is reported instead of duplicated.
- Preserve schema 0/1 identity, avatar, layout, language, custom dark colors,
  and calibration data during migration.

## [0.2.0-alpha.4] - 2026-07-28

### Fixed

- Track the exact Desktop root PID, executable path, and start time across
  graceful window close so a tray-resident Codex is never mistaken for an
  exited application.
- Force Restart now terminates only the previously verified root tree, waits
  for the full selected application process family to exit, and fails closed
  on PID reuse, identity uncertainty, or multiple matching roots.
- Handle the race where Desktop exits naturally between force-restart
  validation and termination without killing a replacement process.

## [0.2.0-alpha.3] - 2026-07-28

### Added

- Add launcher sliders for symmetric avatar horizontal position and shared
  vertical position, including live preview and English, Simplified Chinese,
  and Traditional Chinese labels.
- Place both assistant and user avatars midway between the nickname and bubble
  by default while keeping the position configurable and locally persisted.

## [0.2.0-alpha.2] - 2026-07-27

### Added

- Add a development-only dual-Codex visual-acceptance controller with an
  isolated temporary profile, private CDP pipe, synthetic `app://` fixture,
  visible run-id, exact-target Restart/Disable/Stop commands, and
  machine-readable run/final state.
- Add fail-closed PID, executable, process-start-time, profile, cleanup-path,
  and lifecycle guards; another process using the same executable is never
  accepted as the owned target.

### Fixed

- Detect current Codex virtualized conversation units through stable,
  text-free DOM anchors and select the strongest conversation root instead of
  the first generic `main` element.
- Restore assistant bubbles plus user/assistant avatars and nicknames while
  preserving the native user bubble, code, diffs, tools, actions, and input.
- Rank renderer targets by visible conversation evidence and stop background
  renderers from producing a false "skin active" state.
- Serialize complete renderer refreshes, deduplicate injection, add transient
  target/health grace periods, and react to Pipe target lifecycle events.
- Quarantine structurally ambiguous calibration files and restrict calibration
  to the best confirmed conversation renderer.
- Distinguish requested, waiting, degraded, and actually applied skin states;
  disable invalid UI actions; and remove renderer exception messages from logs.

### Verification

- Added modern virtualized-DOM, target ranking, calibration quarantine,
  decoration-counter, localization, process-ownership, isolated-profile,
  lifecycle, and security regression coverage.
- Completed Computer Use inspection of wide/narrow, Restart Target,
  destroy/Disable, and Stop behavior while the controlling Codex session
  remained alive.

## [0.2.0-alpha.1] - 2026-07-26

### Added

- Private inherited CDP pipe transport as the default, with no listening socket.
- Explicit localized consent before a loopback-only TCP fallback is attempted.
- Shared Manager/Runtime/protocol/schema version metadata.
- Bounded renderer events and diagnostics, structural calibration validation,
  avatar path/dimension checks, log rotation/redaction, and safe error codes.

### Verification

- Added security-boundary tests and a private-pipe probe with listener checks.
- Verified Pipe evaluation/DOM mutation and the full TCP runtime recovery
  harness against an isolated official Desktop profile.

## [0.1.1-alpha] - 2026-07-26

### Fixed

- Recognize the current Codex renderer's stable structural user and assistant
  turn shapes before consulting older calibration signatures.
- Rebind the mutation observer after SPA conversation-root replacement and
  restore a missing Runtime or stylesheet through periodic CDP health checks.
- Upgrade an older Runtime already resident in the renderer instead of reusing
  stale code with the same protocol symbol.
- Preserve the official user bubble without changing its background, padding,
  width, radius, or position; only assistant Markdown receives a MyCodex bubble.
- Anchor avatars and nicknames outside message content so native tool cards,
  status rows, controls, and message geometry remain unchanged.
- Defer the second WPF close request until the first closing callback has
  unwound, preventing an `InvalidOperationException` on exit.

### Verification

- Added current-renderer structural fixtures, ambiguous-calibration precedence,
  native-user-bubble preservation, root/style self-healing, Runtime hot-upgrade,
  and CDP session rehydration regression tests.
- Extended the isolated official-Desktop gate with a synthetic current-structure
  conversation and destructive style/root recovery check.

## [0.1.0-alpha] - 2026-07-26

### Added

- Windows WPF manager with onboarding, detection, appearance preview,
  calibration, diagnostics, and about pages.
- Immediate and persisted English, Simplified Chinese, and Traditional Chinese
  localization, including first-run setup, dialogs, statuses, and previews.
- Official ChatGPT Desktop and legacy Codex application-adapter architecture.
- Random loopback CDP launch, target discovery, concurrent WebSocket command
  correlation, runtime injection, protocol handshake, cleanup, and reinjection.
- Versioned TypeScript runtime with prose-only bubbles, avatars, nicknames,
  mutation observation, calibration, diagnostics, safe mode, and destroy.
- Schema-versioned atomic configuration, separate calibration storage, corrupt
  file backup, nickname validation, and validated avatar import.
- xUnit and jsdom compatibility/regression test suites.
- Self-contained Windows release script and GitHub Actions workflow.
- English and Simplified Chinese documentation, privacy/security policies, MIT
  license, and original project artwork.

### Known limitations

- Unsigned alpha build.
- System tray, startup registration, and advanced color selection are deferred.
- Desktop DOM changes may require recalibration or a new MyCodex release.

### Fixed

- Refresh the registered Store package entry immediately before launch so a
  Desktop update cannot leave MyCodex pointing at a removed versioned
  `WindowsApps` directory.
- Collapse side-by-side package versions by stable Package Family identity and
  retry discovery once if the executable changes during launch.
- Render imported assistant and user avatars as circular, center-cropped images
  in both the live preview and injected conversation UI.
