# Codex project handoff

> **beta.1 stability update (2026-07-28):** the working tree now contains the
> uncommitted `0.3.0-beta.1` icon, one-click restart/readiness, independent
> Manager/Codex lifecycle, close-choice/tray, and bubble-display-mode work.
> Runtime check is 30/30, Release build has zero warnings/errors, and xUnit is
> 91/91. Isolated private-pipe detach survival passed. Visual-acceptance run
> `aac722d7b73040bfa8e863c021c5a3bd` passed all automated checks, one exact
> restart, dark/light/destroy gates, and cleanup. Computer Use inspection of
> Codex B is explicitly blocked by the tool policy and is not a visual pass.
> Do not restart the controlling Codex A to close this evidence gap.

> **alpha.5 continuation update (2026-07-28):** the current local HEAD is
> `a4f072895cf5bf21fbaa2c81eecc2351948b9eee` on the same branch. The dirty
> working tree now also contains the uncommitted `0.2.0-alpha.5` Renderer
> theme, independent Manager theme, tray lifecycle, and startup-settings work.
> Config schema is `2`; protocol/calibration remain `1`. Runtime check is
> 24/24, xUnit is 86/86, Release build is 0 warnings/errors, and the
> self-contained release script passed. Manager Computer Use acceptance and
> isolated Codex B automated acceptance are recorded in
> `docs/development-notes.md`. Codex UI Computer Use screenshots and the
> taskbar+tray screenshot remain explicitly blocked; do not reinterpret the
> older alpha.4 P0 section below as alpha.5 visual completion.
>
> The alpha.4 material is retained below as historical restart-fix context.

Last verified: 2026-07-28 (Asia/Hong_Kong)

Repository: `C:\Users\crikok\Documents\MyCodex`

Branch: `codex/fix-pipe-runtime-compat`

Upstream: `origin/codex/fix-pipe-runtime-compat`

HEAD: `a4f0728 save`

Working tree: dirty; the alpha.4 restart fix, alpha.5 continuation, and this
handoff are not committed.

This document describes the repository and working tree as inspected locally,
not only the preceding chat history.

## 1. Project goal and current phase

MyCodex is a local Windows launcher and appearance layer for the official
Codex/ChatGPT Desktop application. It launches the official Chromium app with
a controlled CDP endpoint, injects a reversible Runtime, and decorates only the
conversation presentation layer. It must preserve the official window,
navigation, composer, User bubble, code, Diff, tools, status, action controls,
credentials, profile, and network behavior.

The project is in a functional pre-release Alpha phase. The shared working-tree
version is `0.2.0-alpha.5`; protocol/config/calibration schema versions are
`1`/`2`/`1`. Phase-one architecture, localization, appearance editing, calibration,
private-pipe injection, Safe Mode, diagnostics, isolated visual acceptance, and
the latest tray-resident restart repair are implemented. It is not yet a
signed, generally released production application.

The current work item is intentionally paused after implementing the
tray-resident restart fix. No new feature should be started until the
uncommitted change set is reviewed and the production launcher restart is
confirmed from the `alpha.4` artifact in a safe user-controlled run.

## 2. Completed capabilities

- WPF Manager with first-run onboarding, Appearance, Calibration, Diagnostics,
  About/privacy pages, tray integration, single-instance behavior, and
  orderly Runtime cleanup.
- English, Simplified Chinese, and Traditional Chinese UI switching with
  immediate application and atomic persistence.
- Assistant/User nickname and imported avatar support. Avatars are validated,
  content-hash named, copied to a managed directory, and rendered as circular
  cover crops.
- Appearance controls for avatar size, shared horizontal/vertical avatar
  offsets, Assistant bubble radius/padding, message gap/max width, nickname
  visibility, and colors.
- Official application discovery for Store/MSIX and legacy installations,
  candidate scoring, stale Store path refresh, and side-by-side package
  collapsing.
