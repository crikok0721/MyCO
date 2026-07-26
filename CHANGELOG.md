# Changelog

All notable project changes are documented here.

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
