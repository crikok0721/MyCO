# Development Log

## 2026-08-06 — Geometry baseline v2 and refreshed self-contained release

Date:

2026-08-06

Change:

- Recalibrated the schema-7 geometry baseline: zero now resolves to a 35px
  avatar and -4px User avatar Y; Assistant avatar Y remains 11px.
- Added idempotent migration from schema-7 baseline v1 and preserved schema-0..6
  absolute-value migration through the new baseline.
- Updated the Manager preview's role-specific baseline converters and Runtime
  default/fallback variables, then regenerated the Runtime bundle via
  `npm.cmd run check`.
- Ran `scripts/build-release.ps1` from the current `0.99.2` source tree with
  the user-local .NET 8.0.423 SDK and generated `artifacts\MyCO-win-x64\` plus
  its ZIP.
- The directory contains `MyCO.exe`, `MyCO.Updater.exe`, runtime configuration,
  documentation and 499 SHA-256 manifest entries; all entries match.
- The ZIP SHA-256 is recorded in `MyCO-win-x64.zip.sha256` and matches the
  independently calculated archive hash.
- Archive SHA-256:
  `df20625ccb5066fd9c2ca70adfd47801c18a222ba51becec7c412c8c8ba1012d`.

Reason:

Provide a fresh directly launchable self-contained directory after removing
all previous local release outputs.

Impact:

This is a local unsigned, unreleased build. No public release, signing,
registry association or program launch was performed.

Testing:

Runtime lint and 57/57 tests, .NET Release build 0 warnings/0 errors and
238/238 tests, manifest verification 0 mismatches, archive hash verification,
and executable version `0.99.2.0` passed. Visual and real-user launch
acceptance remains pending; the executable was not launched.

## 2026-08-06 — Repository housekeeping

Date:

2026-08-06

Change:

- Removed obsolete ignored build outputs: pre-incremental MyCO packages,
  legacy MyCodex ZIPs, the SBOM tool cache, both superseded 0.99.2 directories,
  the empty icon-preview file, and re-creatable node/.NET build/test caches.
- Waited for the previously running local MyCO process to exit normally before
  removing its final artifact directory; no process was terminated and no
  executable was replaced.
- Marked `docs/HANDOFF.md` historical, made `docs/CONTEXT.md` the current
  validation handoff, and aligned repository instructions on the local 0.99.2
  unreleased state.

Reason:

Remove stale local outputs and prevent future agents from launching an old
ignored package while leaving a clean source checkout.

Impact:

Only ignored local artifacts/caches and current-document wording changed.
Source, official Codex files, user data and credentials were not removed.

Testing:

Verified no MyCO process remained, obsolete artifact/cache targets were absent,
and `git diff --check` passed. Source build/test results remain in the preceding
0.99.2 implementation entry; a fresh build is required before launch.

## 2026-08-06 — V0.99.2 realtime appearance, startup and onboarding repair

Date:

2026-08-06

Change:

- Added schema 6 independent Assistant/User avatar and nickname offsets and an
  Assistant-only ordinary-prose width, with legacy migration and validation.
- Made config application a serialized, all-renderer transaction with Runtime
  diagnostics, latest-save ordering, accurate zero/partial-session status and
  navigation-script replacement that preserves the last known-good source.
- Separated the three startup options, hardened MyCO-owned shortcuts/protocol
  with full-path reparse checks, generation-aware rollback and opaque snapshots,
  and retained single-instance associated launch routing.
- Kept the dependency-free one-per-boot BalloonTip, changed its title to
  `It's MyCO!!!!!`, and supplied MyCO AUMID/icon identity through a project-owned
  Start-menu shortcut.
- Updated the four-language onboarding heading and removed the obsolete
  description, privacy line, green dot and unused resources.

Reason:

Remove false-success appearance updates, make role placement independent, fix
startup association failures without touching official Codex entries, and
align the tray/onboarding experience with the 0.99.2 specification.

Impact:

Manager, Core configuration/injection/startup, Runtime source/generated bundle,
tests and project memory changed. No official Codex files, credentials, network
traffic or machine-wide settings were changed; no commit, release or PR was
created.

