# Project Handoff Document

Last updated: 2026-07-28 (Asia/Hong_Kong)

Repository: `C:\Users\crikok\Documents\MyCodex`

This document describes the current local repository and uncommitted working
tree. It is intended to be the primary recovery point for the next long-term
maintainer.

Source-of-truth note:

- The current session supplied a new project-agent policy that supersedes
  earlier agent instructions.
- The checked-in/root `AGENTS.md` currently contains only repository build,
  code, security, and release rules; it does not yet contain the complete
  project-memory/workflow policy supplied in the current session.
- `docs/PROJECT_CONTEXT.md`, `docs/TECH_STACK.md`, `docs/TASK_LIST.md`,
  `docs/DECISIONS.md`, and `docs/DEVELOPMENT_LOG.md` do not currently exist.
  Do not assume their contents. Creating or reconciling them requires a
  separately approved documentation task.
- `docs/CODEX_HANDOFF.md` contains valuable historical Alpha context, but much
  of its main body is stale relative to the current Beta working tree. Use this
  document as the current handoff.

## 1. 项目简介

### 项目目标

MyCodex is a local Windows manager and reversible appearance layer for the
official Codex/ChatGPT Desktop application. It launches a supported official
Desktop executable through a controlled Chrome DevTools Protocol connection,
injects an embedded TypeScript Runtime into the renderer, and decorates only
safe conversation presentation surfaces.

The project must preserve the official application, user profile, credentials,
network behavior, User bubble, composer, code, Diff, tool, status, toolbar,
button, editor, and input surfaces. It must not modify `app.asar`, official
binaries, installed resources, or authentication data.

### 当前实现功能

- Windows WPF Manager with onboarding, Appearance, Calibration, Diagnostics,
  privacy/About pages, localization, single-instance activation, independent
  themes, tray lifecycle, and startup settings.
- Official Desktop installation/running-process discovery for packaged and
  legacy families.
- Private CDP pipe launch by default, with explicit-consent random
  `127.0.0.1` TCP fallback.
- Runtime injection, target discovery/scoring, protocol handshake, target/SPA
  monitoring, health repair, Safe Mode, and reversible `destroy()`.
- Assistant/User avatars and nicknames; only Assistant prose receives MyCodex
  bubble styling.
- Automatic semantic bubble grouping and Whole-response bubble mode.
- Atomic versioned settings, migration/recovery, calibration, avatar import,
  and allowlisted privacy-safe diagnostics.
- Exact-identity production restart using PID, executable path, start time, and
  process-tree ownership.
- Independent Manager/Codex lifecycle: exiting MyCodex should leave Codex
  running.
- Development-only isolated CDP probe and VisualAcceptance host.
- Self-contained `win-x64` ZIP release build.

### 技术定位

MyCodex is not an overlay, OCR renderer, browser extension, API client, native
injector, or patcher. It is a .NET 8 WPF host plus an embedded, project-scoped
TypeScript DOM Runtime connected to the official Chromium renderer through CDP.

### 当前开发阶段

Phase 3: Beta testing and pre-release stability remediation.

The current working-tree version is `0.3.0-beta.1`. Core implementation and
automated validation for the Beta remediation are complete, but final
multi-environment and real-user visual acceptance is not complete. The project
is not ready for an unconditional public-release claim.

## 2. 当前项目状态

Completed:

- Phase-one Manager/Core/Runtime architecture and security boundaries.
- English, Simplified Chinese, and Traditional Chinese UI resources.
- Private-pipe-first transport and explicit-consent loopback TCP fallback.
- Capability/structure-based DOM classification with fail-closed Safe Mode.
- Assistant-only bubble decoration, avatar/nickname support, calibration, and
  reversible cleanup.
- Manager and renderer theme separation, tray lifecycle, single-instance
  restoration, and per-user startup settings.
- Alpha.4 exact-identity restart protections covering PID reuse, multiple
  roots, parent/child ownership, and natural-exit races.
- Beta.1 one-click restart orchestration: process-tree quiescence, bounded
  early-exit retry, renderer readiness, Runtime application before success,
  and single-flight UI operations.
- Beta.1 independent process lifecycle through suspended launch and exact
  target pipe-peer handle duplication.
- Exit / Minimize to tray / Cancel close choice and expanded tray commands.
- Official multi-frame application icon embedded in the EXE and reused by WPF
  windows and tray.
- Automatic and Whole bubble modes with Config schema 3 migration.
- Streaming-stable semantic grouping with protected native structures.
- Final Runtime check `30/30`, .NET tests `91/91`, Release build with zero
  warnings/errors, and self-contained `win-x64` archive generation.
- Isolated private-pipe detach survival and automated VisualAcceptance restart,
  dark/light, destroy, and cleanup gates.

In Progress:

