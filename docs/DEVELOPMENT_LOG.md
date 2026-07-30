# Development Log

## 2026-07-29 — MyCO engineering-name normalization

Date:

2026-07-29

Change:

- Case-normalized the solution, projects, source/test/tool directories,
  namespaces, repository-owned brand assets, embedded Runtime bundle filename,
  CI, build scripts, and current engineering documentation from `Myco` to
  `MyCO`.
- Used two-step Git moves for Windows-safe case-only path changes.
- Retained `%APPDATA%\Myco`, transitional startup/config values, lowercase
  protocol/package identifiers, and legacy `MyCodex` compatibility inputs.

Reason:

Align repository-owned engineering names with the final MyCO brand while
preserving upgrade and cross-version behavior.

Impact:

Current build commands and references now use `MyCO.sln` and `MyCO.*` project
paths. Stable data, migration, serialization, IPC, and protocol identifiers are
unchanged.

Modified Files:

- `MyCO.sln`, `eng/MyCO.Version.props`
- `src/MyCO.*`, `tests/MyCO.Tests`, `tools/MyCO.*`
- `assets/MyCO*`
- CI/build scripts, README files, and current project documentation

Testing:

- Runtime strict checks and 42/42 tests passed; the renamed
  `dist/MyCO.runtime.js` bundle was regenerated.
- `dotnet build .\MyCO.sln -c Release` passed with zero warnings/errors.
- `dotnet test .\MyCO.sln -c Release --no-build` passed 102/102.
- `scripts\build-release.ps1` passed and produced `MyCO.exe` plus
  `MyCO-win-x64.zip`.
- Case-sensitive path scanning found no remaining repository filenames using
  `Myco`, `MyCodex`, or `Mycodex`.
- Remaining mixed/legacy content was classified as compatibility identifiers,
  migration tests, retained data paths, or historical records.

## 2026-07-29 — Repository identity normalization

Date:

2026-07-29

Change:

- Renamed the GitHub repository from `crikok0721/MyCodex` to
  `crikok0721/MyCO`.
- Updated the local `origin` URL.
- The local checkout directory rename from `MyCodex` to `MyCO` is pending
  because the active VS Code/Codex workspace still owns the directory.

Reason:

Complete the final external and local repository branding after the application
display name was standardized as MyCO.

Impact:

Branch, HEAD, Git history, working-tree changes, remote contents, and upgrade
compatibility identifiers remain unchanged. Existing GitHub redirects preserve
old repository URLs.

Modified Files:

- Git remote configuration and current project handoff/development records.

Testing:

- Verified the renamed GitHub repository, `origin`, branch, HEAD, and preserved
  dirty worktree.
- Verified that the first local directory rename was rejected by Windows while
  the active workspace owns the old path; no partial move occurred.

## 2026-07-29 — MyCO display-brand normalization

Date:

2026-07-29

Change:

- Standardized all current user-visible product names and executable metadata
  from Myco to MyCO without renaming stable engineering identifiers.
- Added the exact slogan `It's MyCO!!!!!` to the existing About and README
  brand areas.
- Changed release output to `MyCO.exe` and `MyCO-win-x64.zip`.
- Made the login-startup display value `MyCO` and added safe recognition of
  both `Myco` and `MyCodex`, including Windows Registry case-only migration.

Reason:

Apply the final brand capitalization while preserving configuration paths,
namespaces, Runtime protocol names, IPC, and upgrade compatibility.

Impact:

The product now displays MyCO throughout the Manager and package metadata.
`%APPDATA%\Myco`, project/namespace names, and lowercase Runtime identifiers
remain stable. Existing `Myco` login-startup values are normalized without
creating duplicates.

Modified Files:

- Manager resources/views, shared build metadata, startup registration,
  release/CI scripts, current documentation, and related tests.

Testing:

- Runtime lint/build and 42/42 tests passed.
- Release build passed with zero warnings/errors; 102/102 .NET tests passed.
- The standard release script produced `MyCO.exe` and
  `MyCO-win-x64.zip`.
- The final Manager was observed directly: title/header, actions, close dialog,
  About branding, and `It's MyCO!!!!!` were correct; application Exit closed
  only the tested Manager.
- The real case-only Run-value migration produced one exact `MyCO` value
  pointing to the current executable, with no `Myco` or `MyCodex` duplicate.

## 2026-07-29 — MyCodex to Myco engineering migration

Date:

2026-07-29

Change:

- Renamed the then-current user-visible branding, projects, namespaces, assets,
  Runtime bundle, executable, build paths, and release artifacts to MyCO.
- Added copy-only legacy user-data migration, startup-value migration,
  pre-rename Runtime cleanup, and cross-version single-instance compatibility.
- Advanced Config schema from 3 to 4 for the renamed startup field.
- Preserved the pre-existing removal of the title-bar version badge.

Reason:

Complete the formal product rename without losing settings, creating duplicate
login launches, or allowing old and new managers to run concurrently.

Impact:

New builds use `MyCO.exe`, `%APPDATA%\Myco`, and `Myco-win-x64.zip`. Legacy
data remains untouched after a successful copy. Historical records below keep
their original names and paths.

Modified Files:

- Current solution, project, source, test, tool, asset, build, CI, and
  documentation paths.

Testing:

- Runtime lint/build and 42/42 tests passed.
- Release solution build passed with zero warnings/errors; 100/100 .NET tests
  passed.
- The standard release script produced `MyCO.exe` and
  `Myco-win-x64.zip`; SHA-256 is recorded in `docs/HANDOFF.md`.
- Release startup, legacy data preservation/copy, and non-duplicated startup
  registration were verified. Current-brand visual acceptance remains pending.

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