Modified Files:

See the current working-tree diff. The generated Runtime bundle was rebuilt only
through `npm run check`.

Testing:

Runtime `npm ci` and `npm run check` passed with lint, 52/52 tests and 0 known
package vulnerabilities. Release solution build passed with 0 warnings and 0
errors; .NET tests passed 229/229; `git diff --check` passed. Real signed-in
Codex visual response, Windows 10/11 notification rendering, DPI coverage and
the live eight-combination Windows startup matrix remain unverified.

## 2026-08-06 — Repair startup failure when inspecting existing MyCO shortcuts

Date:

2026-08-06

Change:

- Fixed `MCX-APP-STARTUP-*` failures caused by inspecting an uninitialised
  Windows ShellLink during startup association reconciliation. Existing `.lnk`
  files are now loaded through `IPersistFile` before their target and
  arguments are read; empty or malformed targets fail closed, and COM objects
  are released on every path.
- Added a Windows regression test that writes and then reloads a real shortcut.

Reason:

`IsOwnedShortcut` previously read from a blank ShellLink and passed an empty
target to `Path.GetFullPath`, which raised `ArgumentException` and aborted
startup before the main window was created.

Impact:

Existing MyCO-owned shortcuts can be reconciled safely; foreign or malformed
shortcuts are left untouched. A repaired self-contained validation artifact is
available at `artifacts\\MyCO-current-0.99.2-win-x64-20260806\\MyCO.exe`.

Modified Files:

- `src/MyCO.Core/Startup/CodexLaunchAssociationService.cs`
- `tests/MyCO.Tests/StartupTests.cs`
- `docs/DEVELOPMENT_LOG.md`

Testing:

The targeted regression test failed before the fix and passed after it. The
Release build passed with 0 warnings and 0 errors; .NET tests passed 174/174;
Runtime `npm run check` passed with 51/51 tests; and the repaired executable
was launched from the new artifact path with a responsive `MyCO` window and
no new startup error log entries. The old 20260803 artifact was not overwritten.

## 2026-08-03 — MyCO 0.99.2 local build and executable handoff

Date:

2026-08-03

Change:

- Completed the compile/test repair pass for the confirmed 0.99.2 incremental
  changes, including COM interop casts, update-package validation overloads,
  missing namespace imports, and regression-test fixtures/assertions.
- Published an isolated self-contained win-x64 build with the Manager and the
  single-file external updater; the existing pre-incremental artifact was not
  overwritten.

Reason:

Provide one unambiguous executable path for the current incremental build and
prevent future handoffs from pointing to a stale or experimental binary.

Impact:

No official Codex files, user data, credentials, or public release artifacts
were modified. The working tree remains uncommitted.

Modified Files:

- `src/MyCO.Core/Startup/CodexLaunchAssociationService.cs`
- `src/MyCO.Core/Updates/UpdatePackageValidator.cs`
- `src/MyCO.Core/Updates/UpdateCoordinator.cs`
- `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`
- `tests/MyCO.Tests/UpdateTests.cs`
- `tests/MyCO.Tests/ManagerThemeAndTrayTests.cs`
- `tests/MyCO.Tests/ManagerUiRegressionTests.cs`

Testing:

Full .NET SDK 8.0.423 build passed with 0 warnings and 0 errors; .NET tests
passed 173/173; Runtime `npm ci` and `npm run check` passed with 51/51 tests
and regenerated `dist/MyCO.runtime.js`; `scripts/build-release.ps1` also passed
in an isolated temporary worktree and produced a ZIP containing the Manager,
single-file updater, embedded Runtime evidence, and `SHA256SUMS.txt`.
`git diff --check` passed. The public registry audit endpoint was unavailable;
the one-shot offline npm audit cache reported 0 vulnerabilities.

## 2026-08-02 — MyCO 0.99.2 incremental feature set

## 2026-08-02 — MyCO 0.99.2 incremental feature set

Date:

2026-08-02

Change:

- Filled only the titlebar avatar presentation and added synchronized four-
  locale two-tone MyCO branding with the `It's MyCO!!!!!` subtitle.