- Review and disposition of the complete uncommitted Beta.1 working tree.
- Final real-user/visual acceptance of the new Manager close dialog, tray
  commands, taskbar, Alt+Tab, icon surfaces, and production one-click restart.
- Release provenance, signing, and clean-checkout/CI verification.
- Reconciliation of project memory documents and stale historical handoff
  content.

Todo:

- Perform the final manual acceptance matrix without restarting the Codex
  session controlling development.
- Test Windows 10 22H2, common DPI values, high-contrast mode, English Windows,
  custom/spaced/Chinese installation paths, and ordinary-user behavior.
- Validate real signed-in conversations across multiple official Desktop
  versions, including copy/selection, scrolling, composer, Markdown, code,
  Diff, tools, status, approvals, buttons, and toolbar actions.
- Establish code signing and SmartScreen/release provenance.
- Run CI from an approved commit and inspect the produced artifact.
- Decide whether to introduce MSI/MSIX or keep the self-contained ZIP model.
- Decide whether to add a repository `global.json` for .NET SDK pinning.
- Create/reconcile `PROJECT_CONTEXT.md`, `TECH_STACK.md`, `TASK_LIST.md`,
  `DECISIONS.md`, and `DEVELOPMENT_LOG.md` under a separate approved task.
- Commit/push/open a PR/publish only after explicit user approval.

### 当前版本状态

- Application/Runtime version: `0.3.0-beta.1`
- Host/Runtime protocol: `1`
- Configuration schema: `3`
- Calibration schema: `1`
- Release RID: `win-x64`
- Distribution status: local unsigned Beta candidate; not published
- Latest local archive:
  `artifacts/MyCodex-win-x64.zip`
- Latest recorded SHA-256:
  `172b1bfa812ea518054fe4936500c445ab15f269b081f1f88ded964481df05e0`

## 3. 技术架构总结

### 技术栈

- Host language/runtime: C# with nullable reference types, .NET 8
- Desktop UI: WPF, MVVM-style ViewModel, Windows Forms `NotifyIcon`
- Renderer Runtime: TypeScript 5.7, strict mode, bundled by esbuild
- DOM test environment: Node.js test runner and jsdom
- Host tests: xUnit on `net8.0-windows`
- Transport: inherited anonymous private CDP pipes; consent-gated loopback TCP
- Persistence: versioned JSON under `%APPDATA%\MyCodex`, atomic file replacement
- Startup registration: exact per-user `HKCU\...\Run` value
- Build/release: PowerShell and `scripts/build-release.ps1`
- CI: Windows GitHub Actions workflow

### 项目目录结构

```text
.
├─ eng/
│  └─ MyCodex.Version.props       Shared version/schema metadata
├─ src/
│  ├─ MyCodex.Manager/            WPF UI, resources, ViewModel, tray
│  ├─ MyCodex.Core/               Discovery, process/CDP/config/session logic
│  └─ MyCodex.Runtime/
│     ├─ src/                     Hand-written TypeScript Runtime
│     ├─ tests/                   jsdom/Runtime tests
│     └─ dist/                    Generated embedded bundle
├─ tests/MyCodex.Tests/           .NET unit/security/integration-style tests
├─ tools/MyCodex.CdpProbe/        Isolated pipe and detach feasibility gate
├─ tools/MyCodex.VisualAcceptance/ Isolated official-renderer acceptance host
├─ scripts/                       Release and icon-generation scripts
├─ assets/                        Project-owned icon/visual resources
├─ docs/                          Architecture, compatibility, settings, history
└─ .github/workflows/             CI
```

### 核心模块职责

#### `MyCodex.Manager`

- Owns WPF application lifecycle and single-instance activation.
- Presents settings, calibration, diagnostics, operation progress, restart
  consent, and localized failures.
- Owns the single tray icon and close-choice UI.
- Serializes user operations through `MainWindowViewModel`.
- Loads the generated Runtime bundle as an embedded resource.

Key files:

- `src/MyCodex.Manager/App.xaml.cs`
- `src/MyCodex.Manager/ViewModels/MainWindowViewModel.cs`
- `src/MyCodex.Manager/Services/TrayService.cs`
- `src/MyCodex.Manager/Views/CloseChoiceWindow.xaml`
- `src/MyCodex.Manager/Resources/Strings.*.xaml`

#### `MyCodex.Core`

- Discovers and resolves supported official applications.
- Creates and owns CDP transports.
- Starts the exact selected process and manages its identity.
- Selects renderer targets and injects/monitors Runtime sessions.
- Performs safe production restart and process-tree quiescence checks.
- Owns configuration migration, avatar import, compatibility state, startup
  registration, and privacy-safe logs.

Key files:

- `Applications/WindowsApplicationLocator.cs`
- `Applications/ApplicationCandidateResolver.cs`
- `Applications/ApplicationRestartService.cs`
- `Cdp/WindowsPipeProcessLauncher.cs`
- `Discovery/TargetDiscoveryService.cs`
- `Injection/DesktopSessionController.cs`
- `Injection/RuntimeInjector.cs`
- `Configuration/AppConfig.cs`
- `Configuration/ConfigStore.cs`

#### `MyCodex.Runtime`

- Finds a semantic conversation root and bounded turn candidates.
- Classifies Assistant/User roles from capability and stable structure.
- Decorates only high-confidence Assistant prose and identity areas.
- Groups safe prose into Automatic or Whole bubbles.
- Preserves code, table, math, Diff, tool, status, command, toolbar, button,
  editor, and input surfaces.
- Observes incremental DOM changes and keeps streaming grouping stable.
- Reports only allowlisted technical events through a random binding.
- Removes all MyCodex state through idempotent `destroy()`.

Key files:

- `src/runtime.ts`
- `src/scanner.ts`
- `src/classifier.ts`
- `src/decorator.ts`
- `src/bubble-segmenter.ts`
- `src/style-manager.ts`
- `src/observer.ts`
- `src/bridge.ts`
- `src/types.ts`

### 模块之间关系

```text
Manager UI
  -> Core application discovery/resolution
  -> Core exact process launch
  -> private CDP pipe (preferred) or consented loopback TCP
  -> renderer target discovery
  -> Runtime injection and protocol handshake
  -> Runtime DOM matching and decoration
  -> allowlisted Runtime events back to Core/Manager
```

Application discovery, CDP transport, Runtime injection, DOM matching, and
visual decoration are separate maintenance boundaries. Do not collapse them
into one version-specific adapter.

### 数据流/调用流程

1. Manager loads and validates `%APPDATA%\MyCodex\config.json`.
2. Core discovers installed/running official Desktop candidates.
3. A selected executable is created suspended with private-pipe arguments.
4. Only required pipe handles are inherited; host-side peer handles are
   duplicated into the exact target and made non-inheritable.
5. The target resumes and Core discovers/scans renderer targets.
6. Runtime is registered for new documents and immediately evaluated.
7. Host and Runtime verify protocol compatibility.
8. Runtime finds safe semantic nodes, decorates Assistant prose, and observes
   incremental changes.
9. Manager waits for an observer-active Runtime session before reporting
   successful Start/Restart.
10. On configuration change, Manager atomically saves settings and calls
    Runtime `applyConfig()`.
11. On Disable/Manager exit, Runtime `destroy()` and local CDP disconnect occur;
    duplicated target-owned pipe peers allow Codex to continue running.

## 4. 重要设计决策

Decision: Keep the official installation untouched.

Reason: Reversibility, update compatibility, and security require avoiding
official file modification.

Alternative: Patch `app.asar`, replace resources, native injection, or network
interception.

Impact: MyCodex depends on a supported CDP launch boundary. If that boundary is
removed, it must report injection unavailable instead of patching the app.

---

Decision: Prefer private inherited CDP pipes.

Reason: A private pipe creates no listening debug socket.

Alternative: Always-on TCP debugging.

Impact: TCP remains a per-attempt, explicit-consent fallback restricted to a
random `127.0.0.1` port.

---

Decision: Duplicate private-pipe peer handles into the exact suspended target.

Reason: Closing the Manager side of Chromium's remote-debugging pipe previously
caused Codex to exit with MyCodex. Target-owned peer handles decouple lifetimes
without broad process ownership or an extra broker.

Alternative: Keep MyCodex resident forever, spawn a permanent helper/broker, or
accept that Codex closes with its launcher.

Impact: Process creation is more Windows-specific and must preserve exact
handle-list and cleanup discipline. Detach-survival coverage is mandatory.

---

Decision: Restart only a captured and revalidated exact process identity.

Reason: Process names and PIDs alone are unsafe because multiple roots, child
processes, singleton handoff, and PID reuse are possible.

Alternative: `taskkill`/process-name-wide termination, fixed PID, fixed title,
or selecting an arbitrary matching root.

Impact: PID, canonical path, start time, and tree ownership are revalidated.
Unreadable identity, multiple roots, or reuse fail closed and require user
recovery.

---

Decision: Use readiness and quiescence state instead of fixed sleeps.

Reason: Process exit, singleton resource release, renderer creation, and
Runtime readiness have nondeterministic timing.

Alternative: Wait a fixed number of seconds before launch/injection.

Impact: Restart waits for consecutive empty snapshots, detects late children
and early exits, bounds retries/timeouts, and reports success only after Runtime
is active.

---

Decision: Decorate only high-confidence Assistant prose.

Reason: Native controls and the official User bubble must retain behavior and
appearance.

Alternative: Redraw entire messages or force classification on uncertain DOM.

