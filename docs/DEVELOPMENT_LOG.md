# Development Log

## 2026-07-30 — Final Manager UI preview consolidation

Date:

2026-07-30

Change:

- Simplified the title bar and sidebar brand areas to remove duplicate
  subtitles while preserving native window chrome and drag behavior.
- Reduced the shared conversation preview to one Assistant message and one
  User message, removed its nested scroll/status/demo surfaces, and reused it
  unchanged on Home and Appearance.
- Added one session-local Codex Dark/Light preview selection whose resolved
  palette controls the complete preview independently from the Manager theme.
- Replaced the About version badge with the canonical portrait asset and
  removed the requested Calibration and Appearance explanatory copy/cards.

Reason:

Complete the final UI cleanup, remove demonstration-only content, and prevent
mixed Manager/Codex preview palettes without changing Runtime, calibration,
process, IPC, or persisted appearance behavior.

Impact:

Manager presentation, localized resources, and preview-only ViewModel state
changed. Runtime injection, calibration algorithms, process ownership,
configuration schema, saved palettes, and compatibility identifiers are
unchanged.

Modified Files:

- `src/MyCO.Manager/Controls/ChatPreviewControl.xaml`
- `src/MyCO.Manager/Views/{MainWindow,HomePage,AppearancePage,CalibrationPage,AboutPage}.xaml`
- `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`
- `src/MyCO.Manager/Resources/Strings.*.xaml`
- Manager UI/localization tests and current project records

Testing:

- `dotnet build .\MyCO.sln -c Release` passed with zero warnings/errors;
  `dotnet test` passed 105/105.
- Runtime lint/build and 42/42 tests passed through the standard release script.
- Real desktop inspection passed for the compact Home preview, shared
  Appearance preview, live Dark/Light selection, title/sidebar cleanup,
  simplified Calibration, and canonical About icon.
- The release script produced `MyCO.exe` and `MyCO-win-x64.zip`; archive
  SHA-256: `e5fbb4c62e494eee38cf42c562adcd80168fdd6a7cb6cfe5813613abb558bc2f`.
- XAML parsing, localization key parity, deleted-copy residual scanning, and
  `git diff --check` passed.

## 2026-07-30 — Phase 3 role-first Manager visual refactor

Date:

2026-07-30

Change:

- Replaced pill-heavy and outline-heavy Manager components with a layered
  rounded-rectangle shape system and solid semantic surfaces.
- Added a Windows 11 DWM corner request with `WindowChrome` fallback and
  maximize/restore-aware content clipping.
- Rebuilt Home around clickable Assistant/User identities and moved connection
  state, target selection, and existing lifecycle commands into a compact
  sidebar dock.
- Applied a deterministic anti-aliased rounded-rectangle mask to every derived
  PNG/ICO frame while preserving the canonical portrait artwork.
- Aligned Appearance, Calibration, and the shared conversation preview with the
  revised surface and shape hierarchy.

Reason:

Match MyCO's role as a lightly anime-styled appearance companion, reduce
template-like dashboard styling, and improve native desktop polish without
changing application behavior.

Impact:

Manager presentation and home-page information architecture changed. Runtime,
configuration, calibration rules, process ownership, startup, tray, IPC, and
upgrade-compatible identifiers are unchanged. No dependency was added.

Modified Files:

- `src/MyCO.Manager/Themes/*`
- `src/MyCO.Manager/Views/MainWindow.*`, `HomePage.xaml`,
  `CalibrationPage.xaml`
- `src/MyCO.Manager/Controls/ChatPreviewControl.xaml`
- `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`
- `src/MyCO.Manager/Resources/Strings.*.xaml`
- `scripts/build-app-icon.ps1`, `assets/MyCO.ico`,
  `assets/MyCO-logo.png`
- Manager UI tests and current project records

Testing:

- Manager Release build and 15 focused localization/UI tests passed.
- Real desktop inspection passed for role-first Home, role navigation,
  Appearance, Calibration, Light/Dark themes, focus states, and
  maximize/restore behavior.
- Runtime strict checks and 42/42 tests passed.
- `dotnet build .\MyCO.sln -c Release` passed with zero warnings/errors;
  `dotnet test` passed 105/105.
- The standard release script produced `MyCO.exe` and
  `MyCO-win-x64.zip`; the current archive SHA-256 is recorded in the task
  completion report.
- The packaged executable was started and visually inspected, then closed
  through MyCO's orderly exit path. `git diff --check` passed.

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

## 2026-07-30 — Premium Manager visual foundation and core home

Date:

2026-07-30

Change:

- Added a three-layer WPF design-token foundation with a complete mint scale,
  unified typography, spacing, shape, elevation, and motion resources.
- Reworked light and dark Manager themes around neutral surfaces, restrained
  mint accents, and a subtle warm near-black dark palette.
- Added complete shared states for buttons, inputs, navigation, selection,
  focus, disabled controls, switches, progress, cards, and page entry motion.
- Rebuilt the application shell so navigation is separate from lifecycle
  actions and connection state is truthful in every theme.
- Added a state-led home page using the existing detect/start/restart/enable/
  disable commands.
- Added one shared representative conversation preview and reused it on Home
  and Appearance. The preview distinguishes decorated Assistant prose from
  native User, code, and status surfaces.
- Reorganized Appearance into a live preview plus a progressively disclosed
  inspector without changing the configuration schema.