- Private inherited CDP pipe launch by default, with explicit-consent random
  loopback-only TCP fallback.
- Transport-neutral target discovery, renderer capability scoring, Runtime
  injection, protocol handshake, SPA/target monitoring, and self-healing.
- TypeScript Runtime with structural/capability matching, calibration fallback,
  confidence thresholds, Safe Mode, idempotent install, hot upgrade, and
  reversible `destroy()`.
- Assistant prose/Markdown bubble decoration while preserving the official User
  bubble and excluding code/pre, Diff, tools, status, commands, buttons,
  toolbars, editors, and inputs.
- Atomic configuration recovery/migration, bounded corrupt backups, versioned
  calibration signatures, privacy-safe logging, and zero telemetry.
- Development-only dual-Codex visual acceptance tool with isolated profile,
  synthetic fixture, run marker, machine-readable state, Restart Target,
  Disable/destroy, and precise Stop/Cleanup.
- Production restart repair in the current uncommitted work: visible-to-tray
  transitions remain associated with the exact root PID/path/start time;
  force close is separately confirmed, identity checked again, limited to the
  captured tree, and fails closed on PID reuse or multiple roots.

## 3. Current architecture and key files

### Manager host

- `src/MyCodex.Manager/App.xaml.cs`: WPF startup, single-instance lifecycle,
  services, and shutdown.
- `src/MyCodex.Manager/ViewModels/MainWindowViewModel.cs`: UI state, detection,
  configuration, calibration, diagnostics, Start/Restart, transport fallback,
  enable/disable, and error handling.
- `src/MyCodex.Manager/Views/`: onboarding and main pages.
- `src/MyCodex.Manager/Resources/Strings.*.xaml`: English, Simplified Chinese,
  and Traditional Chinese resources.
- `src/MyCodex.Manager/Resources/RuntimeResourceLoader.cs`: loads the embedded
  generated Runtime.
- `src/MyCodex.Manager/Services/TrayService.cs`: Manager tray behavior.

### Core host services

- `src/MyCodex.Core/Applications/WindowsApplicationLocator.cs`: installed and
  running application discovery.
- `src/MyCodex.Core/Applications/ApplicationCandidateResolver.cs`: candidate
  normalization/scoring.
- `src/MyCodex.Core/Applications/IApplicationAdapter.cs`: official application
  adapters and launch arguments.
- `src/MyCodex.Core/Applications/ApplicationRestartService.cs`: production
  graceful close and exact-identity force-restart safety.
- `src/MyCodex.Core/Cdp/WindowsPipeProcessLauncher.cs`: restricted inherited
  pipe handles and official process launch.
- `src/MyCodex.Core/Cdp/DesktopDebugConnection.cs`,
  `PipeCdpConnection.cs`, and `CdpClient.cs`: private-pipe/TCP connections and
  CDP message handling.
- `src/MyCodex.Core/Discovery/TargetDiscoveryService.cs`: renderer selection.
- `src/MyCodex.Core/Injection/DesktopSessionController.cs`: complete managed
  Desktop session lifecycle and target monitoring.
- `src/MyCodex.Core/Injection/RuntimeInjector.cs` and
  `RuntimeTargetSession.cs`: injection, handshake, binding, config, health, and
  destroy operations.
- `src/MyCodex.Core/Configuration/`: config paths, schemas, migration, recovery,
  and validation.
- `src/MyCodex.Core/Avatars/AvatarService.cs`: safe avatar import.
- `src/MyCodex.Core/Compatibility/`: structural signatures and state machine.
- `src/MyCodex.Core/Diagnostics/PrivacySafeLogger.cs`: allowlisted local logs.
- `src/MyCodex.Core/VisualAcceptance/VisualAcceptanceSafety.cs`: isolated run
  paths, process identity, ownership, and lifecycle guards.

### Renderer Runtime

