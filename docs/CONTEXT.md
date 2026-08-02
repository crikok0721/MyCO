# Project Context

Last updated: 2026-08-01

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
- **Version:** `0.99.1`
- **Branch:** `main`
- **Base HEAD:** `d347c7e0ea4a47ec82bb0125e41f234d9b01d8c3`
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
- Exact-identity restart, process-tree quiescence, force fallback, readiness detection.
- Dev tools: CdpProbe, isolated VisualAcceptance host.
- Self-contained win-x64 ZIP build, per-release self-signed Authenticode signing.

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
  creates the signed public package, attestation, and Release.
- Real Manager/Codex visual acceptance for the August 1 UI changes remains a
  disposable-session/VM gate; automated XAML/DOM tests are supporting evidence only.

## Next recommended steps

1. Move ZIP to a disposable Windows session/VM; execute the restart and visual matrix.
2. Verify fresh config, disable/recovery, removal.
3. Verify the `v0.99.1` tag-driven signing, GitHub provenance, and published assets.