- Unified user minimize paths into the existing single tray icon, hid the
  taskbar entry, preserved Normal/Maximized state, and persisted one balloon
  claim per Windows boot session.
- Added schema 5 migration for the default-off Codex launch association and
  boot-scoped tray notification state. Associations are limited to MyCO-owned
  Start-menu/Desktop shortcuts, a MyCO protocol, and MyCO's own AUMID; official
  Codex files, protocols, and pinned taskbar items remain untouched.
- Added official GitHub release checking, exact asset/hash/package validation,
  a temporary staged update area, and a separate external updater with exact
  PID/path/start-time waiting and rollback.
- Reworked the reset confirmation surface and added the four-language user-
  wording hard rule to `CLAUDE.md`.
- Reproduced the Runtime long-bar regression with a minimal nested prose shell;
  Whole mode now selects the innermost safe Markdown surface, rejects protected
  mixed-content shells, and uses an available-space bounded width rule.

Reason:

Complete the confirmed 0.99.2 scope while preserving the private CDP boundary,
single-instance lifecycle, atomic configuration, official Codex ownership, and
privacy-safe diagnostics.

Impact:

Manager/Core/Updater/config schema and Runtime source changed. The generated
Runtime bundle must be rebuilt by `npm run check`; no official Codex files or
user data are modified by the implementation.

Modified Files:

See the final handoff's Changed Files section; no commit or release was made.

Testing:

Runtime `npm ci`/`npm run check` passed locally: 0 vulnerabilities, lint, 51/51
tests, and generated bundle rebuild. XML/resource parity and `git diff --check`
also passed. The initial `dotnet build`, `dotnet test`, and
`scripts/build-release.ps1` attempts stopped at restore because the global
environment had no .NET SDK; the full-SDK completion and isolated release
validation are recorded in the 2026-08-03 entry above. Isolated Windows updater
replacement and real desktop visual acceptance remain pending.

## 2026-08-02 — MyCO 0.99.2 Manager UI repair

Date:

2026-08-02

Change:

- Unified the shared Home/Appearance chat preview so Assistant and User
  messages use the same solid user-bubble surface, text color, padding, and
  rounded geometry while preserving their original alignment and identity
  content.
- Replaced the Settings, About, Diagnostics, onboarding steps, and reset
  confirmation content-card outlines with the shared borderless card style.
  The reset card keeps the semantic light-danger surface and solid danger
  button; the reset command and confirmation flow are unchanged.
- Removed only the Home page-header appearance button; the overview-card and
  chat-preview-header edit entries remain.
- Added a local DPI-scaled rounded WinForms tray renderer with solid light/
  dark surfaces, inset separators, hover feedback, and disabled-state text.
  NotifyIcon ownership, item order, commands, language refresh, theme refresh,
  and double-click behavior remain in `TrayService`.
- Updated all four `FactoryResetDescription` resources and advanced only
  `MyCOVersion` to `0.99.2`; protocol and schema versions are unchanged.

Reason:

- Remove inconsistent preview and content-card surfaces without changing the
  existing Manager layout or Runtime behavior, and provide a stable local
  renderer for the tray menu instead of the system menu renderer.

Impact:

- Manager-only visual behavior and localized copy. No Runtime, CDP, process,
  reset-service, protocol, or calibration-schema changes.

Modified Files:

- `src/MyCO.Manager/Controls/ChatPreviewControl.xaml`
- `src/MyCO.Manager/Services/TrayMenuRenderer.cs`
- `src/MyCO.Manager/Services/TrayService.cs`
- `src/MyCO.Manager/Views/{HomePage,SettingsPage,AboutPage,DiagnosticsPage,OnboardingWindow,ResetConfirmationWindow}.xaml`
- `src/MyCO.Manager/Resources/Strings.{en-US,zh-CN,zh-TW,ja-JP}.xaml`
- `tests/MyCO.Tests/ManagerUiRegressionTests.cs`
- `tests/MyCO.Tests/ManagerThemeAndTrayTests.cs`
- `tests/MyCO.Tests/SecurityBoundaryTests.cs`
- `eng/MyCO.Version.props`