- `src/MyCodex.Runtime/src/runtime.ts`: Runtime lifecycle and orchestration.
- `scanner.ts`, `classifier.ts`, `matcher.ts`, `dom-utils.ts`: bounded
  conversation discovery and role matching.
- `decorator.ts` and `style-manager.ts`: identities, Assistant prose bubbles,
  CSS variables, and cleanup.
- `calibration.ts`, `observer.ts`, `bridge.ts`, `diagnostics.ts`: manual
  selection, incremental rescans, host events, and safe diagnostics.
- `types.ts`: shared Runtime contract and default appearance values.
- `dist/mycodex.runtime.js`: generated IIFE embedded by the Manager; do not edit
  manually.

### Development and verification

- `tools/MyCodex.CdpProbe`: isolated CDP feasibility/private-pipe probe.
- `tools/MyCodex.VisualAcceptance`: Start/Restart/Status/Disable/Record/Stop
  command host and synthetic official-renderer fixture.
- `tests/MyCodex.Tests`: .NET tests, including application discovery,
  configuration, compatibility, CDP, security boundaries, restart safety, and
  visual-acceptance ownership.
- `src/MyCodex.Runtime/tests`: synthetic DOM/Runtime tests.
- `scripts/build-release.ps1`: reproducible local checks, self-contained
  publish, documents, archive, and checksum.
- `.github/workflows/build.yml`: Windows CI build/test/audit/publish artifact.
- `eng/MyCodex.Version.props`: authoritative host version and schema metadata.

## 4. Important technical decisions and reasons

1. **Official installation remains untouched.** MyCodex launches and decorates
   through CDP; it does not patch `app.asar`, official binaries, resources, or
   native processes. This keeps updates reversible and preserves the security
   boundary.
2. **Private pipe first.** Inherited anonymous pipes avoid a listening socket.
   TCP exists only as an explicitly approved, random, `127.0.0.1` fallback.
3. **Runtime has a narrow event-only host boundary.** The random binding only
   accepts allowlisted Runtime events; the page receives no filesystem, shell,
   credential, process, or arbitrary network API.
4. **Capability and structure over fixed app versions/classes.** Current
   renderer semantics and stable structure take precedence; generated class
   names are filtered. Calibration is a fallback, not permission to decorate
   uncertain nodes.
5. **Fail closed at confidence below `0.72`.** Leaving an uncertain node
   untouched is safer than modifying an official control or native surface.
6. **Assistant prose only.** The official User bubble already exists and must
   not be redrawn. Code/pre, Diff, tools, status, command cards, buttons,
   toolbars, editors, and inputs retain official behavior and styling.
7. **Runtime changes are idempotent and reversible.** Install/ensureActive may
   run repeatedly; Disable/exit calls `destroy()` to remove all MyCodex state.
8. **Atomic versioned local configuration.** Settings live under
   `%APPDATA%\MyCodex`; invalid files are backed up and defaults restored.
   Calibration stores structure, never message text.
9. **One shared version source.** Host metadata comes from
   `eng/MyCodex.Version.props`; Runtime package/build metadata and tests must
   move with it.
10. **Restart targets exact identity, not a name.** The current repair captures
    PID, canonical executable path, start time, and tree relationship before
    closing. A tray-only root remains the same target. PID reuse, unreadable
    identity, or multiple roots aborts instead of broadening termination.
11. **Visual acceptance requires actual observation.** DOM assertions and
    automated tests are stored separately from Computer Use observations.
    Codex B uses an isolated temporary profile so Codex A and real user data are
    never restarted, copied, or read.
12. **China mirrors are an opt-in developer transport detail.** They can speed
    local downloads, but committed package versions and lockfiles remain
    region-neutral.

## 5. Prohibited or rejected approaches

- Do not terminate every process with a matching name or executable path.
- Do not weaken PID/path/start-time/profile validation or continue on uncertain
  identity.
