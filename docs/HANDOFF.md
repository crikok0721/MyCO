# Project Handoff

Last updated: 2026-07-29 (Asia/Hong_Kong)

Repository: `C:\Users\crikok\Documents\MyCodex`

Target local path: `C:\Users\crikok\Documents\MyCO` (rename pending until the
current VS Code/Codex workspace releases the directory)

GitHub: `https://github.com/crikok0721/MyCO`

## 1. Current status

MyCO is a Windows 10/11 x64 .NET 8 WPF manager plus an embedded TypeScript
Runtime for the official Codex/ChatGPT Desktop renderer. It is local, reversible,
privacy-safe, and fail-closed.

- Phase: Phase 3 — Beta testing and pre-release stability remediation
- Version: `0.3.0-beta.1`
- Branch: `beta2`
- Base HEAD: `59c46670a938a91e36dbc5314ab33e55318405c7` (`beta2`)
- Working tree: intentionally modified by the MyCodex → MyCO engineering
  migration, final MyCO display normalization, repository-name normalization,
  and the pre-existing title-bar version-label removal
- Git actions: no commit, push, pull request, or release was created
- Release conclusion: fix the remaining Blockers before public testing

The active controlling Codex session was never restarted or closed. Production
restart behavior was exercised only against an exact-owned isolated official
Desktop process tree.

## 2. Source of truth

Read in this order:

1. `AGENTS.md`
2. this file
3. `docs/PROJECT_CONTEXT.md`
4. `docs/architecture.md`
5. `docs/TASK_LIST.md`
6. `docs/DEVELOPMENT_LOG.md`
7. `docs/DECISIONS.md`

`docs/CODEX_HANDOFF.md` and `docs/development-notes.md` contain useful historical
Alpha evidence, but this file describes the current working tree.

## 3. Completed in this session

### Product rename, display normalization, and upgrade compatibility

- Renamed current projects, namespaces, solution, assets, and embedded Runtime
  bundle from the old brand to `MyCO`.
- Standardized the user-visible brand and file metadata to MyCO. The final
  package contains `MyCO.exe` and is archived as `MyCO-win-x64.zip`.
- Added the exact slogan `It's MyCO!!!!!` to the existing About page and both
  README brand areas without changing layout.
- Preserved version `0.3.0-beta.1`; Config schema advanced to 4 only for the
  renamed persisted startup field.
- Added copy-only migration from legacy `%APPDATA%\MyCodex` when the retained
  compatibility directory `%APPDATA%\Myco` does not exist, including safe
  avatar-path rewriting.
- Migrated known `MyCodex` and transitional `Myco` `HKCU\...\Run` values to
  the exact visible `MyCO` name after verifying the new command. Case-only
  registry migration deletes/recreates with rollback because registry value
  lookup is case-insensitive.
- Kept the legacy mutex/activation-event values so old and new launchers remain
  one cross-version instance.
- Added Runtime hot-upgrade cleanup for the pre-rename Symbol/window API before
  installing the MyCO DOM hooks.
- Preserved the user's earlier removal of the title-bar version badge.

### Restart lifecycle

- Added one `CloseForRestartAsync` transaction with explicit stages.
- Explicit Restart first requests normal close, then automatically uses the
  existing exact-identity force path only when needed.
- The force path still revalidates PID, executable path, start time, and unique
  process-tree ownership; uncertainty and multiple roots fail closed.
- Shutdown waits for stable process-family quiescence instead of a fixed sleep.
- Launch retries only bounded early-exit/readiness races and safely cleans the
  exact retry target between attempts.
- Failure refreshes actual Desktop state, preventing the previous half-failed
  state that required another Start click.
- User errors now identify unsafe identity, force-close, shutdown, launch, or
  readiness stage instead of exposing an opaque internal chain.

Primary files:

- `src/MyCO.Core/Applications/ApplicationRestartService.cs`
- `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`
- `tests/MyCO.Tests/ApplicationRestartTests.cs`

### Window, tray, and close behavior

- `WindowChrome` now provides native minimize, maximize, restore, drag, resize,
  and caption hit testing.
- Caption/taskbar minimize uses the Windows minimized state and never hides to
  tray.
- Only the explicit close-dialog Minimize choice calls the tray background
  route.
- Tray restore preserves the last non-minimized state and reuses one window.
- The close dialog now contains one short prompt and Exit / Minimize / Cancel;
  Escape cancels and Minimize is the default focused action.
- English, Simplified Chinese, and Traditional Chinese resources were kept in
  sync.

Primary files:

- `src/MyCO.Manager/Views/MainWindow.xaml`
- `src/MyCO.Manager/Views/MainWindow.xaml.cs`
- `src/MyCO.Manager/Views/CloseChoiceWindow.xaml`
- `src/MyCO.Manager/Views/CloseChoiceWindow.xaml.cs`
- `src/MyCO.Manager/Resources/Strings.*.xaml`

### Icon pipeline

