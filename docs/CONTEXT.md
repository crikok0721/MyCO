# Project Context

Last updated: 2026-08-06

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
- **Base HEAD:** `da71bfb4b0bf51b6c00b8eb2f60ba92ea538e2b9` (existing commit preserved; current repair is uncommitted)
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
- Shared Home/Appearance preview now uses one anchor model with role-specific
  Assistant/User surfaces; WPF visual parity is still an acceptance gate.
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
- Runtime Automatic/Whole protected-barrier and inline-code regression repair;
  structure fingerprints now invalidate stale bubble positions.
- Whole-mode stretching-shell rejection, shrink-to-content Assistant bubbles,
  shared Home/Appearance preview width cap, and the managed packaged default
  avatar contract are covered by the current repair set. The supplied four
  screenshots are recorded as `ERR-001` through `ERR-004` evidence.
- Schema 7 baseline-plus-delta geometry migration with eight independent role
  offsets and Assistant ordinary-prose width; geometry baseline v2 now resolves
  a 35px avatar and -4px User avatar Y at zero while source-compatible
  absolute views are resolved but not persisted.
- All-renderer appearance application keeps zero/partial failure status and now
  rejects a renderer that reports `installed=false`.
- Transactional MyCO-owned startup associations with parent reparse rejection,
  generation-aware rollback, and exact partial-state snapshots.
- Four-language onboarding copy/layout cleanup and invariant tray title
  `It's MyCO!!!!!` with MyCO-owned notification identity.

## Public Beta gap

Fix the following blockers before public testing:
1. Disposable real signed-in production restart matrix.
2. Real conversation calibration/bubble visual matrix.
3. Fresh-machine install/config/recovery/removal validation.
4. Verify self-signed release warning on fresh Windows environment.

Full task list: `docs/TASK_LIST.md`.
Current requirement truth and the historical evidence audit live in
`docs/REQUIREMENTS.md` and `docs/REQUIREMENTS_AUDIT.md`; `docs/HANDOFF.md` is
historical evidence only.

## Historical validation baseline (0.99.1)

- `npm ci`: passed, 0 vulnerabilities.
- `npm run check`: passed, 42/42 Runtime tests.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: 131/131; the icon alpha regression test
  now uses the repository's pure .NET PNG decoder and does not depend on a
  machine-specific GDI runtime assembly.
- `scripts/build-release.ps1 -GenerateSbom`: passed end to end in the historical
  0.99.1 release pass and produced the local unsigned package, SPDX SBOM,
  per-file SHA-256 manifest, and archive hash. The obsolete artifact path is
  intentionally omitted from the current checkout.
  Archive SHA-256:
  `2ad6b4a144dfdb40a912c1ab8eedc4bb80d72926328daf47d8a281ad36dffa09`.
- The local validation package is unsigned; the tag-gated GitHub workflow
  created the signed public package, attestation, and Release for `v0.99.1`.
- Published archive SHA-256:
  `90904290e879943cf35a52891ba358c7de4dcdee49e506b961e3d560c4179720`.
- Release URL: https://github.com/crikok0721/MyCO/releases/tag/v0.99.1
- Real Manager/Codex visual acceptance for the August 1 UI changes remains a
  disposable-session/VM gate; automated XAML/DOM tests are supporting evidence only.

## Current validation for the local 0.99.2 incremental development

- `npm.cmd ci`: passed with 0 vulnerabilities; Runtime `npm.cmd run check`:
  lint, 58/58 tests, and generated bundle rebuild passed.
- The official .NET 8.0.423 SDK was installed to a user-local tool directory
  because the machine image did not include an SDK. `dotnet build .\MyCO.sln
  -c Release --no-restore`: 0 warnings, 0 errors. `dotnet test .\MyCO.sln
  -c Release --no-build`: 242/242 passed.
- `scripts\build-release.ps1` completed against the repository release output.
  The synchronized package is at
  `C:\Users\crikok\Documents\MyCO\artifacts\MyCO-win-x64`,
  contains 501 files, and its ZIP SHA-256 is
  `2a298253931b9556174ea48aa9e1832f1091827d47e0827ccd5b3b1bc5e0cee5`.
  Reflection over the packaged `MyCO.g.resources` confirmed
  `assets/myco-logo.png`; `MyCO.exe` reports `0.99.2.0` and product version
  `0.99.2+da71bfb4b0bf51b6c00b8eb2f60ba92ea538e2b9`.
- Real Windows visual acceptance, notification rendering, DPI checks, and the
  eight-combination startup matrix remain pending. The executable was not
  launched during this release synchronization.
- No commit, push, tag, published release, or PR was created.
- The release synchronization found no running MyCO process at the final build
  step; no Codex process or active user profile was changed.

## Next recommended steps

1. Use the generated package in a disposable Windows session/VM for the restart
   and visual matrix, including the real Codex conversation and tray shell.
2. Verify fresh config, disable/recovery, removal and the startup combinations.
3. Keep publication/signing separate until the visual and clean-machine gates pass.