- Do not restart or close the controlling Codex to make visual acceptance pass.
- Do not use a real Codex profile for probes or acceptance; do not read/copy
  Cookies, credentials, conversations, or account data.
- Do not patch `app.asar`, official bundles, executables, icons, or installed
  files; do not add native injection or network interception.
- Do not bind CDP TCP to LAN/all interfaces or silently fall back to TCP.
- Do not add a page-to-host request API, shell bridge, file bridge, or arbitrary
  network bridge.
- Do not force decoration when classification is uncertain.
- Do not redraw or restyle the official User bubble.
- Do not wrap native code, Diff, tool, status, action, editor, or input surfaces
  in ordinary prose bubbles.
- Do not depend on minified/generated CSS class names or commit official DOM
  snapshots and copied application assets.
- Do not report logs, DOM assertions, or passing tests as Computer Use visual
  acceptance.
- Do not hand-edit the generated Runtime bundle.
- Do not commit build artifacts, machine-specific paths, acceptance profiles,
  secrets, or real user content.
- Do not commit, push, open a PR, or publish without explicit user direction.

## 6. Unfinished work, ordered by priority

### P0 — verify and review the uncommitted `alpha.4` restart repair

1. Review `ApplicationRestartService.cs`,
   `MainWindowViewModel.StartAsync`, and `ApplicationRestartTests.cs`.
2. On a disposable/user-approved run with exactly one official root, close the
   old MyCodex Manager, launch the `alpha.4` artifact, choose Start while
   Desktop is open, allow the normal close to move it to tray, confirm the new
   force-restart prompt, and verify the official Desktop relaunches.
3. Inspect `%APPDATA%\MyCodex\logs` for the exact result and confirm
   `ApplicationAlreadyRunningException`/`MCX-UI-ERRORSTARTDE-9E513C` no longer
   occurs.
4. Do not perform this by restarting the Codex session controlling the work.
   Use a user-controlled run, a Windows VM, or another safely observable
   session.
5. If accepted, ask the user whether to commit/push; no Git publication is
   currently authorized.

### P1 — public release readiness

- Run `scripts\build-release.ps1` from a clean checkout, run `npm audit
  --audit-level=high`, inspect the archive contents, and record its SHA-256.
- Establish Windows code-signing and release provenance. The current Alpha is
  unsigned and may trigger SmartScreen reputation warnings.
- Exercise CI from the eventual commit and verify the GitHub Actions artifact.
- Perform a signed-in official Desktop release matrix covering copy/selection,
  composer input, scrolling, multiple Markdown shapes, code, Diff, tool cards,
  status, approvals, buttons, toolbar actions, Disable, exit, tray, and restart.
- Prepare the public GitHub Release only after the user explicitly authorizes
  commit/push/release.

### P2 — robustness and maintainability

- Add an integration harness for the production restart backend using a
  disposable process that can deliberately close its window into a tray-like
  background state; current restart regression coverage uses an injected fake
  process backend.
- Improve user-facing diagnostics for “multiple matching roots” and “identity
  changed” instead of relying on the generic error surface.
- Decide whether a repository `global.json` should pin the .NET 8 feature band.
  The current machine's PATH `dotnet` has no SDK; validation required the local
  SDK at `%LOCALAPPDATA%\MyCodexDev\dotnet8\dotnet.exe`.
- Reconcile historical wording in `docs/development-notes.md`: its heading now
  says `alpha.4`, while some earlier probe evidence correctly records Runtime
  `alpha.2`. Label those sections explicitly as historical to avoid confusion.
- Recheck line-ending normalization. `git diff --check` passes, but Git warns
  that several LF working-tree files will be converted to CRLF on the next Git
  write according to `.editorconfig`/Git settings.

### P3 — deferred product features

- System tray refinements, startup-on-login, and an advanced color picker.
- Broader renderer-version fixtures and future application adapters when the
  official Desktop structure changes.

## 7. Known bugs, risks, and technical debt