- `assets/MyCO-source.ico` is the byte-identical official source of truth.
- `scripts/build-app-icon.ps1` generates a 256×256 WPF PNG and a nine-frame ICO
  from that source and replaces output atomically.
- WPF header/onboarding images use the PNG; executable, windows, taskbar, and
  tray continue to use the ICO.
- The generated Manager icon is the only project artwork referenced by current
  application surfaces.

Primary files:

- `assets/MyCO-source.ico`
- `assets/MyCO-logo.png`
- `assets/MyCO.ico`
- `scripts/build-app-icon.ps1`
- `src/MyCO.Manager/MyCO.Manager.csproj`
- `src/MyCO.Manager/Views/OnboardingWindow.xaml`

### Calibration and identity ownership

- Calibration starts in every renderer with positive conversation evidence.
- A conversation root must contain explicit turn/unit/role evidence; generic
  `main`, empty workspace, navigation, title, status, dialog, and composer
  surfaces fail closed.
- Each role calibration collects three different legal message roots, derives
  text-free structural consensus and a conversation-context fingerprint, then
  validates against current same-role messages including at least one held-out
  message before saving.
- Code, Diff, tool, status, toolbar, control, editor, and input clicks are
  rejected instead of climbing to an unrelated message ancestor.
- Saved screen position/layout no longer participates in role classification.
  Old single-sample calibration expires while names, avatars, appearance,
  language, and unrelated settings remain.
- One logical conversation turn owns one identity per role; reconciliation
  updates that pair and removes duplicate, illegal, detached, or orphaned
  identity nodes.
- One observer watches the confirmed conversation root and refreshes affected
  turns during streaming rather than suppressing changes inside decorated
  messages.

Primary files:

- `src/MyCO.Core/Injection/DesktopSessionController.cs`
- `src/MyCO.Core/Injection/RuntimeTargetSession.cs`
- `src/MyCO.Runtime/src/calibration.ts`
- `src/MyCO.Runtime/src/dom-utils.ts`
- `src/MyCO.Runtime/src/matcher.ts`
- `src/MyCO.Runtime/src/scanner.ts`
- `src/MyCO.Runtime/src/classifier.ts`
- `src/MyCO.Runtime/src/decorator.ts`
- `src/MyCO.Runtime/src/observer.ts`
- `src/MyCO.Runtime/src/runtime.ts`
- `src/MyCO.Core/Compatibility/ElementSignature.cs`
- `src/MyCO.Core/Compatibility/ElementSignatureValidator.cs`
- `src/MyCO.Core/Configuration/ConfigStore.cs`

### Bubble structure and style

- Whole mode prefers an existing stable Markdown/prose surface, including
  protected native children, without moving or rewriting DOM nodes.
- Identity and bubble ownership are independent.
- Every marked bubble surface keeps a complete border radius; the previous
  start/middle/end zero-corner rules were removed.
- User bubbles remain native. Code, tables, math, Diff, tools, statuses,
  toolbars, buttons, editors, and inputs remain excluded.
- Idempotent install, observer repair, Runtime hot upgrade, and destroy cleanup
  remain intact.

Primary files:

- `src/MyCO.Runtime/src/bubble-segmenter.ts`
- `src/MyCO.Runtime/src/style-manager.ts`
- `src/MyCO.Runtime/src/runtime.ts`
- `src/MyCO.Runtime/dist/MyCO.runtime.js` (generated)

### Diagnostics

- Unchanged target-discovery counts and Runtime-evidence records are no longer
  written on every poll.
- Logging remains allowlisted and text-free.

## 4. Validation completed

From the final source state:

- `npm ci`: passed; 0 vulnerabilities.
- `npm run check`: passed.
  - TypeScript strict lint: passed.
  - Runtime tests: 42/42 passed, including pre-rename Runtime cleanup.
  - Embedded Runtime bundle regenerated.
- `dotnet build .\MyCO.sln -c Release`: passed with 0 warnings and 0 errors.
- `dotnet test .\MyCO.sln -c Release --no-build`: 102/102 passed, including
  legacy data, persisted-field, and login-startup migration.
- `scripts\build-release.ps1`: passed end to end and produced
  `artifacts\MyCO-win-x64\MyCO.exe` plus `artifacts\MyCO-win-x64.zip`.
- The release script emitted the final ZIP SHA-256; record it with the
  distributed artifact rather than embedding a self-referential hash here.
- Published EXE metadata reports Product/File Description `MyCO`, Company
  `MyCO Contributors`, and original filename `MyCO.dll`; the ZIP has no
  old-brand entry names.
- The Release Manager started successfully. First-run upgrade copied the
  existing legacy user-data directory while preserving it, and startup
  registration reconciled to one exact `MyCO` value with no `Myco` or
  `MyCodex` duplicate.
- `git diff --check`: passed.
- The canonical source SHA-256 matched the uploaded ICO:
  `c5825c0da0171efea5f96bc7fce755241091a6d85505abb9ed7514e1866686a0`.
