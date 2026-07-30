# Task List

Last updated: 2026-07-30

## Current release gate

Status: `0.3.0-beta.1` is a local unsigned candidate. The systemic Beta 1 repair
is implemented and regressed, but public testing remains blocked by the items
below.

## Blocker — public Beta 前必须解决

- [ ] In a disposable Windows account/session or VM, run the real signed-in
  end-to-end matrix: Codex stopped/running/minimized/background, MyCO
  enabled/disabled, and at least three one-click production restarts.
- [ ] In that same disposable environment, visually inspect real Codex
  conversations after calibration: different User/Assistant locations, long
  streaming replies, code/list/tool/Diff/status content, scrolling, resize,
  task switch, recalibration, disable, and re-enable.
- [ ] Verify a fresh-machine self-contained ZIP flow: first launch, dependency
  independence, configuration creation/migration, disable/recovery, removal,
  and no orphaned process/profile state.
- [ ] Establish a signed distribution route or an explicit approved plan for
  Windows SmartScreen/security warnings before inviting external testers.

## High — Beta 初期优先

- [ ] Validate Windows 10 and Windows 11 at 100%, 125%, 150%, and 200% DPI,
  including multi-monitor maximize/restore and tray restore.
- [ ] Add a user-facing privacy-safe diagnostics export and issue-report
  workflow.
- [ ] Define and run a compatibility smoke matrix for supported official
  Desktop versions and verify Safe Mode after an incompatible update.
- [ ] Document crash-recovery and restart-failure actions in the user guide.

## Medium — 可在 Beta 中继续

- [ ] Evaluate an installer/MSIX route with explicit uninstall semantics.
- [ ] Add an opt-in version check/update notification flow.
- [ ] Add a disposable real-process production-restart integration fixture.
- [ ] Expand accessibility and keyboard navigation checks for all pages.

## Low — 后续体验优化

- [ ] Refine compact-layout spacing at the minimum supported window size.
- [ ] Add optional release signing verification in local tooling.

## Completed in the current repair

- [x] Phase 3 Manager visual system: native rounded shell, deterministic rounded
  icon derivatives, solid surface hierarchy, reduced pill geometry, role-first
  home page, and compact sidebar connection dock.
- [x] Rename the product, projects, namespaces, Runtime bundle, executable, and
  release archive to MyCO with legacy data/startup/runtime compatibility.
- [x] One-transaction restart with automatic verified force fallback,
  quiescence, bounded readiness retry, and failure-state recovery.
- [x] Simplified accessible close dialog.
- [x] Canonical official ICO-to-PNG/multi-frame ICO icon pipeline.
- [x] Role-safe multi-renderer calibration and protected-surface rejection.
- [x] One identity owner per logical turn.
- [x] Three-sample structural calibration with context validation and safe
  invalidation of legacy single-sample rules.
- [x] Conversation-root boundary enforcement, composer/empty-state exclusion,
  singleton reconciliation, orphan cleanup, and streaming mutation refresh.
- [x] Stable Whole-response surface and fully rounded bubble contours.
- [x] Native taskbar minimize separated from explicit tray hiding.
- [x] Standard minimize/maximize/restore/close window controls.
- [x] Privacy-safe polling-log deduplication.
- [x] Runtime, .NET, isolated restart/theme/destroy, and Manager visual
  regression checks.
