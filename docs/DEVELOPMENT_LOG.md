# Development Log

## 2026-07-29 — Message recognition and calibration stabilization

Date:

2026-07-29

Change:

- Restricted matching to legal message roots inside a positively identified
  conversation container.
- Replaced single-sample/layout fallback with three-sample structural consensus,
  context validation, and fail-closed scoring.
- Made avatar/nickname reconciliation singleton-safe and removed illegal,
  duplicate, detached, and role-inconsistent decorations.
- Kept one conversation-root observer and refreshed affected turns during
  streaming mutations.
- Invalidated legacy single-sample calibration while preserving names, avatars,
  appearance, language, and all unrelated settings.

Reason:

Generic root fallback and weak single-sample rules allowed empty-state and
composer nodes to become logical turns; repeated reconciliation then made the
same boundary error appear as duplicate and drifting identities.

Impact:

Low-confidence or structurally incompatible pages remain undecorated and
request recalibration instead of attaching identity UI to native non-message
surfaces.

Modified Files:

- `src/MyCodex.Runtime/src/{calibration,classifier,decorator,dom-utils,matcher,observer,runtime,scanner,types}.ts`
- `src/MyCodex.Runtime/tests/{calibration,compatibility-regression,runtime}.test.ts`
- `src/MyCodex.Core/Compatibility/ElementSignature*.cs`
- `src/MyCodex.Core/Configuration/ConfigStore.cs`
- `src/MyCodex.Manager/Resources/Strings.*.xaml`
- `tests/MyCodex.Tests/{Configuration,SecurityBoundary}Tests.cs`
- `tools/MyCodex.VisualAcceptance/AcceptanceFixture.cs`
- Compatibility, architecture, decision, task, changelog, and handoff records

Testing:

- Runtime strict lint/bundle and 41 tests passed.
- Release solution build passed with zero warnings/errors; 97 .NET tests passed.
- The standard release script passed all validation stages but could not remove
  its previous output because the user was running that exact background
  MyCodex binary; it was left untouched. An equivalent isolated self-contained
  publish and ZIP completed successfully.
- Isolated official Desktop fixture passed role/identity/native-surface,
  non-conversation exclusion, theme, exact-owned restart, destroy, and cleanup
  checks.
- Real signed-in Codex visual inspection remains pending in a disposable
  session.

## 2026-07-29 — Global official icon replacement

Date:

2026-07-29

Change:

- Saved the uploaded official ICO byte-for-byte as
  `assets/mycodex-source.ico`.
- Generated the repository-owned nine-size Windows ICO and 256×256 WPF PNG
  from that single source.
- Removed the obsolete SVG asset/reference and kept every existing application
  icon surface on the generated ICO/PNG pair.

Reason:

Use one permanent, reproducible MyCodex icon across source builds and release
artifacts without depending on a local upload path.

Impact:

Icon resources, icon references, and icon build documentation only. No business
logic, layout, text, interaction, lifecycle, or process behavior changed.

Modified Files:

- `assets/mycodex-source.ico`
- `assets/mycodex.ico`
- `assets/mycodex-logo.png`
- `assets/mycodex-logo.svg` (removed)
- `scripts/build-app-icon.ps1`
- `src/MyCodex.Manager/MyCodex.Manager.csproj`
- Icon-related project/release documentation

Testing:

- Runtime checks and 35/35 tests passed.
- Release build passed with zero warnings/errors; 96/96 .NET tests passed.
- Self-contained `win-x64` release build passed.
- Published EXE icon pixels matched the generated 32×32 ICO frame.
- Computer Use confirmed the new Manager, taskbar, and Explorer icons and the
  tray hide/restore lifecycle.

## 2026-07-29 — Beta 1 systemic remediation

Date:

2026-07-29

Change:

- Consolidated one-click restart into a staged, recoverable transaction.
- Separated taskbar minimize, tray hide, maximize/restore, and close states.
- Simplified the close choice window and restored standard caption controls.
- Rebuilt the visual icon chain from the repository-owned official ICO source.
- Strengthened multi-renderer calibration, role signatures, logical identity
  ownership, Whole-response grouping, rounded surfaces, and destroy cleanup.
- Deduplicated unchanged Runtime/discovery polling logs.
- Added regression coverage and release-memory documents.

Reason:

Beta 1 exposed shared lifecycle, state-model, semantic matching, and style-owner
defects. Fixing the shared boundaries avoids per-screenshot special cases.

Impact:

- Explicit Restart no longer requires a separate force confirmation or a
  second Start click in the covered paths.
- Process safety remains fail-closed and exact-target-only.
- Taskbar and tray behavior now follow standard Windows expectations.
- Calibration and decoration tolerate multi-unit replies without duplicating
  identities.
- Whole bubbles use stable content surfaces and retain complete rounded
  contours.
- Diagnostics retain state changes with substantially less repeated noise.

Modified Files:

- `src/MyCodex.Core/Applications/ApplicationRestartService.cs`
- `src/MyCodex.Core/Injection/DesktopSessionController.cs`
- `src/MyCodex.Core/Injection/RuntimeTargetSession.cs`
- `src/MyCodex.Manager/**`
- `src/MyCodex.Runtime/src/**`
- `src/MyCodex.Runtime/tests/**`
- `tests/MyCodex.Tests/**`
- `assets/**`, `scripts/build-app-icon.ps1`
- `CHANGELOG.md`, `docs/**`

Testing:

- Runtime lint/build and 35 tests passed.
- Release solution build passed with zero warnings/errors.
- 96 .NET tests passed.
- The self-contained `win-x64` release script passed and produced a ZIP plus
  SHA-256.
- Isolated official Desktop passed three restart cycles, reversible dark/light
  theme checks, Runtime disable/destroy checks, exact-PID/profile guards, and
  profile cleanup.
- Computer Use observed the built Manager icon, caption controls, taskbar
  minimize, maximize/restore, and simplified close dialog.
- Real signed-in Codex visual operation was not performed because the active
  Computer Use policy forbids automating Codex Desktop; this remains a release
  Blocker in `TASK_LIST.md`.
