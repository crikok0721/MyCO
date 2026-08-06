# Requirements Audit — 2026-08-06

This audit records evidence and drift for the current checkout. It references stable IDs in `docs/REQUIREMENTS.md`; it is not a second requirements source.

## Evidence scope

Read in order: current user requests and attached screenshots; `docs/CONTEXT.md`; `docs/PROJECT_CONTEXT.md`; `docs/TASK_LIST.md`; `docs/DECISIONS.md`; latest `docs/DEVELOPMENT_LOG.md`; `AGENTS.md`; `CLAUDE.md`; focused settings/compatibility/release notes; historical `docs/HANDOFF.md` and `docs/archive/`; filtered Git history and diffs; focused Runtime, Core, Manager and tests. The initial checkout was clean at `da71bfb4b0bf51b6c00b8eb2f60ba92ea538e2b9`; this pass preserves the resulting implementation and records the new working-tree changes below.

## Documentation drift and conflicts

- `docs/CONTEXT.md` recorded stale base HEAD values (`fce27ee`/`705219c`), while the audited checkout is `da71bfb`; its completion claims therefore required revalidation.
- `docs/CONTEXT.md` and `docs/DEVELOPMENT_LOG.md` describe Whole, real-time apply and schema-6 independent positions as complete, while the current user supplied Automatic leakage and preview geometry regressions and explicitly says real visual acceptance was not done.
- `docs/settings.md` contained stale schema-5 wording at audit start; this pass corrected it to describe schema 7 and the schema 0..6 migration path.
- Historical archive visual passes are not evidence for the current renderer or current WPF layout.

## Confirmed invalid or no-longer-current requirements

- `CFG-LEGACY-001` absolute persisted geometry is superseded by `SLD-001` and `SLD-002`; old data remains a migration input, not a current UI/storage contract.
- Any historical “all tests passed” statement without a current command result is not a current pass for `BUB-001`, `BUB-002`, `PRE-001`, `SYNC-001`, or `VIS-001`.

## Document-only completion claims lacking current proof

The following IDs were historically described as complete but lacked current real evidence and were downgraded in the ledger: `BUB-001`, `BUB-002`, `SYNC-001`, `PRE-001`, `PRE-002`, `SLD-003`, `SLD-004`, `START-001`, `START-002`, `TRAY-001`, and `ONBOARD-001`.

## Unavailable sources

- Prior chat sessions not present in this thread, private external issue trackers, and user attachments that are not available as files cannot be searched or reconstructed. They remain unavailable rather than guessed.
- Real signed-in Codex DOM, Windows 10/11 toast shell, WPF DPI matrix, and current user profile startup behavior were not accessed during the code pass.

## Restoration batches

1. **Ledger and process:** `MEM-001`, `MEM-002`, `MEM-003`, `DOC-001`.
2. **Geometry baseline and preview:** `SLD-001`, `SLD-002`, `SLD-003`, `PRE-001`, `PRE-002`.
3. **Bubble segmentation and lifecycle:** `BUB-001`, `BUB-002`, `BUB-003`.
4. **Host/runtime application:** `SYNC-001`, `SLD-004`.
5. **Existing Windows/UI evidence gates:** `START-001`, `START-002`, `TRAY-001`, `ONBOARD-001`, `VIS-001`.

6. **Regression restoration and memory guard:** `DEF-001`, `PRE-003`,
   `BUB-004`, `BUB-005`, `MEM-004`, with defects `ERR-001` through `ERR-005`.

## Requirement Impact Check for this pass

Preserved unchanged: `SEC-001`, private CDP preference, confidence threshold, native User/protected surfaces, single-instance and precise process identity rules, atomic configuration writes, four locales, no official Codex file changes, no generated bundle hand edits, and no release/commit actions. Directly affected IDs: `MEM-001..003`, `BUB-001..003`, `SYNC-001`, `PRE-001..002`, `SLD-001..005`, `CFG-LEGACY-001`, `DOC-001`. The baseline adjustment interprets “-15px” relative to the prior +11px User Y baseline, yielding -4px. Regression tests were added before Runtime changes; the full .NET build/test and Runtime check now pass, while real visual gates remain open.

