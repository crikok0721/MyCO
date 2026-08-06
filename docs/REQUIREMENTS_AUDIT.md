# Requirements Audit — 2026-08-06

This audit records evidence and drift for the current checkout. It references stable IDs in `docs/REQUIREMENTS.md`; it is not a second requirements source.

## Evidence scope

Read in order: current user requests and attached screenshots; `docs/CONTEXT.md`; `docs/PROJECT_CONTEXT.md`; `docs/TASK_LIST.md`; `docs/DECISIONS.md`; latest `docs/DEVELOPMENT_LOG.md`; `AGENTS.md`; `CLAUDE.md`; focused settings/compatibility/release notes; historical `docs/HANDOFF.md` and `docs/archive/`; filtered Git history and diffs; focused Runtime, Core, Manager and tests. Current checkout was verified at `705219c86ac38cc94a61544247854d8568b2fe5c` with a clean tree before this implementation began.

## Documentation drift and conflicts

- `docs/CONTEXT.md` records base HEAD `fce27ee`, while the checkout is `705219c`; its completion claims therefore required revalidation.
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

## Requirement Impact Check for this pass

Preserved unchanged: `SEC-001`, private CDP preference, confidence threshold, native User/protected surfaces, single-instance and precise process identity rules, atomic configuration writes, four locales, no official Codex file changes, no generated bundle hand edits, and no release/commit actions. Directly affected IDs: `MEM-001..003`, `BUB-001..003`, `SYNC-001`, `PRE-001..002`, `SLD-001..005`, `CFG-LEGACY-001`, `DOC-001`. The baseline adjustment interprets “-15px” relative to the prior +11px User Y baseline, yielding -4px. Regression tests were added before Runtime changes; schema migration and preview changes remain subject to the full .NET build and real visual gates.

## User decisions that would otherwise block implementation

None after the user explicitly authorized one-pass autonomous implementation. The remaining gaps are verification/environment gaps, not silent design decisions.

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