Impact: Per-element confidence below `0.72` remains native. Official renderer
updates may intentionally enter Safe Mode until compatibility is restored.

---

Decision: Use lightweight structural bubble grouping without a Markdown AST
dependency.

Reason: Existing renderer DOM already exposes structural blocks, while a large
parser dependency would add bundle size and mismatch risk during streaming.

Alternative: Fixed character/newline/punctuation splitting or a full Markdown
AST reconstruction.

Impact: Headings attach to following prose; related short paragraphs coalesce;
lists/quotes stay atomic; protected structures form barriers. Unsafe long
blocks remain intact rather than being corrupted.

---

Decision: Freeze grouping while streaming element structure is unchanged.

Reason: Recomputing on every text mutation caused flicker and layout jumps.

Alternative: Clear and rebuild markers on every MutationObserver callback.

Impact: Grouping updates when structure or mode changes, not merely when text
grows inside the same block set.

---

Decision: Keep Manager theme and renderer bubble theme independent.

Reason: Windows preference and the official renderer's in-app theme can differ.

Alternative: One global theme value.

Impact: Manager uses WPF semantic resources; renderer uses a bounded hybrid
theme detector and project-scoped CSS variables.

---

Decision: Use versioned atomic local configuration.

Reason: Updates and interrupted writes must not destroy user settings.

Alternative: Unversioned overwrite-in-place JSON.

Impact: Config schema 3 adds `bubbleDisplayMode`; schemas 0/1/2 migrate to
Automatic. Corrupt files are moved to bounded backups before defaults load.

---

Decision: Require real observation for visual acceptance claims.

Reason: DOM assertions, logs, and tests cannot prove actual screen rendering.

Alternative: Treat automated fixture checks as visual completion.

Impact: Automated and Computer Use evidence must be recorded separately. The
current Beta.1 Codex visual observation remains incomplete because the
available Computer Use policy forbids automating Codex/ChatGPT Desktop UI.

## 5. 最近修改记录

### Beta.1 stability remediation — 2026-07-28

Change:

- Added the formal multi-frame application icon to EXE, WPF windows, and tray.
- Added Exit/Minimize/Cancel close choice and full tray lifecycle commands.
- Decoupled Codex lifetime from MyCodex private-pipe connection lifetime.
- Added restart-tree quiescence, early-exit detection, bounded retry, renderer
  readiness, and automatic Runtime application.
- Added persisted Automatic/Whole bubble modes.
- Replaced destructive marker refresh with streaming-stable structural groups.
- Added icon, configuration migration, restart, detach, segmentation,
  streaming, and strict acceptance tests.

Reason:

- Fix the one-click restart error requiring a second Start action.
- Prevent exiting MyCodex from closing Codex.
- Meet Beta/public-release icon and lifecycle expectations.
- Prevent excessive small bubbles and streaming flicker.

Files:

- `eng/MyCodex.Version.props`
- `src/MyCodex.Core/Applications/ApplicationRestartService.cs`
- `src/MyCodex.Core/Cdp/WindowsPipeProcessLauncher.cs`
- `src/MyCodex.Core/Injection/DesktopSessionController.cs`
- `src/MyCodex.Core/Configuration/*`
- `src/MyCodex.Manager/ViewModels/MainWindowViewModel.cs`
- `src/MyCodex.Manager/Services/TrayService.cs`
- `src/MyCodex.Manager/Views/*`
- `src/MyCodex.Manager/Resources/Strings.*.xaml`
- `src/MyCodex.Runtime/src/decorator.ts`
- `src/MyCodex.Runtime/src/bubble-segmenter.ts`
- `src/MyCodex.Runtime/src/style-manager.ts`
- associated tests, docs, generated Runtime bundle, icon, and release scripts

Impact:

- Broad but related Beta lifecycle/appearance change set remains uncommitted.
- Exact Windows process and handle behavior is now a high-risk regression area.
- Config schema advanced to 3.

### Alpha.5 theme, tray, and startup continuation — 2026-07-28

Change:

- Added independent Manager themes, renderer theme detection, dual bubble
  palettes, complete tray restore behavior, and login/post-start options.

Reason:

- Provide stable theme handling and normal desktop-app lifecycle behavior.

Files:

- Manager theme/services/resources/views
- Runtime theme detector/style manager
- Core startup registration and Config schema 2 migration
- corresponding tests and documentation

Impact:

- Manager and renderer theme state must remain independent.
- Startup registration must only mutate the exact MyCodex HKCU value.

### Alpha.4 restart identity repair — 2026-07-28

Change:

- Preserved exact PID/path/start-time identity across visible-to-tray changes.
- Added tree-scoped force close, PID-reuse, multi-root, unreadable-identity, and
  natural-exit protections.

Reason:

- Avoid mistaking a tray-resident Desktop for a completed exit and avoid
  terminating unrelated processes.

Files:

- `ApplicationRestartService.cs`
- `MainWindowViewModel.cs`
- `ApplicationRestartTests.cs`
- localized status/error resources

Impact:

- Safety intentionally takes precedence over convenience when identity is
  ambiguous.

### Alpha.2 renderer compatibility and isolated acceptance — 2026-07-27

Change:

- Added current virtualized-turn matching, target ranking, serialized renderer
  refresh, calibration quarantine, state accuracy, and dual-Codex isolated
  acceptance.

Reason:

- Restore bubbles and identities against the current official renderer without
  reading real chats or risking the controlling session.

Files:

- Runtime scanner/classifier/decorator/calibration
- target discovery and session controller
- CdpProbe/VisualAcceptance tools and safety tests

Impact:

- Official DOM compatibility remains update-sensitive and must fail closed.

## 6. 当前已知问题（非常重要）

### Critical Issues

No confirmed source-level Critical issue is currently known.

The following release gate is critical operationally:

Problem:

Final real-user visual and production workflow acceptance is incomplete.

现象：

Automated gates pass, but the exact new Manager close/tray UI and a real
signed-in one-click production restart have not been visually observed end to
end in the current Beta build.

Cause:

The controlling Codex session could not be restarted safely. The available
Computer Use policy also forbids automating Codex/ChatGPT Desktop UI.

Location:

- `src/MyCodex.Manager/Views/CloseChoiceWindow.xaml`
- `src/MyCodex.Manager/Services/TrayService.cs`
- `src/MyCodex.Manager/ViewModels/MainWindowViewModel.cs`
- `src/MyCodex.Core/Applications/ApplicationRestartService.cs`
- `src/MyCodex.Core/Injection/DesktopSessionController.cs`

Suggested Fix:

Perform a disposable VM or user-controlled session acceptance using the
self-contained Beta artifact. Keep the active development controller separate.
Record actual observations independently from automated results.

### Major Issues

Problem:

The distribution is unsigned.

现象：

Windows may display SmartScreen/reputation warnings, reducing public-release
trust and usability.

Cause:

Code-signing infrastructure and release provenance are not established.

Location:

- Release process
- `.github/workflows/build.yml`
- `scripts/build-release.ps1`

Suggested Fix:

Select a signing certificate/provider, protect signing credentials in CI, sign
the EXE/archive or installer as applicable, verify signatures, and document
provenance before public release.

---

Problem:

Windows/process integration coverage is not a full real-process matrix.

现象：

Unit tests cover restart state transitions, but machine-specific singleton
handoff, Store path access, parent mapping, slow exit, and tray behavior can
still vary.

Cause:

Production restart regression tests primarily use an injected process backend;
isolated official-process tests do not reproduce every signed-in state.

Location:

- `ApplicationRestartService.cs`
- `ApplicationRestartTests.cs`
- `MyCodex.CdpProbe`
- `MyCodex.VisualAcceptance`

Suggested Fix:

Add a disposable integration fixture that can deliberately transition from a
visible window to a background/tray-like state and expose controlled exit
races. Preserve exact identity guards.

---

Problem:

Official renderer compatibility is inherently update-sensitive.

现象：

A major official Desktop DOM or CDP change can move MyCodex to Safe Mode or
make injection unavailable.

Cause:

MyCodex operates against an external application's supported Chromium/CDP and
semantic DOM surfaces.

Location:

- `src/MyCodex.Runtime/src/scanner.ts`
- `classifier.ts`
- `matcher.ts`
- `src/MyCodex.Core/Discovery/TargetDiscoveryService.cs`
- `docs/compatibility.md`

Suggested Fix:

Maintain project-authored synthetic fixtures for new structures, keep
capability probes and fail-closed thresholds, and update adapters rather than
depending on generated class names.

---

Problem:

Project memory documentation is incomplete and inconsistent.

现象：

The files mandated by the newly supplied maintenance workflow do not exist.
`docs/CODEX_HANDOFF.md` still contains stale Alpha version/HEAD/status sections
below its Beta update banner.

Cause:

The project predates the new memory-document convention, and recent work
updated historical documents incrementally.

Location:

- missing `docs/PROJECT_CONTEXT.md`
- missing `docs/TECH_STACK.md`
- missing `docs/TASK_LIST.md`
- missing `docs/DECISIONS.md`
- missing `docs/DEVELOPMENT_LOG.md`
- stale `docs/CODEX_HANDOFF.md`
- root `AGENTS.md`

Suggested Fix:

Under a separate documentation-only approved task, create the memory files from
this handoff and `ARCHITECTURE.md`, then either archive or clearly mark
`CODEX_HANDOFF.md` as historical. Reconcile the root `AGENTS.md` with the
current supplied policy.

