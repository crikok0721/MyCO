# Project Handoff (historical)

Last updated: 2026-08-06 (Asia/Hong_Kong)

This file is retained as historical handoff evidence. It is not the current
source of truth and its old artifact paths, test counts and continuation notes
must not be used as a launch instruction. Current state is maintained in
`docs/CONTEXT.md`, `docs/PROJECT_CONTEXT.md`, `docs/TASK_LIST.md` and the latest
entry in `docs/DEVELOPMENT_LOG.md`.

Repository: `C:\Users\crikok\Documents\MyCO`

The current working tree has no verified handoff executable. Build the source
tree after any further edits; do not launch an old ignored artifact. The active
local MyCO process, if present, must never be terminated by process name.

The sections below are historical records kept for traceability only. They are
not current validation claims.

## 1. Current status

MyCO is a Windows 10/11 x64 .NET 8 WPF manager plus an embedded TypeScript
Runtime for the official Codex/ChatGPT Desktop renderer. It is local, reversible,
privacy-safe, and fail-closed.

- Phase: Phase 3 — Beta testing and pre-release stability remediation
- Version: `0.99.2` (local incremental development; not released)
- Branch: `main`
- Base HEAD: `e52f21a1caef` (`main`); all 0.99.2 changes remain uncommitted.
- Release scope: the confirmed 0.99.2 UI, Runtime, tray, association, update,
  reset, and documentation changes are present only in the working tree.
- Git actions: no commit, push, tag, PR, or release was created.

The active controlling Codex session was never restarted or closed. Production
restart behavior was exercised only against an exact-owned isolated official
Desktop process tree.

## 2. Source of truth

Read in this order:

1. `CLAUDE.md`
2. `docs/CONTEXT.md`
3. `docs/architecture.md`
4. `docs/TASK_LIST.md`
5. `docs/DEVELOPMENT_LOG.md`
6. `docs/DECISIONS.md`

Historical evidence (archived):
- `docs/archive/CODEX_HANDOFF.md`
- `docs/archive/development-notes.md`

## 3. Completed in this session

### Product rename, display normalization, and upgrade compatibility

- Renamed current projects, namespaces, solution, assets, and embedded Runtime
  bundle from the old brand to `MyCO`.
- Standardized the user-visible brand and file metadata to MyCO. The final
  package contains `MyCO.exe` and is archived as `MyCO-win-x64.zip`.
- Added the exact slogan `It's MyCO!!!!!` to the existing About page and both
  README brand areas without changing layout.
- Advanced the product version to `0.99.1`; Config schema remains 4 for the
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
- Caption/taskbar and close-dialog user minimize all use the tray background
  route, hide the taskbar button, and preserve the prior Normal/Maximized
  state.
- The first user minimize in one Windows boot may show one localized tray
  notification; background startup and duplicate state events stay silent.
- Tray restore preserves the last non-minimized state and reuses one window.
- The close dialog now contains one short prompt and Exit / Minimize / Cancel;
  Escape cancels and Minimize is the default focused action.
- English, Simplified Chinese, Traditional Chinese, and Japanese resources are
  kept in sync.

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
- Each distinct legal message unit owns one identity; multiple Assistant
  progress/final units under one logical turn each receive an avatar and
  nickname, while Markdown children and bubble segments do not. Reconciliation
  removes duplicate, illegal, detached, or orphaned identity nodes.
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
  the historical unsigned package plus its executable (obsolete local paths
  intentionally omitted).
- The release script emitted the final ZIP SHA-256; record it with the
  distributed artifact rather than embedding a self-referential hash here.
- Published EXE metadata reports Product/File Description `MyCO`, Company
  `Crikok`, and original filename `MyCO.dll`; the ZIP has no
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
- The release route signs project-owned binaries with a per-release
  self-signed `CN=Crikok` certificate and publishes checksums, SBOM, and
  provenance. Windows public trust and SmartScreen reputation remain explicitly
  unavailable.

## 6. Public Beta gap

Current verdict: **修复若干 Blocker 后可以公开测试**.

Blockers are maintained in `docs/TASK_LIST.md`:

1. Disposable real signed-in production restart matrix.
2. Real conversation calibration/bubble visual matrix.
3. Fresh-machine install/config/recovery/removal validation.
4. Verify the self-signed release warning and integrity guidance on a fresh
   Windows environment.

High and Medium follow-ups are also recorded there. Do not begin unrelated
feature work before the Blockers are dispositioned.

## 7. Next recommended sequence

1. Review the current diff by the five subsystems above; preserve all unrelated
   user changes.
2. Move the locally built ZIP to a disposable Windows user/session or VM; do
   not publish it yet.
3. Execute the Blocker restart and real-conversation visual matrix.
4. Verify fresh configuration, disable/recovery, and removal.
5. Verify the tag-driven signing and GitHub provenance route.
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

