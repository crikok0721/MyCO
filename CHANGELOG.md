# Changelog

All notable project changes are documented here.

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