Testing:

- Release build passed with 0 warnings and 0 errors.
- Release .NET tests passed: 150/150.
- `git diff --check` passed.
- Manager light/dark visual checks passed for the visible Home preview and
  Settings cards. Tray-menu visual inspection remains incomplete because the
  current desktop observation surface did not expose the notification area.
- No commit, push, tag, PR, or release was created.

## 2026-08-02 — Locale-aware CJK typography and rendering repair

Date:

2026-08-02

Change:

- Replaced the unconditional mixed CJK WPF stack with a canonical
  `LocaleFontCatalog` for `en-US`, `zh-CN`, `zh-TW`, and `ja-JP`.
- Made WPF controls, windows, tooltips, and the WinForms tray menu resolve the
  active locale's font stack dynamically; windows also receive the matching
  `XmlLanguage` and pixel-rounding settings.
- Synchronized `CurrentCulture`, `CurrentUICulture`, default thread cultures,
  localized resources, and the Runtime `language`/`html.lang`/CSS font stack;
  a connected Runtime is refreshed immediately after a language selection.
- Replaced Runtime `system-ui` nickname rendering with the locale-aware stack,
  restoring the host document language during `destroy()`.
- Added CJK regression samples covering the known `恢复到默认设置`/`置`
  case and corresponding Traditional Chinese, Japanese, and English text.

Reason:

- `DesignTokens.xaml` placed `Yu Gothic UI` and `Meiryo UI` before the Chinese
  families, while every shared WPF style used a `StaticResource`; this caused
  Simplified Chinese windows to inherit a JP-prioritized fallback and remain
  stale after language changes. Runtime nicknames independently used
  `system-ui` without a locale contract.

Impact:

- Typography, locale, and rendering synchronization only. Existing layouts,
  colors, component geometry, privacy boundaries, Runtime decoration scope,
  and process lifecycle are unchanged. No CJK font files were bundled.

Modified Files:

- `src/MyCO.Manager/Localization/LocaleFontCatalog.cs`,
  `LocalizationService.cs`, `App.xaml.cs`, `Themes/DesignTokens.xaml`,
  `Themes/SharedStyles.xaml`, `Services/TrayService.cs`, and
  `ViewModels/MainWindowViewModel.cs`
- `src/MyCO.Core/Injection/RuntimeConfigSerializer.cs`
- `src/MyCO.Runtime/src/types.ts`, `runtime.ts`, `style-manager.ts`, and the
  generated `dist/MyCO.runtime.js`
- `tests/MyCO.Tests/LocaleFontCatalogTests.cs`, localization/configuration
  tests, and `MyCO.Tests.csproj`; Runtime tests; visual acceptance fixture
- `docs/superpowers/specs/2026-08-02-locale-aware-cjk-fonts-design.md` and
  `docs/superpowers/plans/2026-08-02-locale-aware-cjk-fonts.md`

Testing:

- `npm run check`: passed; 43 Runtime tests, lint, and bundle build passed.
- Isolated Release solution build with the bundled .NET 8.0.423 SDK: passed
  with 0 warnings and 0 errors.
- Full .NET Release tests: 143/143 passed; the test assembly was run with the
  installed .NET 8.0.6 WindowsDesktop host because the temporary SDK runtime
  lacked `PresentationFramework.dll`.
- Real WPF smoke run from the isolated build: verified `zh-CN → ja-JP →
  zh-CN → zh-TW → en-US`, settings-page and close-dialog text, then restarted
  and verified `zh-CN` persisted. The test process was closed normally.
- UTF-8 resource scan, font/transform/weight scan, and `git diff --check`
  completed without product encoding or static text-transform findings.

## 2026-08-01 — Avatar crop dialog initialization repair

Date:

2026-08-01

Change:

- Moved the avatar zoom slider event subscription out of XAML and after
  `InitializeComponent`, preventing initialization-time access to the later
  `ZoomLabel` named control.
- Added stage-specific avatar validation/decode errors and localized guidance
  while preserving the existing format, 10 MiB, and 4096x4096 limits.