### Minor Issues

Problem:

Git reports LF-to-CRLF conversion warnings.

现象：

`git diff --check` succeeds, but future Git writes may create noisy
line-ending diffs.

Cause:

Working-tree line endings and Git/editor normalization settings differ.

Location:

- multiple modified Markdown, C#, XAML, JSON, and TypeScript files
- `.editorconfig` / Git `core.autocrlf` policy

Suggested Fix:

Choose and document one repository normalization policy before the next commit.
Do not perform a bulk rewrite inside a feature commit.

---

Problem:

The repository does not pin a .NET SDK feature band.

现象：

`dotnet` on PATH currently finds no SDK on this machine; validation uses a
local SDK at `%LOCALAPPDATA%\MyCodexDev\dotnet8\dotnet.exe`.

Cause:

No `global.json` decision is recorded, and the machine PATH lacks the local SDK.

Location:

- repository root
- developer environment documentation

Suggested Fix:

Decide whether to add `global.json` for .NET 8.0.423 or an approved compatible
feature band, then document setup. Do not hardcode the current user's path.

---

Problem:

There is no dedicated MSI/MSIX installer project.

现象：

The current release is a self-contained ZIP. Installer-specific icons,
upgrade/uninstall behavior, shortcuts, and signing are not independently
verified.

Cause:

ZIP distribution was selected for the current Alpha/Beta scope.

Location:

- release architecture
- `scripts/build-release.ps1`

Suggested Fix:

Confirm whether ZIP remains the Phase 4 distribution. If an installer is
required, make it a planned release-engineering task rather than embedding
installer behavior into the Manager.

---

Problem:

The Windows compatibility matrix is incomplete.

现象：

Windows 10, all intended DPI values, high-contrast themes, English Windows,
custom paths, and ARM64 have not all been exercised.

Cause:

Only the current Windows 11 x64 development host was available.

Location:

- `docs/compatibility.md`
- release test plan

Suggested Fix:

Use VMs/devices and record tested versions, DPI, language, privilege, official
Desktop version, and results. Do not infer support from target framework alone.

## 7. 当前Bug排查上下文

### One-click restart error

Observed symptom:

- The old Codex process closed.
- Start then failed with `MCX-UI-ERRORSTARTDE-9CFB8D`.
- A second manual Start succeeded and applied bubbles.

Investigation result:

- The old process tree/resource state and the new renderer/Runtime readiness
  were not represented as one state-driven transaction.
- A visible window or process existence was insufficient readiness evidence.
- Singleton handoff and late child exit could make a fresh launch race.

Implemented solution:

- Stable process-tree quiescence requires consecutive empty snapshots.
- Early process exit is detected while waiting for the endpoint.
- Manager waits for an observer-active Runtime session.
- Runtime is applied before success is reported.
- Only early-exit races receive bounded retries.
- All related UI actions share a single-flight operation guard.

Do not repeat:

- Do not add a fixed multi-second sleep.
- Do not retry indefinitely.
- Do not report success when only the process/window exists.
- Do not classify renderer-not-ready as a generic secure-transport failure.

### Manager exit closed Codex

Observed symptom:

- Closing/exiting MyCodex also closed a Codex instance launched through its
  private CDP pipe.

Investigation result:

- The direct cause was private-pipe lifetime/EOF behavior, not a broad explicit
  process termination in the Manager shutdown path.

Implemented solution:

- Create the exact target suspended.
- Duplicate host-side peer pipe handles into that target as non-inheritable.
- Resume only after the duplication succeeds.
- On Manager exit, destroy Runtime and disconnect locally without terminating
  Codex.
- Verify detach survival using an isolated exact PID/path/start-time target.

Do not repeat:

- Do not solve this by keeping MyCodex permanently resident.
- Do not add a process-name watchdog.
- Do not leave inheritable handles available to unrelated descendants.
- Do not weaken exact target cleanup.

### Tray-resident restart

Previously failed/unsafe assumptions:

- Window disappearance was treated as process exit.
- A PID alone was considered enough identity.
- Broad process-name termination was considered an available fallback.

Current rule:

- Capture and revalidate PID, canonical executable path, start time, and
  process-tree ownership.
- Continue tracking a visible-to-tray root.
- Fail closed on unreadable identity, multiple roots, or PID reuse.
- Treat a natural exit between validation and termination as a safe completion.

Do not repeat:

- Never call process-name-wide termination.
- Never select the “first” matching root.
- Never reuse stale PID identity.
- Never restart the Codex session controlling development.

### Bubble over-fragmentation and streaming flicker

Observed symptom:

- Assistant output split into many small bubbles.
- Repeated marker replacement during streaming caused visible reflow risk.

Rejected approaches:

- One bubble per newline or punctuation.
- Hard character slicing through sentences/Markdown.
- Rebuilding all markers on every text mutation.
- Introducing a large Markdown parser without a demonstrated need.

Implemented solution:

- Structure-aware grouping with a 480-character target and 120/900 soft bounds.
- Headings attach to subsequent prose.
- Related short paragraphs coalesce.
- Lists and quotes remain atomic.
- Code, tables, math, Diff, tools, status, and controls are barriers.
- Grouping is frozen while element structure remains stable during streaming.

Do not repeat:

- Do not move/copy official DOM nodes to build bubbles.
- Do not split protected structures.
- Do not prioritize target length over content integrity.

### Visual acceptance

Successful evidence:

- CdpProbe detach gate verified the same isolated target PID/path/start time
  survived Manager-side pipe disconnect.
- VisualAcceptance run `aac722d7b73040bfa8e863c021c5a3bd` completed one exact
  restart and passed automated fixture, identity, Runtime, dark/light,
  destroy, and cleanup checks.
- Isolated targets/profile were removed; the controlling session remained
  alive.

Evidence gap:

- Automated DOM/state assertions are not a visual pass.
- Computer Use did not inspect Codex B pixels because the available tool policy
  prohibited automating Codex/ChatGPT Desktop.
- The current Beta Manager visual close/tray surfaces were not inspected
  because starting the new single-instance Manager would require replacing the
  active controller.

Do not repeat:

- Do not relabel automated results as Computer Use observations.
- Do not read a real profile/chat/Cookie to close the evidence gap.
- Do not restart the controlling session for acceptance.

## 8. 下一阶段开发建议

Priority 1:

任务：

Perform final manual Beta acceptance in a disposable Windows 11 session or VM:
new Manager close choices, tray restoration/exit, Codex survival after Manager
exit, one-click restart without a second Start, bubble-mode persistence, and
taskbar/Alt+Tab/Explorer icons.

原因：

This is the remaining release-critical evidence gap. It must be observed on the
actual UI and must not affect the active development controller.

Priority 2:

任务：

Review the complete uncommitted Beta.1 diff by subsystem, rerun the required
validation from the final source state, and decide with the user whether to
create small, coherent commits.

原因：

The working tree contains a broad but related 48-file change set. It should not
be committed blindly or split without understanding generated/runtime/version
dependencies.

Priority 3:

任务：

Run Windows 10/DPI/language/path/high-contrast and real signed-in official
Desktop compatibility matrices.

原因：

The current host proves only Windows 11 x64 development behavior. Public
support claims need recorded evidence.

Priority 4:

任务：

Establish signing, CI provenance, archive inspection, and the final
distribution model.

原因：

Unsigned ZIP distribution remains a major Phase 4 usability and trust risk.

Priority 5:

任务：

Create/reconcile the project memory document set and retire stale handoff
sections.

原因：

The newly required long-term maintenance workflow cannot operate efficiently
while its source documents are absent or contradictory.

Priority 6:

任务：

Add a disposable real-process restart integration fixture and improve
user-facing diagnostics for multiple roots and identity changes.

原因：

These improve regression confidence without weakening the fail-closed safety
model.

Risk reminders:

- Preserve the separation between discovery, transport, injection, matching,
  and decoration.
- Never broaden termination scope to improve convenience.
- Never read or commit real user/profile/chat/Cookie data.
- Keep Runtime host binding random, event-only, and allowlisted.
- Edit Runtime TypeScript source, then regenerate `dist`; never hand-edit it.
- Keep all three localization dictionaries in key parity.
- Keep shared version, schemas, Runtime metadata, tests, compatibility docs,
  and changelog synchronized.
- Do not begin unrelated feature work until the Beta change set is reviewed and
  the release-critical manual acceptance is resolved.

## 9. 开发环境信息

### 当前已确认环境

- OS: Microsoft Windows 11 Home Chinese, x64
- OS version/build: `10.0.26200`, build `26200`
- Shell: Windows PowerShell `5.1.26100.8875`
- Node.js: `v24.18.0`
- npm: `11.16.0`
- .NET target: `net8.0-windows`
- Local .NET SDK used successfully: `8.0.423`
- PATH `dotnet`: no SDK currently discoverable
- Architecture/RID: `win-x64`

The local SDK path is machine-specific and must not be copied into source or
configuration. Use a normal installed .NET 8 SDK on other development hosts.

### 主要依赖

Runtime development dependencies:

- TypeScript `5.7.2`
- esbuild `0.28.1`
- jsdom `25.0.1`
- `@types/node` `22.10.5`
- `@types/jsdom` `21.1.7`

.NET test dependencies:

- Microsoft.NET.Test.Sdk `18.8.1`
- xUnit `2.9.3`
- xunit.runner.visualstudio `3.1.5`

### 启动方式

