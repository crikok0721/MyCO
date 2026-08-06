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
- **Version:** `0.99.2` (published; environment-dependent acceptance gates remain)
- **Branch:** `main`
- **Release source:** tag `v0.99.2` at commit `db3f5d65217f16e7a5a4cbda7ac1810d1705feeb`.
- **Release URL:** https://github.com/crikok0721/MyCO/releases/tag/v0.99.2
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
  state, and shows one in-memory balloon on the first user minimize per MyCO
  process cycle.
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
- Manager preview Assistant/User bubbles now share a theme-aware visible
  outline in the common bubble style; the role-specific surfaces remain
  separate and WPF visual/DPI parity is still an acceptance gate.
- Tray presentation now runs before an in-memory per-MyCO-process claim; a
  native ToastGeneric uses the packaged 64px blue information mark and a
  failed native request falls back to BalloonTip. A failed presentation does
  not consume the remaining claim, and a new MyCO process starts with a fresh
  first-minimize opportunity. The legacy boot marker remains only for config
  compatibility.

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

## 0.99.2 release validation

- `npm.cmd ci`: passed with 0 vulnerabilities; Runtime `npm.cmd run check`:
  lint, 58/58 tests, and generated bundle rebuild passed.
- The official .NET 8.0.423 SDK was installed to a user-local tool directory
  because the machine image did not include an SDK. `dotnet build .\MyCO.sln
  -c Release --no-restore`: 0 warnings, 0 errors. `dotnet test .\MyCO.sln
  -c Release --no-build`: 247/247 passed.
- The tag-gated release workflow rebuilds the self-contained package from the
  `v0.99.2` source tag, signs project-owned binaries, emits the SPDX SBOM and
  SHA-256 sidecar, and publishes the GitHub Release assets.
- GitHub Actions run `31107302898` completed successfully. The published ZIP
  asset SHA-256 is
  `e501e401e5cd9c4fae07ace34e1f1594694a42049241da778042bd58545ca353`, matching
  `MyCO-win-x64.zip.sha256` and the GitHub asset digest.
- Real Windows visual acceptance, native notification rendering, DPI checks, and the
  eight-combination startup matrix remain pending. The executable was not
  launched during this release synchronization.
- The local release build remains a supporting check; the signed GitHub package
  is the official distribution artifact for this version.

## Next recommended steps

1. Use the published package in a disposable Windows session/VM for the restart
   and visual matrix, including the real Codex conversation and tray shell.
2. Verify fresh config, disable/recovery, removal and the startup combinations.
3. Record the real Windows visual and clean-machine results against the release
   requirements; do not infer them from automated tests.