- Added privacy-safe `validation`/`decode` stage diagnostics without image
  paths, filenames, content, or exception messages.

Reason:

- Avatar import logs recorded `ArgumentException` followed by
  `NullReferenceException`. The latter was caused by WPF raising
  `Slider.ValueChanged` before the complete XAML name scope was initialized;
  the earlier privacy-safe record could not distinguish validation from decode.

Impact:

- Manager avatar-import UI only. Avatar managed storage, configuration,
  Runtime, process control, restart boundaries, and user data are unchanged.

Modified Files:

- `src/MyCO.Manager/Views/AvatarCropWindow.xaml(.cs)`
- `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`
- `src/MyCO.Manager/Resources/Strings.*.xaml`
- `tests/MyCO.Tests/AvatarCropTests.cs`
- `docs/DEVELOPMENT_LOG.md`, `docs/HANDOFF.md`

Testing:

- Release build: passed with 0 warnings and 0 errors.
- Avatar/crop/configuration/localization regression subset: 40/40 passed.
- Full Release tests: 121 passed; the same existing icon test failed because
  this runner cannot load `System.Drawing.Common` (122 total).
- Visual retry with the user's selected image: not tested; the original image
  was not logged or copied by design.

## 2026-08-01 — Windows theme defaults, avatar crop, and first-run identity

Date:

2026-08-01

Change:

- Initialized the session-local chat preview from the effective Manager theme;
  System mode follows Windows until a manual preview selection is made.
- Added the shared WPF avatar crop dialog with circular mask preview, bounded
  pan/zoom, 512x512 PNG output, and cancel-without-save behavior.
- Routed cropped bytes through the existing AvatarService validation and managed
  content-hash storage path.
- Made new configurations use Assistant nickname `菲叶子` and seed the packaged
  `MyCO-logo.png` into the managed avatar directory without changing existing
  or migrated users.
- Changed the startup switch geometry to rounded rectangles and removed the
  requested settings-card copy in all three languages.
- Recorded and resolved handoff hazards (stale repository path, missing global
  SDK, pre-existing dirty restart changes, and the running single-instance
  Manager) with continuation commands in `docs/HANDOFF.md`.

Reason:

- Make the Manager and preview consistent with the user's Windows theme by
  default, support non-square avatar uploads safely, and align the settings UI
  with the requested product wording.

Impact:

- Manager/Core/configuration and tests only. Runtime source, generated bundle,
  private-pipe lifecycle, restart safety, and compatibility identifiers are
  unchanged.

Modified Files:

- `src/MyCO.Core/Avatars/AvatarService.cs`,
  `src/MyCO.Core/Configuration/AppConfig.cs`
- `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`,
  `src/MyCO.Manager/Services/AvatarCropMath.cs`,
  `src/MyCO.Manager/Views/AvatarCropWindow.xaml(.cs)`,
  `src/MyCO.Manager/Views/SettingsPage.xaml`,
  `src/MyCO.Manager/Themes/SharedStyles.xaml`, and the three string resources
- `tests/MyCO.Tests/AvatarCropTests.cs`, `AvatarTests.cs`,
  `ConfigurationTests.cs`, and `ManagerThemeAndTrayTests.cs`
- `docs/DECISIONS.md`, `docs/architecture.md`, `docs/CONTEXT.md`,
  `docs/PROJECT_CONTEXT.md`, and `docs/HANDOFF.md`

Testing:

- Release build with the temporary .NET 8.0.423 SDK: passed, 0 warnings and
  0 errors.
- Full Release tests: 120 passed; 1 pre-existing environment failure in
  `ReleaseIconIsMultiResolutionAndUsedByEveryManagerEntryPoint` because the
  runner cannot load `System.Drawing.Common`.
- Targeted crop/avatar/configuration/localization/theme tests: 42 passed.
- `git diff --check`: passed.
- Visual acceptance was not run: the existing published `MyCO.exe` owns the
  single-instance mutex and no disposable Windows session was available; the
  active process was not closed or restarted.

## 2026-08-01 — Reliable one-click Codex restart lifecycle

Date:

2026-08-01

Change:

- Fixed the "verified Codex process tree could not be closed safely"
  (`MCX-UI-ERRORSTARTDE-0608DA`) false failure that aborted one-click restart
  when Codex was still shutting down. The close stage now waits for the verified
  tree to exit (bounded by the graceful timeout) instead of treating a windowless
  root as immediate failure.
- Added tolerant kill handling: a mid-exit `Win32Exception`/`InvalidOperationException`
  race during `Process.Kill` is absorbed, and the residual-count loop remains the
  authoritative exit verifier. PID reuse still fails closed.
- Added bounded tolerance for transiently unreadable process snapshots during
  teardown (`SnapshotForPoll`/`SnapshotWithEntryRetry`), so a dying process no
  longer aborts the whole shutdown as an identity failure.
- After a successful graceful close, remaining verified headless descendants are
  force-cleaned without ever touching an unattributed or windowed process.
- The Manager `StartAsync` is now a single non-aborting transaction: a tolerated
  shutdown-stage timeout logs, re-verifies via `IsRootRunning`, and continues into
  launch instead of requiring a second click. A `SemaphoreSlim` restart gate
  serializes start/restart transactions.
- Added `StatusRestartingDesktop` ("正在重启 Codex…" / "Restarting Codex…")
  across en-US, zh-CN, zh-TW, and lifecycle diagnostics logs.

Impact:

- Runtime injection, calibration, bubble, avatar, config, and process-identity
  verification logic are unchanged. Restart tests grew to 21/21 covering slow
  teardown, root-exit-before-grace, orphan cleanup, kill races, PID reuse,
  transient snapshot failures, and refusal to terminate unattributed processes.

Validation:

- `dotnet build .\MyCO.sln -c Release`: 0 warnings, 0 errors.
- `dotnet test .\MyCO.sln -c Release`: 111 passed, 2 environment-dependent
  failures unrelated to this change (missing `System.Drawing.Common` assembly
  for the icon test; real HKCU registry access for the startup test).

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
- The fourth validation showed the PowerShell signer also blocked after
  importing the PFX private key into the Windows certificate store. Signing now
  passes the ephemeral PFX directly to SignTool and verifies the embedded
  signer identity without modifying a Windows trust store.
- A local direct-PFX smoke test signed `MyCO.exe`, `MyCO.dll`, and
  `MyCO.Core.dll` with `CN=Crikok`; all embedded signer thumbprints matched, and
  the temporary PFX/certificate directory was removed after validation.
