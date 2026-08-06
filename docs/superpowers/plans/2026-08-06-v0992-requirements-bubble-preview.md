# MyCO V0.99.2 Requirements, Bubble, and Preview Implementation Plan

> **For agentic workers:** Execute this plan task-by-task with tests before production changes. Do not modify official Codex files or generated Runtime output by hand.

**Goal:** Establish an auditable requirements ledger, restore Automatic/Whole bubble coverage, and make Manager preview geometry use the same baseline-plus-delta model as Runtime.

**Architecture:** Persist user-relative geometry deltas in a new configuration schema while keeping effective geometry in a shared Core contract. Runtime remains marker-only and reports only privacy-safe structural geometry; Manager preview binds the same effective snapshot. Bubble repair stays bounded to segmenter/decorator/cache invalidation unless invariant tests prove the existing model cannot be repaired safely.

**Tech Stack:** .NET 8/WPF, C# records and atomic JSON configuration, strict TypeScript Runtime, jsdom/node tests, xUnit, existing private CDP transport.

## Global Constraints

- Private CDP pipe remains first choice; TCP fallback remains explicit-consent only.
- DOM confidence below 0.72 remains fail-closed.
- Only confirmed Assistant ordinary prose is decorated; User, code, Diff, table, tools, status, approvals, controls, editors, and inputs remain native.
- No DOM movement, cloning, wrapping, official file modification, credential/profile access, or generated bundle hand editing.
- `install()` remains idempotent and `destroy()` removes every MyCO marker, observer, identity node, style variable, and snapshot.
- Configuration writes remain versioned, atomic, backward-compatible, and corruption-safe.
- All Manager text remains en-US, zh-CN, zh-TW, and ja-JP.

## Task 1: Requirements ledger and historical audit

**Files:**
- Create: `docs/REQUIREMENTS.md`, `docs/REQUIREMENTS_AUDIT.md`
- Modify: `AGENTS.md`, `CLAUDE.md`, `docs/CONTEXT.md`, `docs/TASK_LIST.md`, `docs/DECISIONS.md`, `docs/DEVELOPMENT_LOG.md`
- Test: `tests/MyCO.Tests/RequirementsLedgerTests.cs`

- [x] Register stable non-reusable requirement IDs and legal status values.
- [x] Record source, evidence, visual gate, supersession, and regression links without copying the ledger into the audit report.
- [x] Make `CONTEXT.md` the current status source and keep `HANDOFF.md` historical.
- [x] Add tests for unique IDs, required fields, valid statuses, and Superseded links.

## Task 2: Shared geometry baseline and schema migration

**Files:**
- Create: `src/MyCO.Core/Configuration/AppearanceGeometry.cs`
- Modify: `src/MyCO.Core/Configuration/AppConfig.cs`, `ConfigStore.cs`, `RuntimeConfigSerializer.cs`, `eng/MyCO.Version.props`
- Modify: `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`, `Views/AppearancePage.xaml`, `Controls/ChatPreviewControl.xaml`, `Controls/ChatPreviewControl.xaml.cs`, four resource dictionaries
- Modify: `src/MyCO.Runtime/src/types.ts`, `style-manager.ts`, `runtime.ts`, diagnostics validator contracts
- Test: `tests/MyCO.Tests/ConfigurationTests.cs`, `ManagerUiRegressionTests.cs`, `src/MyCO.Runtime/tests/runtime.test.ts`

- [x] Add schema 7 delta fields for avatar size, message gap, radius, padding, Assistant width, and eight role/identity offsets.
- [x] Define `effective = baseline + delta` with symmetric slider ranges and zero-centered labels.
- [x] Use versioned baseline defaults equal to the current schema-6 defaults so default visuals migrate to zero deltas.
- [x] Convert schema-6 effective values to deltas exactly once, clamp safely, preserve unrelated settings, and preserve corrupt-file recovery.
- [x] Bind preview and Runtime to the same Core geometry contract; WPF still requires real visual parity evidence.

## Task 3: Automatic and Whole repair

**Files:**
- Modify: `src/MyCO.Runtime/src/bubble-segmenter.ts`, `decorator.ts`, `observer.ts`, `runtime.ts`
- Test: `src/MyCO.Runtime/tests/bubble-segmenter.test.ts`, `runtime.test.ts`

- [x] Add failing fixtures for inline code, nested Markdown shells, protected barriers, streaming, virtualization, and mode switching; real-DOM fixtures remain pending.
- [x] Replace whole-candidate rejection that drops safe descendants with safe-span extraction around protected barriers.
- [x] Make barrier detection structural across wrapper boundaries while preserving DOM order.
- [x] Replace shallow segment cache checks with a structure/group fingerprint and explicit invalidation on config/mode changes.
- [x] Verify the covered safe blocks are marked once and protected nodes are marked zero times in Runtime fixtures.

## Task 4: Runtime application proof

**Files:**
- Modify: `src/MyCO.Core/Injection/DesktopSessionController.cs`, `RuntimeTargetSession.cs`, `RuntimeConfigSerializer.cs`, `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`, Runtime diagnostics contracts
- Test: `tests/MyCO.Tests/CdpTests.cs`, `src/MyCO.Runtime/tests/runtime.test.ts`

- [ ] Carry an ephemeral monotonic apply revision through each serialized apply (not added; existing controller gate remains the ordering boundary).
- [ ] Require each renderer to return the revision and current decoration diagnostics before reporting applied (revision echo remains pending; installed/error diagnostics are now required).
- [x] Preserve zero-session and partial-failure status; fan out to every current session.
- [x] Clear/recompute stale markers immediately and ensure the latest serialized config wins in the current Runtime contract.

## Task 5: Verification and evidence

- [x] Run Runtime check, .NET Release build/tests, `git diff --check`, static XAML/resource checks, and status.
- [x] Regenerate `dist/MyCO.runtime.js` only through `npm.cmd run check`.
- [ ] Run isolated synthetic visual acceptance for dark/light, narrow/wide, streaming, protected surfaces, mode switching, destroy, and DPI where available.
- [x] Update requirement statuses and documentation only with fresh evidence; leave real signed-in Windows visual gates explicitly unverified when unavailable.
