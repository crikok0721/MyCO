# Task List

Last updated: 2026-08-02

## Current release gate

Status: `0.99.2` incremental development is implemented locally and is not
released; build and Windows acceptance gates remain open.
The existing `0.99.1` signed-release route remains documented below; public
readiness is still blocked by the environment-dependent items below.

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
- [x] Establish an autonomous signed distribution route with explicit
  self-signed trust and SmartScreen limitations, SHA-256 checksums, SPDX SBOM,
  and GitHub build provenance.

## High — Beta 初期优先

- [ ] Validate Windows 10 and Windows 11 at 100%, 125%, 150%, and 200% DPI,
  including multi-monitor maximize/restore and tray restore.
- [ ] Add a user-facing privacy-safe diagnostics export and issue-report
  workflow.
- [ ] Audit legacy user-visible status and diagnostics strings for untranslated
  development terms such as Runtime/pipe/signature; expand only after a
  separate wording plan and four-language review.
- [ ] Define and run a compatibility smoke matrix for supported official
  Desktop versions and verify Safe Mode after an incompatible update.
- [ ] Document crash-recovery and restart-failure actions in the user guide.

## Medium — 可在 Beta 中继续

- [ ] Evaluate an installer/MSIX route with explicit uninstall semantics.
- [x] Add an opt-in version check/update flow with official-release validation
  and a project-owned external updater; run the isolated replacement matrix.
- [ ] Add a disposable real-process production-restart integration fixture.
- [ ] Expand accessibility and keyboard navigation checks for all pages.

## Low — 后续体验优化

- [ ] Refine compact-layout spacing at the minimum supported window size.
- [ ] Add optional release signing verification in local tooling.

## Completed in the current repair

- [x] 0.99.2 incremental scope: filled titlebar icon, two-tone brand title and
  subtitle, one-per-Windows-boot tray minimize balloon, default-off MyCO-owned
  Codex associations, official release update flow, reset confirmation layout,
  and the long-content Runtime shell regression fix.

- [x] 0.99.2 Manager UI repair: shared user-style preview bubbles, exact
  four-language factory-reset copy, borderless semantic content cards, the
  requested Home edit-entry removal, and a local rounded tray renderer.
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
- [x] One identity owner per legal message unit, including multiple Assistant
  progress/final units inside one logical turn.
- [x] Shared preview bubble geometry, compact startup spacing, and unified
  calibration/onboarding surfaces in light and dark themes.
- [x] Four-language Manager/localization support including natural Japanese and
  a Japanese README.
- [x] Transactional factory reset with known-target containment, reparse-point
  refusal, startup rollback, default-logo reseeding, and onboarding replay.
- [x] Three-sample structural calibration with context validation and safe
  invalidation of legacy single-sample rules.
- [x] Conversation-root boundary enforcement, composer/empty-state exclusion,
  singleton reconciliation, orphan cleanup, and streaming mutation refresh.
- [x] Stable Whole-response surface and fully rounded bubble contours.
- [x] User minimize paths uniformly hide to the single tray icon and preserve
  the prior Normal/Maximized state.
- [x] Standard minimize/maximize/restore/close window controls.
- [x] Privacy-safe polling-log deduplication.
- [x] Runtime, .NET, isolated restart/theme/destroy, and Manager visual
  regression checks.