- **Exact production UI path not yet exercised in the controlling session.**
  It was deliberately not clicked because doing so would close the active
  Codex controller. Unit tests cover the production restart algorithm; the
  official isolated Codex B lifecycle was exercised separately.
- **Multiple matching roots fail closed.** This is intentional safety behavior,
  but users with two independent same-install roots will receive an error and
  need to close one manually. There is no unsafe “pick one” fallback.
- **Restart integration coverage is backend-fake.** Windows process snapshot,
  Toolhelp parent mapping, Store-path access, and real timing can still expose
  machine-specific behavior.
- **Official DOM compatibility is not permanent.** A large renderer update may
  cause Safe Mode until a new synthetic fixture/adapter is added.
- **Unsigned distribution.** SmartScreen/reputation prompts remain a release
  blocker for a polished public experience.
- **Local SDK discoverability.** Generic `dotnet build` currently fails on this
  machine because PATH does not contain an SDK. This is environmental, not a
  source failure.
- **Historical documentation drift.** Development notes contain evidence from
  different Alpha versions and need clearer date/version labels.
- **Line-ending warnings.** No whitespace error is present, but repeated LF to
  CRLF warnings may create noisy future diffs.

## 8. Uncommitted working-tree changes and purpose

The following list was produced from `git status`, `git diff --name-status`,
`git diff --numstat`, and direct diff inspection before writing this handoff.

| Path | State | Purpose |
| --- | --- | --- |
| `CHANGELOG.md` | modified | Adds the `0.2.0-alpha.4` tray restart fixes. |
| `README.md` | modified | Documents exact PID/path/start-time tray restart and fail-closed behavior in Chinese. |
| `README.en-US.md` | modified | English equivalent of the restart instructions. |
| `docs/architecture.md` | modified | Adds exact-identity visible/tray restart architecture. |
| `docs/compatibility.md` | modified | Updates current backend version from alpha.3 to alpha.4. |
| `docs/development-notes.md` | modified | Updates the document's current version heading to alpha.4. |
| `eng/MyCodex.Version.props` | modified | Bumps shared host version to `0.2.0-alpha.4`. |
| `src/MyCodex.Core/Applications/ApplicationRestartService.cs` | modified | Replaces window-handle-only restart detection with exact root identity, parent-tree discovery, tray tracking, safe force close, timeout, PID-reuse checks, and injectable process backend. |
| `src/MyCodex.Manager/Resources/Strings.en-US.xaml` | modified | Adds tray/background force-close wording and status. |
| `src/MyCodex.Manager/Resources/Strings.zh-CN.xaml` | modified | Simplified Chinese equivalent. |
| `src/MyCodex.Manager/Resources/Strings.zh-TW.xaml` | modified | Traditional Chinese equivalent. |
| `src/MyCodex.Manager/ViewModels/MainWindowViewModel.cs` | modified | Consumes `ApplicationCloseAttempt`, rejects uncertain identity, prompts separately, and passes the captured target to force close. |
| `src/MyCodex.Runtime/package.json` | modified | Bumps Runtime package version to alpha.4. |
| `src/MyCodex.Runtime/package-lock.json` | modified | Keeps locked root package metadata at alpha.4; dependency versions are unchanged. |
| `src/MyCodex.Runtime/dist/mycodex.runtime.js` | modified/generated | Regenerated bundle with alpha.4 version metadata; no corresponding Runtime feature-source change is part of this repair. |
| `tests/MyCodex.Tests/SecurityBoundaryTests.cs` | modified | Updates shared-version assertion to alpha.4. |
| `tests/MyCodex.Tests/ApplicationRestartTests.cs` | untracked/new | Nine regression tests for tray-only roots, visible-to-tray transition, exact force close, natural-exit race, target PID reuse, recycled parent PID, unreadable identity, multiple roots, and normal close. |
| `AGENTS.md` | untracked/new | Long-term repository rules, commands, code standards, and security constraints only. |
| `docs/CODEX_HANDOFF.md` | untracked/new | This complete cross-session working-state handoff. |