- GitHub Actions validation run
  [`30566829783`](https://github.com/crikok0721/MyCO/actions/runs/30566829783)
  completed successfully on `windows-latest`: build/tests, direct-PFX signing,
  packaged version/signature verification, signed evidence upload, and build
  provenance attestation all passed. Release publication was correctly skipped
  because the validation run was dispatched from `main` rather than a tag.

## 2026-08-01 — Unit identities, Japanese UI, compact setup, and factory reset

Date:

2026-08-01

Change:

- Unified Assistant/User preview bubble geometry with one shared style and
  correct horizontal/vertical padding.
- Changed Runtime identity ownership from one pair per logical turn to one pair
  per distinct legal message unit, including multiple Assistant progress units.
- Reworked startup spacing, calibration, and onboarding into compact solid-
  surface Manager layouts.
- Added complete `ja-JP` UI resources, language persistence, font fallbacks, and
  Japanese project documentation.
- Added a localized red factory-reset action backed by a contained same-root
  staging transaction and rollback.

Reason:

The prior preview used two geometry algorithms; later Codex progress units lost
their identity because ownership was anchored to the enclosing turn; setup
pages used oversized, weakly related cards; Japanese was unavailable; and there
was no safe way to restore the first-launch state.

Impact:

- Preview geometry is consistent without merging semantic role colors.
- Each independent Assistant unit receives exactly one identity while native
  User bubbles, code, Diff, tools, status, controls, and input stay unchanged.
- Manager setup surfaces use the existing neutral/mint design system in both
  themes, and all visible strings now have four-language parity.
- Reset operates only on known immediate children of `%APPDATA%\Myco`, retains
  the root and legacy migration source, refuses reparse points, restores state
  on failure, and never closes Codex.

Modified Files:

- `src/MyCO.Runtime/src/runtime.ts` and Runtime tests/generated bundle
- `src/MyCO.Manager/Controls/ChatPreviewControl.*`
- `src/MyCO.Manager/Views/{SettingsPage,CalibrationPage,OnboardingWindow,ResetConfirmationWindow}*`
- `src/MyCO.Manager/Themes/*` and `ViewModels/MainWindowViewModel.cs`
- `src/MyCO.Core/Configuration/{LanguageCodes,FactoryResetService}.cs`
- `src/MyCO.Manager/Resources/Strings.*.xaml`
- `README*.md`, project memory, and corresponding xUnit tests

Testing:

- `npm.cmd ci` passed with 0 vulnerabilities; `npm.cmd run check` passed lint,
  42/42 Runtime tests, and bundle generation.
- Release solution build passed with 0 warnings and 0 errors.
- Full Release .NET tests passed 130/131; the only failure was the previously
  documented test-host `System.Drawing.Common` load failure in the existing
  icon test. Excluding that environment-only test, 130/130 passed.
- Factory-reset tests used temporary directories only; language tests verified
  key parity, placeholder parity, Japanese normalization, and save/reload.
- `git diff --check` passed.
- Real desktop visual acceptance was not performed; it remains required in a
  disposable session/VM and is not inferred from automated tests.

## 2026-08-01 — Local unsigned release package for the UI/localization baseline

Change:

- Ran `scripts\build-release.ps1 -GenerateSbom` with the Release configuration
  and `win-x64` self-contained publish target.
- Added `README.ja-JP.md` to the release copy list so the package carries all
  three project README languages.
- Replaced the icon alpha regression test's machine-specific GDI decoder with
  a pure .NET PNG decoder, making the full Release suite independent of the
  local test-host runtime layout.

Impact:

- `artifacts\MyCO-win-x64.zip` is a reproducible local unsigned release
  candidate containing the self-contained Manager, Japanese README, SPDX 2.2
  SBOM, per-file SHA-256 manifest, and archive hash.
- Archive SHA-256:
  `9850a8edca40d8068347901302c264389a38c5ea5fe80e333073eafa93df102c`.

Testing:

- Runtime checks: 42/42 passed; .NET Release tests: 131/131 passed; Release
  publish and SBOM generation completed successfully.
- All 493 file hashes matched `SHA256SUMS.txt`; ZIP contents matched the
  publish directory, and no certificate/private-key files were packaged.
- No signing certificate, commit, push, pull request, or public release was
  created. Real desktop visual acceptance remains a disposable-session/VM
  gate.

## 2026-08-02 — Version 0.99.1 release preparation

Change:

- Advanced the canonical product, Runtime package, documentation, and issue
  template version to `0.99.1`.
- Prepared the signed-release workflow to read SBOM and release-note versions
  from the canonical version source instead of a hardcoded value.
- Included the current preview-bubble, progress-message identity, Japanese
  localization, calibration/onboarding, and factory-reset implementation in the
  release scope.

Reason:

- Publish one coherent maintenance release without allowing stale `0.99.0`
  metadata to appear in the package, README files, or GitHub Release.

Impact:

- The `v0.99.1` tag will produce a self-contained Windows x64 package with
  per-release signing, SHA-256 checksums, SPDX SBOM, and provenance attestation.

Modified Files:

- `eng/MyCO.Version.props`, Runtime package metadata, release scripts/workflow,
  localized README files, changelog, and current project handoff documents.
- Current Manager/Core/Runtime UI and behavior changes already present in the
  working tree.

Testing:

- Runtime `npm ci`/`npm run check` passed with 42/42 tests and 0 vulnerabilities.
- .NET Release build and tests passed with 131/131 tests and 0 warnings/errors.
- Local `scripts\build-release.ps1 -GenerateSbom` passed and produced a
  0.99.1 self-contained ZIP, per-file SHA-256 manifest, archive hash, and SPDX
  SBOM.
- GitHub Actions run `30728714890` passed all release steps and published
  `https://github.com/crikok0721/MyCO/releases/tag/v0.99.1` as the latest
  release. The downloaded archive SHA-256 is
  `90904290e879943cf35a52891ba358c7de4dcdee49e506b961e3d560c4179720`, and
  `gh attestation verify` passed for the ZIP against `v0.99.1` and commit
  `a638a25b8b101297396c7ba832e9a036148cd9f6`.

Release verification:

- The packaged executable reports FileVersion `0.99.1.0` and ProductVersion
  `0.99.1+a638a25b8b101297396c7ba832e9a036148cd9f6`.
- The embedded Authenticode signer is `CN=Crikok`; Windows reports
  `UnknownError` for the self-signed certificate, which is expected because it
  is not in the public Windows trust chain.

## 2026-08-06 — Requirements ledger, bubble regression repair and geometry deltas

Change:

- Added the current requirements ledger and evidence audit, and updated the
  project entry rules to require a Requirement Impact Check. Historical
  completion claims are now separated from current evidence.
- Repaired Automatic/Whole segment selection for inline code and nested
  protected wrappers. Added structural cache invalidation while preserving
  streaming grouping and complete destroy cleanup.
- Introduced schema-7 baseline-relative appearance geometry deltas, a Core
  resolver, schema-0..6 migration, centered Manager slider ranges, and preview
  converters that use the same effective geometry and opposite right-anchor
  sign as Runtime. Runtime serialization carries both effective values and the
  delta metadata.
- Runtime host apply now rejects diagnostics that say the runtime is not
  installed instead of treating a CDP evaluation as a successful appearance
  apply.

Reason:

- User evidence showed Automatic text loss, Whole-mode boundary failures and
  preview sliders that were absolute values rather than centered adjustments.
  Historical docs also disagreed with the current HEAD and real visual gates.

Impact:

- Runtime fixture tests cover inline code, nested barriers and cache refresh.
- Existing startup, tray, onboarding, private-CDP, DOM safety and exact-process
  boundaries remain unchanged.
- WPF, Windows Shell, real Codex timing and DPI acceptance are still unverified;
  the executable was not launched against the active user/Codex profile.

Modified Files:

- `docs/REQUIREMENTS.md`, `docs/REQUIREMENTS_AUDIT.md`, `docs/CONTEXT.md`,
  `docs/architecture.md`, `docs/TASK_LIST.md`, `docs/DECISIONS.md`, this log,
  `AGENTS.md`, `CLAUDE.md`, and the implementation plan.
- `src/MyCO.Core/Configuration/{AppConfig,ConfigStore,AppearanceGeometry}.cs`
  and `src/MyCO.Core/Injection/{RuntimeConfigSerializer,RuntimeTargetSession}.cs`.
- `src/MyCO.Manager/{Controls/ChatPreviewControl.*,Views/AppearancePage.xaml,
  ViewModels/MainWindowViewModel.cs,Resources/Strings.*.xaml}`.
- `src/MyCO.Runtime/src/{bubble-segmenter,decorator,types}.ts` and regenerated
  `dist/MyCO.runtime.js`; focused Runtime and .NET regression tests.

Testing:

- `npm.cmd ci`: passed with 0 vulnerabilities; `npm.cmd test` and
  `npm.cmd run check`: passed, 56/56 Runtime tests and regenerated the bundle.
- WPF XAML and four-locale resource dictionaries parsed successfully; centered
  slider bindings and the requirements ledger/audit structure were checked with
  read-only scripts.
- Local .NET 8.0.423 SDK: `dotnet build .\MyCO.sln -c Release --no-restore`
  passed with 0 warnings/0 errors; `dotnet test .\MyCO.sln -c Release
  --no-build` passed 235/235.
- `scripts\build-release.ps1` passed and replaced the local publish output with
  `artifacts\MyCO-win-x64`; `MyCO.exe` is `0.99.2.0` and the generated
  `MyCO-win-x64.zip.sha256` sidecar records the archive hash.
- Real visual/system acceptance, including Windows 10/11 DPI and startup
  matrix, remains unverified; no published release was created.
