# Task List

Last updated: 2026-08-06

## Current release gate

Status: `0.99.2` release metadata and notes are prepared for the tag-gated
signed publication; Windows acceptance gates remain open after publication.
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

## Implemented in code; verification gates still open

- [x] `DEF-001` packaged `MyCO-logo.png` default Assistant avatar through
  managed storage; custom and migrated avatars remain untouched. Fresh-profile
  and Windows visual proof remain open.
- [x] `BUB-004`/`BUB-005`/`PRE-003` Whole shell rejection, shrink-to-content
  Assistant width, and shared preview bubble cap. Runtime fixture evidence is
  current; real Codex/WPF/DPI evidence remains open. Defects are indexed as
  `ERR-001` through `ERR-004`.
- [x] `MEM-004` autonomous plan-to-delivery workflow and `ERROR_LEDGER.md`.

- [ ] Schema 7 baseline-plus-delta Assistant/User avatar and nickname offsets
  plus Assistant-only ordinary-prose maximum width. Baseline v2 (35px avatar,
  -4px User avatar Y) and migration tests are present; the real preview/Codex
  geometry remains unverified.
- [ ] Immediate all-renderer config application with latest-request ordering,
  Runtime diagnostic validation, partial-failure reporting and zero-session
  fail-closed status. Transport tests exist; current real renderer timing is
  unverified.
- [x] Three independent startup semantics, the eight-combination automated
  matrix, single-instance associated launch routing, and transactional MyCO-only
  shortcut/protocol repair with reparse and concurrent-generation protection.
- [x] Per-MyCO-process-cycle localized tray notification with invariant
  `It's MyCO!!!!!` title, native ToastGeneric 64px blue information mark,
  MyCO AUMID/icon identity shortcut, BalloonTip fallback, shared
  direct/Close→Minimize path, and presentation-before-claim handling. Real
  Windows shell display remains an acceptance gate.
- [x] Four-language onboarding title update and removal of the description,
  privacy line and green-dot elements/resources with compact responsive layout.

- [x] 0.99.2 incremental scope: filled titlebar icon, two-tone brand title and
  subtitle, per-MyCO-cycle tray minimize balloon, default-off MyCO-owned
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
- [ ] Shared preview/runtime anchor geometry, compact startup spacing, and
  unified calibration/onboarding surfaces in light and dark themes; WPF/DPI
  visual evidence is pending.
- [x] Four-language Manager/localization support including natural Japanese and
  a Japanese README.
- [x] Transactional factory reset with known-target containment, reparse-point
  refusal, startup rollback, default-logo reseeding, and onboarding replay.
- [x] Three-sample structural calibration with context validation and safe
  invalidation of legacy single-sample rules.
- [x] Conversation-root boundary enforcement, composer/empty-state exclusion,
  singleton reconciliation, orphan cleanup, and streaming mutation refresh.
- [ ] Automatic and Whole response grouping repairs and fully rounded contours;
  current Runtime fixtures cover shell fallback and adaptive width, but real
  Codex DOM evidence is pending.
- [x] User minimize paths uniformly hide to the single tray icon and preserve
  the prior Normal/Maximized state.
- [x] Standard minimize/maximize/restore/close window controls.
- [x] Privacy-safe polling-log deduplication.
- [x] Runtime, .NET, isolated restart/theme/destroy, and Manager visual
  regression checks.