- Added three-language UI resources and source-level visual-boundary tests.
- Fixed runtime-only WPF resource resolution and dynamic-null-placeholder
  parsing defects found by launching the isolated publish.
- Corrected right-docked status pills and actions so they size to content, and
  normalized the remaining visible `MYCO` labels to `MyCO`.

Reason:

The Manager needed a mature, low-cognitive-load interface consistent with
MyCO's actual role as a lightly anime-styled AI appearance plugin. Central
tokens and shared components prevent continued theme and state drift.

Impact:

- Existing configuration, Runtime, CDP, restart, tray, injection, and packaging
  behavior remain unchanged.
- MyCO now opens on a core state-and-preview home page.
- Windows reduced-motion and high-contrast preferences collapse decorative
  animation durations to zero.
- No third-party UI or animation dependency was added.

Modified Files:

- `src/MyCO.Manager/Themes/**`
- `src/MyCO.Manager/Controls/ChatPreviewControl.*`
- `src/MyCO.Manager/Views/MainWindow.xaml`
- `src/MyCO.Manager/Views/HomePage.*`
- `src/MyCO.Manager/Views/AppearancePage.xaml`
- `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`
- `src/MyCO.Manager/Resources/Strings.*.xaml`
- `src/MyCO.Manager/App.xaml*`
- `tests/MyCO.Tests/ManagerThemeAndTrayTests.cs`
- `docs/PROJECT_CONTEXT.md`
- `docs/DECISIONS.md`
- `docs/DEVELOPMENT_LOG.md`

Testing:

- Runtime lint/build and 42 tests passed.
- Release solution build passed with zero warnings and zero errors.
- 105 .NET tests passed, including premium token, theme-key parity,
  localization, command reuse, and native-preview-boundary checks.
- An isolated self-contained `win-x64` publish completed successfully.
- The standard release script completed and refreshed
  `artifacts/MyCO-win-x64` plus `MyCO-win-x64.zip`; the ZIP SHA-256 is
  `f4fb5fe72acca59c0c663a8ba97b1857e47e9d0f20eedaf9fc3c8cd11dc3bab4`.
- All named XAML resources resolved, core XAML parsed, `git diff --check`
  passed, and checked light/dark text pairs met their intended contrast target.
- Computer Use verified the isolated self-contained build in dark and light
  themes at the default size and the 980x660 minimum, including Home,
  Appearance, Settings, preview/native-surface boundaries, page transition,
  visible keyboard focus, maximize/restore, and the themed close dialog.
- No lifecycle action targeting Codex was invoked during Manager visual
  verification. The isolated Manager process exited normally after acceptance.

## 2026-07-30 — Version 0.99.0 autonomous release engineering

Date:

2026-07-30

Change:

- Advanced the single product/runtime/package version source to `0.99.0`.
- Standardized author, assembly, copyright, SBOM supplier, and signing identity
  metadata on `Crikok`.
- Added per-release ephemeral self-signed Authenticode generation, SHA-256
  signing, temporary local-chain verification, and private-key cleanup.
- Added a tag-gated GitHub Release workflow, SPDX SBOM, archive and file hash
  manifests, GitHub build provenance, CodeQL, and Dependabot.
- Added release, signing-policy, integrity-verification, and trust-limitation
  documentation in Chinese and English.

Reason:

Prepare an independently executable open-source release process without
requiring commercial certificate purchase, external identity validation, or
human signing approval, while remaining explicit that self-signing does not
provide Windows public CA trust or guaranteed SmartScreen reputation.

Impact:

- `v0.99.0` tags can produce and publish a self-contained signed Windows x64
  package with checksums, SBOM, and provenance.
- Every release has a different signing certificate and no cross-version
  publisher reputation.
- Runtime protocol and configuration/calibration schemas remain unchanged.

Modified Files:

- `eng/MyCO.Version.props`
- `Directory.Build.props`
- `src/MyCO.Runtime/package*.json`
- `scripts/*.ps1`
- `.github/workflows/*.yml`
- `.github/dependabot.yml`
- `security/CODE_SIGNING.md`
- `docs/release-process.md`
- `docs/releases/v0.99.0.md`
- release and project memory documentation

Testing:

- Runtime lint, 42 tests, and bundled Runtime generation passed at `0.99.0`.
- npm dependency audit reported zero vulnerabilities.
- Release solution build passed with zero warnings and zero errors.
- 105 .NET tests passed.
- The unsigned local release path produced a self-contained ZIP, SPDX 2.2
  SBOM, per-file SHA-256 manifest, and archive SHA-256 file.
- Authenticode signing remains to be verified on the isolated GitHub-hosted
  Windows release runner because the local execution policy blocks PFX handling.
- The first GitHub signing validation reached Publish after all tests, then was
  canceled when the initial timestamped SignTool call produced no output for
  several minutes. SignTool was changed to the Microsoft timestamp endpoint
  with a bounded process timeout and captured diagnostics for the rerun.
- The second validation confirmed that public timestamp dependencies still
  prevented a bounded autonomous release. The default release therefore signs
  without an external timestamp; certificate-expiry limitations are explicit
  in the user, security, release, and architecture documentation.
- The third validation showed that SignTool itself remained blocked with the
  runner-generated temporary key even without timestamping. Default signing now
  uses Windows PowerShell `Set-AuthenticodeSignature` and verification uses
  `Get-AuthenticodeSignature`; no SignTool process is required.