No commit, push, PR, tag, or release was created.

## 9. Latest verification results

Executed from the current dirty working tree on 2026-07-28:

### Runtime

```powershell
Push-Location .\src\MyCodex.Runtime
npm.cmd run check
Pop-Location
```

Result: passed. TypeScript lint passed; 15 tests passed, 0 failed, 0 skipped;
the Runtime bundle regenerated successfully. Package version reported
`0.2.0-alpha.4`.

### .NET build

The PATH command `dotnet build .\MyCodex.sln -c Release` failed before build
because no SDK was available on PATH. The configured local SDK was then used:

```powershell
$dotnet = "$env:LOCALAPPDATA\MyCodexDev\dotnet8\dotnet.exe"
& $dotnet build .\MyCodex.sln -c Release
```

Result: passed with .NET SDK `8.0.423`, 0 warnings, 0 errors. Manager, Core,
tests, CdpProbe, and VisualAcceptance all built in Release.

### .NET tests

```powershell
& $dotnet test .\MyCodex.sln -c Release --no-build
```

Result: 63 passed, 0 failed, 0 skipped.

### Diff validation

`git diff --check` passed before the handoff edits. Git emitted LF-to-CRLF
conversion warnings but no whitespace errors. Run it again after any future
edit.

### Most recent actual isolated visual acceptance

The final state file was re-read from
`%TEMP%\MyCodex\VisualAcceptance\8b3e2b5d33ba412bab19720e0fdce14f.final.json`.
It reports Runtime `0.2.0-alpha.4`, protocol `1`, phase `cleaned`,
`restartCount: 1`, all automated fixture/isolation checks true, and recorded
Computer Use passes for:

- the same RUN marker and complete synthetic fixture after Restart Target;
- preservation of the controlling Codex while isolated Codex B changed from
  PID `3808` to PID `51592`.

Cleanup result is `target-stopped-run-directory-removed`; the isolated profile
was removed. This acceptance proves the isolated official Desktop
Start/Restart/injection/visual/Stop lifecycle. It does not replace the pending
P0 production Manager-button check described above.

An ignored local artifact exists at
`artifacts/MyCodex-win-x64-0.2.0-alpha.4-restart-fix/MyCodex.exe`.

## 10. First task for the next Codex

Do not add a feature. First:

1. Read `AGENTS.md` and this handoff.
2. Run `git status --short --branch`, inspect the full uncommitted diff, and
   rerun `npm run check`, Release build, and .NET tests.
3. Review the exact-identity restart implementation for safety regressions.
4. Arrange the P0 disposable/user-approved production launcher verification
   without restarting the Codex session controlling the work.
5. Report the result and ask the user whether the reviewed `alpha.4` change set
   should be committed/pushed. Do not do so without approval.

## 11. Suggested new-session opening prompt

```text
继续 MyCodex 项目，但先不要新增功能。

工作区：C:\Users\crikok\Documents\MyCodex

请先完整阅读根目录 AGENTS.md 和 docs/CODEX_HANDOFF.md，然后实际执行：
1. 检查当前分支、HEAD、git status 和所有未提交 diff；
2. 重新运行 Runtime check、Release build 和 .NET tests；
3. 安全审阅 alpha.4 的托盘驻留重启修复，重点检查 PID/路径/启动时间、父子进程树、PID 复用、多根进程和自然退出竞态；
4. 按交接文档 P0 执行或安排不会关闭当前控制 Codex 的生产启动器验收；
5. 明确区分自动化结果、Computer Use 实际观察和未执行项。

不要覆盖现有未提交修改，不要读取真实 Codex profile/聊天/Cookie，不要按进程名批量终止，不要重启当前控制会话，也不要 commit、push、创建 PR 或发布，除非我明确批准。
```