## User decisions that would otherwise block implementation

None after the user explicitly authorized one-pass autonomous implementation.
The old confirmation gate is recorded as Superseded by `MEM-004`; the remaining
gaps are verification/environment gaps, not silent design decisions.

## Recheck — 2026-08-06 / da71bfb working tree

- The four supplied screenshots were opened. They are evidence for `ERR-001`
  through `ERR-003`, not a substitute for a current real-renderer observation.
- `DEF-001` now has a reusable Core asset contract and managed-storage test;
  Manager still owns the WPF pack-resource lookup and safe fallback.
- `BUB-004` rejects stretching Markdown shells and falls back to semantic prose
  blocks. `BUB-005` uses a shrink-to-content width with a percentage cap and
  explicit auto-height/flex sizing. Runtime tests pass locally after the repair.
- `PRE-003` binds both preview roles to the same computed width cap while
  retaining role-specific brushes. WPF visual/DPI acceptance remains pending.
- `ERROR_LEDGER.md` is now the sole current defect index; no chat content or
  complete DOM is copied into it.

## Baseline v2 recheck

- `SLD-005` is implemented as geometry baseline version 2: zero deltas resolve
  to a 35px avatar, Assistant avatar Y 11px, and User avatar Y -4px.
- Schema-7 baseline-1 deltas are resolved against the prior 40px/11px values,
  converted once, persisted as baseline-2 deltas, and verified idempotent by
  configuration tests. Schema-0..6 absolute migration now uses the same
  effective-value conversion and migration-only clamping path.
- Runtime defaults, CSS fallback variables, and the Manager preview use the
  same versioned baseline constants. Real WPF/Codex pixel and slider behavior
  remains unverified under `VIS-001`.

## Recheck — 2026-08-06 / c507837 working tree

- `PRE-003`/`ERR-003`: the shared preview bubble style now includes the
  theme-aware `PreviewBorder` outline at 1 DIP. Assistant and User semantic
  brushes remain role-specific; WPF/DPI observation is still unavailable.
- `TRAY-001`/`ERR-005`: the tray event now calls `ShowBalloonTip` before the
  persisted boot claim is committed. A throwing presentation request leaves
  the claim available; exact title, localized body, packaged icon and AUMID
  wiring remain unchanged. Windows Shell display is still unverified.
- Current focused Manager tests and the full .NET suite pass (244/244); the
  Runtime check passes (58/58). These results do not upgrade `VIS-001`.

## Recheck — 2026-08-06 / per-MyCO-cycle tray semantics

- The user explicitly superseded the Windows-boot notification scope with a
  per-MyCO-process first-minimize reminder. This is recorded as `TRAY-002`;
  `TRAY-001` is retained as historical/superseded rather than deleted.
- `ERR-006` records the confirmed lifecycle mismatch. The active claim is now
  in-memory, while the legacy boot marker remains only for config round-trip
  compatibility.
- Direct minimize, taskbar minimized-state handling, and Close→Minimize-to-tray
  still converge on `UserMinimizedToTray`; background startup remains silent.

## Recheck — 2026-08-06 / reference-sized tray visual

- `ERR-007` confirms that legacy `ToolTipIcon.Info` is shell-sized and cannot
  match the supplied large blue information mark. `TRAY-003` adds a native
  `ToastGeneric` `appLogoOverride` route with the project asset
  `MyCO-notification-info.png`, while retaining BalloonTip as compatibility
  fallback.
- XML, asset-dimension, output/publish wiring, focused tray tests (32/32),
  full .NET tests (247/247), and Runtime tests (58/58) are current evidence.
  Real Windows 10/11 shell rendering remains `Implemented Unverified`.