- The generated ICO contains 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixel
  32-bit DIB frames; the generated PNG matches the canonical 256×256 bitmap.
- The published EXE icon matched the generated 32×32 ICO frame pixel-for-pixel.

Isolated official Desktop acceptance:

- New isolated profile and private CDP pipe: passed.
- Fixture, Runtime handshake, roles, identities, Assistant bubble, circular
  avatars, native User/code/tool/Diff/status/toolbar preservation, and zero
  decoration in synthetic header/empty-state/composer regions: passed.
- Three consecutive exact-owned restart cycles: passed.
- Light → dark reversible theme checks and style singleton checks: passed.
- Disable/destroy removed Runtime style, created nodes, turn markers, and prose
  markers while leaving the fixture visible: passed.
- Stop removed the exact target and isolated profile directory: passed.

Historical Manager Computer Use observations from the prior Beta remediation:

- The official icon was crisp in the Manager header/title, Windows taskbar, and
  File Explorer after the normal Explorer refresh.
- Explicit tray hide kept one Manager process; relaunch restored the same
  window instead of creating a second process.
- Minimize, maximize, restore, and close controls rendered with standard glyphs.
- Repeated maximize/restore used the normal WPF/Windows state machine.
- Taskbar minimize left one restorable taskbar window instead of hiding to tray.
- Close dialog showed one prompt and three correctly labelled choices; Escape
  cancelled.
- The temporary Manager process was closed after inspection.

Current MyCO Computer Use observations:

- The published Manager window title, header, start action, close dialog,
  About title, product card, license line, and disclaimer display MyCO.
- The About page displays the exact slogan `It's MyCO!!!!!`.
- `%APPDATA%\Myco` is shown only as the intentionally retained data path.
- Exit through the Manager's own close dialog removed the tested MyCO process.

## 5. Not verified in this environment

Do not convert these into pass claims:

- Computer Use policy forbids automating Codex Desktop, including the isolated
  Codex window. Its DOM/Runtime checks passed, but that is not visual proof.
- The active signed-in Codex instance was intentionally not restarted because
  it controls this development session.
- Real signed-in long-conversation calibration, streaming, scroll,
  virtualization, task switching, copy/composer interaction, and every native
  tool/Diff variant still require visual inspection in a disposable session.
- Codex already-running/minimized/background production restart states were
  covered by unit/state tests and isolated target restarts, not by destructive
  operation on the user's active session.
- Windows 10, multiple monitors, 125%/150%/200% DPI, high contrast, and a clean
  external tester machine were not available.
- The Task Manager MyCO row and notification-area popup icon were not
  directly captured; their shared published-EXE/tray ICO resources were
  validated instead.
- The local ZIP is unsigned; SmartScreen and public installation trust remain
  unresolved.

## 6. Public Beta gap

Current verdict: **修复若干 Blocker 后可以公开测试**.

Blockers are maintained in `docs/TASK_LIST.md`:

1. Disposable real signed-in production restart matrix.
2. Real conversation calibration/bubble visual matrix.
3. Fresh-machine install/config/recovery/removal validation.
4. Signed distribution or an explicitly approved SmartScreen warning plan.

High and Medium follow-ups are also recorded there. Do not begin unrelated
feature work before the Blockers are dispositioned.

## 7. Next recommended sequence

1. Review the current diff by the five subsystems above; preserve all unrelated
   user changes.
2. Move the locally built ZIP to a disposable Windows user/session or VM; do
   not publish it yet.
3. Execute the Blocker restart and real-conversation visual matrix.
4. Verify fresh configuration, disable/recovery, and removal.
5. Decide the signing/SmartScreen route with the user.
6. Only after those checks, create small scoped commits if explicitly requested.

## 8. Required commands

```powershell
Push-Location .\src\MyCO.Runtime
npm ci
npm run check
Pop-Location

dotnet build .\MyCO.sln -c Release
dotnet test .\MyCO.sln -c Release --no-build

.\scripts\build-release.ps1
```

This validation found no installed .NET SDK. It used an official .NET 8.0.423
SDK extracted temporarily under `%TEMP%\MyCO-dotnet8-sdk` and placed first on
`PATH`:

```powershell
$sdkRoot = Join-Path $env:TEMP "MyCO-dotnet8-sdk"
$env:PATH = "$sdkRoot;" + $env:PATH
```

`-UseChinaMirrors` is permitted only as a local build option when normal package
routes are slow; never commit regional package-source configuration.

## 9. Safety reminders

- Never restart or close the Codex session controlling development.
- Never enumerate/terminate Desktop by process name.
- Never weaken PID/path/start-time/tree ownership checks.
- Never inspect or copy a real profile, chat, DOM snapshot, cookie, credential,
  or account data.
- Never hand-edit `src/MyCO.Runtime/dist/MyCO.runtime.js`.
- Do not commit, push, create a PR, or publish without an explicit request.