The global machine has no installed .NET SDK. Local validation used the official
full .NET 8.0.423 SDK extracted temporarily under
`%TEMP%\MyCO-dotnet8-full-sdk\sdk` and placed first on `PATH`. It completed the
build and test commands above with 0 warnings/errors and 173/173 tests passed.
The Runtime check completed with 51/51 tests and regenerated the bundle.

The isolated self-contained executable is:

the historical 0.99.2 local executable (obsolete local path intentionally
omitted)

The matching updater is beside it as `MyCO.Updater.exe`. The release script was
run only in an isolated temporary worktree because running it in this checkout
would overwrite a retained pre-incremental package.
It produced and inspected a ZIP containing `MyCO.exe`, `MyCO.Updater.exe`,
`MyCO.runtimeconfig.json`, and `SHA256SUMS.txt`; no public package was created.

For a repeatable local SDK setup:

```powershell
$sdkRoot = Join-Path $env:TEMP "MyCO-dotnet8-full-sdk\sdk"
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

## 10. Current task handoff — theme, avatar crop, and first-run identity

This task adds the following Manager/Core behavior:

- `MainWindowViewModel` initializes the session-local preview from the
  effective `ThemeService` theme. In `System` mode it follows Windows changes
  until the user manually selects a preview theme. The Runtime palette remains
  independent and no preview selection is persisted.
- `src/MyCO.Manager/Views/AvatarCropWindow.xaml(.cs)` is the shared modal crop
  dialog for Assistant and User avatars. It accepts validated image bytes,
  supports drag, slider/button/mouse-wheel zoom, a 1:1 circular mask, and
  returns a 512x512 PNG only after Confirm. Cancel returns no bytes and does
  not save configuration.
- `src/MyCO.Manager/Services/AvatarCropMath.cs` contains the pixel-space cover,
  clamp, and square source-rectangle calculations. Keep its tests independent
  from WPF windows when changing crop behavior.
- `AvatarService.ReadValidatedAsync` validates a selected file without storing
  it; `AvatarService.ImportAsync(Stream)` is the only storage path for the
  cropped bytes. Managed content-hash names and Runtime data-URL restrictions
  remain unchanged.
- On `ConfigLoadResult.WasCreated`, `MainWindowViewModel` imports the packaged
  `Assets/MyCO-logo.png` into `%APPDATA%\Myco\avatars` and saves that managed
  path. `AppConfig.Default.Assistant.Name` is `菲叶子`; existing and migrated
  configurations are not overwritten.

How the next Agent should continue:

1. Start in `C:\Users\crikok\Documents\MyCO`; verify `git status` and inspect
   the current diff before editing. Do not use the historical
   `C:\Users\crikok\Documents\MyCodex` path.
2. Preserve the uncommitted Codex restart files and the current localization
   status key. Do not reset or checkout the worktree.
3. Use the temporary SDK when the global `dotnet` command reports no SDK:

   ```powershell
   $sdkRoot = 'C:\Users\crikok\AppData\Local\Temp\MyCO-dotnet8-sdk'
   & (Join-Path $sdkRoot 'dotnet.exe') build .\MyCO.sln -c Release
   & (Join-Path $sdkRoot 'dotnet.exe') test .\MyCO.sln -c Release --no-build
   ```

4. A historical published local executable may already be
   running and owns the single-instance mutex. Never close it by process name;
   use an isolated disposable session for visual crop/theme checks.
5. If a later Agent changes this flow, update this section and
   `docs/DEVELOPMENT_LOG.md` with the problem, cause, fix, verification, and
   exact continuation command.

交接问题记录（本任务已解决）：

- 问题：旧文档/工作目录仍指向 `C:\Users\crikok\Documents\MyCodex`；原因：
  仓库改名后的历史路径残留。解决：确认实际 Git 根目录为
  `C:\Users\crikok\Documents\MyCO`，仅更新当前交接说明，不改动兼容性标识。
  验证：在 MyCO 根目录执行 `git status --short`、构建和测试均成功启动。
  下一位 Agent：先 `Set-Location C:\Users\crikok\Documents\MyCO`，不要使用旧路径。
- 问题：全局 `dotnet` 无 SDK；原因：开发环境未把 SDK 放入 PATH。解决：使用临时
  `C:\Users\crikok\AppData\Local\Temp\MyCO-dotnet8-sdk\dotnet.exe`，不提交环境配置。
  验证：Release 构建 0 警告/0 错误。下一位 Agent：沿用上面的显式 `dotnet.exe` 命令。
- 问题：工作树已有 Codex 重启修复和本地化修改；原因：本任务开始前已有用户变更。
  解决：未 reset/checkout/覆盖，并在当前 diff 上增量修改。验证：重启相关测试保持通过，
  `git diff --check` 通过。下一位 Agent：先看 `git status`，只处理确认范围，不清理这些修改。
- 问题：已运行的 `artifacts\\MyCO-win-x64\\MyCO.exe` 持有单实例互斥；原因：当前开发会话
  仍在使用该实例。解决：未关闭、重启或按进程名终止。验证：当前进程仍存活；视觉验收标为
  未测试。下一位 Agent：使用隔离的开发实例/VM 做主题与裁剪器观察，不碰当前实例。

Validation at this handoff: Release build passed with 0 warnings/errors;
targeted crop/avatar/configuration/localization/theme tests passed 42/42;
full Release tests passed 120/121, with the one known environment-only
`System.Drawing.Common` load failure in the existing icon test. `git diff --check`
passed. Manager visual acceptance remains explicitly untested because the
current published single-instance process must not be closed.

### Follow-up repair — avatar import error

- 问题：用户选择助手头像后收到 `MCX-UI-ERRORIMPORTA-41D180`；同日日志随后又记录
  `MCX-UI-ERRORIMPORTA-749D08`。原因证据：前者类型为 `ArgumentException`，旧的
  隐私日志无法区分图片安全校验和 WPF 解码；后者为 `NullReferenceException`，代码确认
  `ZoomSlider.ValueChanged` 可在 `ZoomLabel` 创建前由 XAML 初始化触发。
- 解决：从 XAML 移除事件订阅，在 `AvatarCropWindow.InitializeComponent()` 完成后订阅，
  并保留空值防护。验证/解码失败现在分别显示三语提示，并仅记录 `stage=validation` 或
  `stage=decode`、`outcome=rejected`，不记录路径、文件名、图片或异常消息。
- 验证：Release 构建 0 警告/0 错误；头像、裁剪、配置和本地化测试 40/40 通过；
  完整 Release 测试 121/122 通过，唯一失败仍是测试环境缺少 `System.Drawing.Common`
  的既有图标测试。原始选图未被日志保存，因此对该文件的真实 UI 重试仍为未测试。
- 下一位 Agent 如何使用：运行最新
  `src\MyCO.Manager\bin\Release\net8.0-windows\MyCO.exe` 后重试。若仍失败，先在
  `%APPDATA%\Myco\logs\myco-YYYYMMDD.jsonl` 查找紧邻错误码之前的
  `avatar_import_rejected`：`validation` 表示格式/10 MiB/4096x4096 限制，`decode`
  表示 WPF 无法解码，应让用户重新导出为 PNG/JPEG。不要要求或记录真实图片路径。

## 11. Current task handoff — unit identities, Japanese, UI refresh, and reset

Date: 2026-08-01

- Runtime identity ownership is now scoped to each distinct legal
  `data-content-search-unit-key`. Independent Assistant progress/final units in
  one logical turn each receive one avatar/nickname; tool/native/protected rows
  remain unowned, refresh is idempotent, and destroy removes every identity.
- `ChatPreviewControl` uses one `PreviewBubbleStyle` and a testable
  `PreviewBubblePadding` built from horizontal and vertical values. Role colors
  remain separate; both roles share max width, radius, padding, line height, and
  borderless geometry in both preview themes.
- Settings startup spacing no longer uses a fixed description offset.
  Calibration is one continuous two-row workspace. Onboarding uses the Manager
  solid-surface/window-chrome system without gradients, glow, or transparency.
- `ja-JP` / `日本語` is supported by startup preload, live switching, atomic
  persistence, all Manager strings/dialogs/statuses, Japanese font fallbacks,
  `README.ja-JP.md`, and equal-key/equal-placeholder tests.
- The red Settings factory-reset action opens a localized confirmation with
  Cancel focused/default. It destroys MyCO Runtime without closing Codex,
  removes the three known login-startup names, stages only known MyCO data,
  rejects escapes/reparse points, preserves the data root and legacy source,
  recreates defaults and packaged logo, reloads the theme/ViewModel, and shows
  onboarding. Failure paths restore staged data and prior startup registration.

Validation:

- `npm.cmd ci`: passed; 68 packages audited, 0 vulnerabilities.
- `npm.cmd run check`: passed; lint, 42/42 tests, and generated bundle build.
- Release solution build: passed, 0 warnings / 0 errors.
- Full Release .NET suite: 131/131. The icon alpha regression test uses a pure
  .NET PNG decoder, so it is independent of the test host's GDI assemblies.
- `scripts\build-release.ps1 -GenerateSbom`: passed end to end and produced
  a `MyCO-win-x64.zip`, `SHA256SUMS.txt`, and an SPDX 2.2 SBOM.
- Archive SHA-256:
  `9850a8edca40d8068347901302c264389a38c5ea5fe80e333073eafa93df102c`.
- `git diff --check`: passed.
- Real visual acceptance for screenshots 1–6, Japanese, light/dark, minimum
  size, and 100%/150%/200% DPI was not performed. Computer Use policy forbids
  automating Codex Desktop, and no disposable Windows session/VM was available.
  Do not reinterpret XAML/DOM assertions as a visual pass.

Next agent: preserve the dirty worktree and use a disposable Windows account or
VM to inspect the built Manager and an isolated synthetic Codex profile. Do not
touch the real `%APPDATA%\Myco`, current Codex profile, or any controlling
process. No commit, push, PR, signing, or public release was performed.