Development:

```powershell
dotnet run --project .\src\MyCodex.Manager\MyCodex.Manager.csproj
```

Self-contained release:

```powershell
.\scripts\build-release.ps1
```

China-compatible download route, only when needed:

```powershell
.\scripts\build-release.ps1 -UseChinaMirrors
```

Mirror selection must stay local and must not alter committed registry/package
sources or region-neutral lockfiles.

### 必需测试方式

From the repository root:

```powershell
Push-Location .\src\MyCodex.Runtime
npm ci
npm run check
Pop-Location

dotnet build .\MyCodex.sln -c Release
dotnet test .\MyCodex.sln -c Release --no-build
```

Latest recorded final results:

- `npm ci`: passed; 67 packages; zero reported vulnerabilities
- Runtime check: 30 passed
- Release solution build: zero warnings, zero errors
- .NET tests: 91 passed, zero failed
- Self-contained `win-x64` publish and ZIP: passed
- `git diff --check`: passed; line-ending warnings only

For changes involving discovery, process lifecycle, private pipes, injection,
renderer recovery, or DOM compatibility, also run the relevant CdpProbe and
isolated VisualAcceptance workflows. Keep automated and actual visual evidence
separate.

## 10. Git状态

- Current branch: `codex/fix-pipe-runtime-compat`
- Upstream: `origin/codex/fix-pipe-runtime-compat`
- HEAD: `121a11b335fe7d57c24d14f6cf490c589c0a7bf7`
- Latest commit date: `2026-07-28T21:02:46+08:00`
- Latest commit subject: `Theme Customize`
- Working tree: dirty
- Modified tracked files before this handoff: 42
- Untracked implementation files before this handoff: 6
- This handoff adds one additional untracked documentation file:
  `docs/HANDOFF.md`
- No commit, push, pull request, tag, or public release was created for the
  Beta.1 changes.

The six pre-existing untracked implementation files are:

- `assets/mycodex.ico`
- `scripts/build-app-icon.ps1`
- `src/MyCodex.Manager/Views/CloseChoiceWindow.xaml`
- `src/MyCodex.Manager/Views/CloseChoiceWindow.xaml.cs`
- `src/MyCodex.Runtime/src/bubble-segmenter.ts`
- `src/MyCodex.Runtime/tests/bubble-segmenter.test.ts`

Recommended next Git operation:

1. Do not reset, checkout, clean, or overwrite the dirty tree.
2. Inspect `git diff`, generated Runtime/version consistency, and all untracked
   files by subsystem.
3. Rerun required validation after any further edit.
4. Complete the Priority 1 manual release acceptance.
5. Ask the user whether to commit.
6. If approved, create small coherent commits using
   `type(scope): description`; do not combine unrelated cleanup.
7. Push, PR, tag, or release only with separate explicit authorization.

## 11. 给下一次AI Agent的启动指令

```text
继续维护 MyCodex，当前处于 Phase 3 Beta 测试与发布前稳定性整改阶段。

工作区：
C:\Users\crikok\Documents\MyCodex

开始前必须：
1. 完整读取根目录 AGENTS.md。
2. 完整读取 docs/HANDOFF.md。
3. 阅读 docs/ARCHITECTURE.md。
4. 不要重新扫描整个仓库；只读取当前任务涉及的模块和必要调用链。
5. 以 docs/HANDOFF.md 的 Beta.1 状态为当前基线；docs/CODEX_HANDOFF.md 主体包含过期 Alpha 信息，仅作历史参考。

当前策略：
- 保持应用发现、CDP transport、Runtime injection、DOM matching 和 visual decoration 的模块边界。
- 优先最小修改，不做无关重构。
- 复杂修改先给出 Implementation Plan、影响文件、风险和测试方案，等待确认。
- 保留当前未提交修改，不要 reset/checkout/clean。
- 不读取真实 Codex profile、聊天、Cookie、令牌或凭据。
- 不按进程名批量终止，不放宽 PID/路径/启动时间/进程树验证。
- 不重启或关闭当前控制开发会话的 Codex。
- 不把自动化 DOM/日志结果声称为 Computer Use 视觉验收。
- 不 commit、push、创建 PR、tag 或发布，除非用户明确批准。

当前首要任务：
在不会影响控制会话的独立 Windows 会话或 VM 中，完成 Beta.1 最终人工验收：
关闭三选项、托盘恢复/退出、MyCodex 退出后 Codex 继续运行、一键重启无需第二次点击、
两种气泡模式持久化，以及任务栏/Alt+Tab/资源管理器图标。分别记录自动化结果、
实际视觉观察和未执行项。

完成验收后，审阅完整未提交 diff，运行 Runtime check、Release build 和 .NET tests，
再向用户报告并询问是否按子系统创建提交。不要自行发布。
```
