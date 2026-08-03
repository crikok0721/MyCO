# Project Context

Last updated: 2026-08-03

## Project goal

MyCO is a Windows 10/11 x64 local appearance plugin for the official Codex/ChatGPT
Desktop application. Its WPF Manager manages characters, avatars, nicknames, bubbles,
themes, preview, and lifecycle. A reversible TypeScript Runtime is injected via CDP
to decorate only confirmed conversation surfaces. It does not modify official
installation files, user data, credentials, or network traffic.

Core constraints:
- Private CDP pipe preferred; TCP only with explicit per-attempt consent.
- Fail closed when DOM is uncertain; decorate Assistant prose only.
- Native User bubble, code, Diff, tools, status, buttons, editors, inputs preserved.
- `install()` idempotent; `destroy()` removes all MyCO state.
- Restart only exact verified process tree; never by process name.
- Atomic, versioned, backward-compatible config.
- Logs use explicit allowlists; no chat content.

## Current status

- **Phase:** Phase 3 — Beta testing and pre-release stability remediation
- **Version:** `0.99.2` (local UI repair; not released)
- **Branch:** `main`
- **Base HEAD:** `e52f21a` (existing local commit preserved)
- **GitHub:** https://github.com/crikok0721/MyCO

The active controlling Codex session was never restarted or closed. Production
restart was exercised only against isolated official Desktop process trees.

## Implemented capabilities

- Single-instance WPF Manager, 4-language UI, theme, tray, startup, atomic config.
- Character-first home page, clickable identity entries, sidebar dock, native rounded window.
- Official Desktop discovery, private CDP pipe, explicit-consent loopback TCP fallback.
- Multi-renderer Runtime injection, health repair, compatibility degradation, Safe Mode, reversible destroy.
- Assistant/User avatar/nickname; Assistant prose bubbles (Automatic/Whole modes).
- Windows-following Manager/preview default theme, shared avatar crop dialog,
  and first-run `菲叶子`/packaged-logo defaults.
- Transactional factory reset that preserves the data root and legacy source,
  safely removes Runtime/startup registration, and reopens onboarding.
- Shared Home/Appearance preview bubbles now use one solid user-bubble surface
  while retaining each role's avatar, name, message, and alignment.
- Manager content cards use the shared borderless card surface; reset surfaces
  retain semantic danger colors without decorative outlines.
- Tray menu now has a local rounded WinForms renderer with theme-aware colors,
  inset separators, disabled text, and DPI-scaled spacing.
- Exact-identity restart, process-tree quiescence, force fallback, readiness detection.
- Dev tools: CdpProbe, isolated VisualAcceptance host.
- Self-contained win-x64 ZIP build, per-release self-signed Authenticode signing.
- Optional default-off MyCO-owned Codex launch associations with explicit
  Start-menu/Desktop/protocol coverage and taskbar-pin limitation.
- Official-release update checking with bounded validation and a separate
  project-owned updater; the local self-contained win-x64 publish includes both
  `MyCO.exe` and the single-file `MyCO.Updater.exe`.
- User minimize now hides to the single tray icon, preserves Normal/Maximized
  state, and shows one persisted balloon per Windows boot.
- Runtime Whole-mode shell regression fix and protected mixed-Markdown
  boundary fixtures.

## Public Beta gap

Fix the following blockers before public testing:
1. Disposable real signed-in production restart matrix.
2. Real conversation calibration/bubble visual matrix.
3. Fresh-machine install/config/recovery/removal validation.
4. Verify self-signed release warning on fresh Windows environment.

Full task list: `docs/TASK_LIST.md`.

## Validation baseline (0.99.1)

- `npm ci`: passed, 0 vulnerabilities.
- `npm run check`: passed, 42/42 Runtime tests.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: 131/131; the icon alpha regression test
  now uses the repository's pure .NET PNG decoder and does not depend on a
  machine-specific GDI runtime assembly.
- `scripts/build-release.ps1 -GenerateSbom`: passed end to end and produced the
  local unsigned `artifacts/MyCO-win-x64.zip`, SPDX SBOM, per-file SHA-256
  manifest, and archive hash. Archive SHA-256:
  `2ad6b4a144dfdb40a912c1ab8eedc4bb80d72926328daf47d8a281ad36dffa09`.
- The local validation package is unsigned; the tag-gated GitHub workflow
  created the signed public package, attestation, and Release for `v0.99.1`.
- Published archive SHA-256:
  `90904290e879943cf35a52891ba358c7de4dcdee49e506b961e3d560c4179720`.
- Release URL: https://github.com/crikok0721/MyCO/releases/tag/v0.99.1
- Real Manager/Codex visual acceptance for the August 1 UI changes remains a
  disposable-session/VM gate; automated XAML/DOM tests are supporting evidence only.

## Validation for the local 0.99.2 incremental development

- Runtime `npm ci`/`npm run check`: 0 vulnerabilities, lint, 51/51 tests, and
  generated bundle rebuild passed after the long-content boundary fix.
- Full .NET SDK 8.0.423 build: 0 warnings and 0 errors.
- .NET test suite: 173/173 passed, 0 failed, 0 skipped.
- Isolated self-contained publish completed at
  `artifacts/MyCO-current-0.99.2-win-x64-20260803/`, containing `MyCO.exe` and
  the single-file `MyCO.Updater.exe`. Starting the updater without arguments
  safely exits with code 2.
- `scripts/build-release.ps1` completed in an isolated temporary worktree, so
  it did not overwrite the pre-incremental `artifacts/MyCO-win-x64` package.
  The validation ZIP contained `MyCO.exe`, `MyCO.Updater.exe`,
  `MyCO.runtimeconfig.json`, and `SHA256SUMS.txt`; the embedded Runtime bundle
  was rebuilt and included in `MyCO.exe`. The registry audit endpoint was
  unavailable, so the script was completed with the one-shot local npm audit
  cache, which reported 0 vulnerabilities.
- Public release packaging remains intentionally unperformed.
- Real Windows visual acceptance, taskbar-entry verification, and isolated
  updater replacement tests remain pending.
- No commit, push, tag, PR, or release was created.

## Next recommended steps

1. Move ZIP to a disposable Windows session/VM; execute the restart and visual matrix.
2. Verify fresh config, disable/recovery, removal.
3. Recheck the published `v0.99.1` package in a disposable Windows session/VM;
   signing, provenance, and asset checks already passed in CI.
