# MyCO UI, Identity, Japanese, and Factory Reset Implementation Plan

> Execution is already approved by the user. Work directly in the current dirty `main` worktree, preserve unrelated changes, and do not commit, push, open a PR, or release.

**Goal:** Unify Manager preview geometry, fix Codex multi-unit identity ownership, add complete Japanese support, refresh calibration/onboarding UI, and add a safe transactional factory reset.

**Architecture:** Keep Manager-only presentation changes inside existing WPF views/themes/localization, make the Runtime identity change at the unit ownership boundary without weakening classification, and implement reset filesystem safety in Core with Manager orchestration. Preserve private-pipe, exact-process, fail-closed, and privacy boundaries.

**Tech Stack:** .NET 8 WPF, C#, XAML resource dictionaries, TypeScript runtime, Vitest, xUnit.

---

## Task 1: Runtime multi-unit identity ownership

**Files:**
- Modify: `src/MyCO.Runtime/src/runtime.ts`
- Modify: `src/MyCO.Runtime/src/runtime.test.ts`

1. Change the existing fixture expectation so each valid Assistant unit in one turn owns one identity, while tool/native/non-prose rows own none.
2. Run the focused Runtime tests and confirm the new assertion fails for the current one-owner-per-turn behavior.
3. Move ownership from turn-anchor uniqueness to eligible unit-key uniqueness while retaining classifier gates and idempotent decoration.
4. Add coverage for repeated refresh, streaming replacement, and destroy cleanup; rerun the Runtime suite.

## Task 2: Shared preview geometry and compact Manager layouts

**Files:**
- Modify: `src/MyCO.Manager/Controls/ChatPreviewControl.xaml`
- Modify: `src/MyCO.Manager/Controls/ChatPreviewControl.xaml.cs`
- Modify: `src/MyCO.Manager/Views/SettingsPage.xaml`
- Modify: `src/MyCO.Manager/Views/CalibrationPage.xaml`
- Modify: `src/MyCO.Manager/Views/OnboardingWindow.xaml`
- Modify: `src/MyCO.Manager/Views/OnboardingWindow.xaml.cs`
- Modify: `src/MyCO.Manager/Themes/SharedStyles.xaml` only if existing tokens/styles cannot express the required states
- Modify: corresponding Manager XAML contract tests

1. Add failing structural tests for one shared preview bubble style, X/Y padding, startup spacing/alignment, compact calibration rows, and onboarding shell.
2. Add a testable `PreviewBubblePadding` and apply one shared geometry style to both roles while retaining semantic brushes.
3. Recompose startup, calibration, and onboarding layouts using existing design tokens, solid surfaces, restrained separators, and responsive grids.
4. Verify both theme dictionaries continue to provide all referenced resources.

## Task 3: Complete Japanese localization

**Files:**
- Modify: `src/MyCO.Core/Localization/LanguageCodes.cs`
- Modify: `src/MyCO.Manager/Services/LocalizationService.cs`
- Add: `src/MyCO.Manager/Resources/Strings.ja-JP.xaml`
- Modify: localized XAML resources and any discovered user-visible hard-coded strings
- Add: `README.ja-JP.md`
- Modify: `README.md`, `README.en-US.md`, language-support documentation
- Modify: localization/configuration tests

1. Add failing tests requiring `ja-JP`, exact resource-key parity, persistence/reload, formatting, and Japanese font fallbacks.
2. Add the language code and selector entry, then create a natural Japanese resource dictionary with exact key parity.
3. Migrate task-relevant hard-coded visible strings into resources and update documentation language entry points.
4. Run focused localization/configuration tests.

## Task 4: Transactional factory reset

**Files:**
- Add: `src/MyCO.Core/Configuration/FactoryResetService.cs`
- Add/modify: Core tests using temporary directories only
- Modify: `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs`
- Modify: `src/MyCO.Manager/Views/SettingsPage.xaml`
- Add: `src/MyCO.Manager/Views/ResetConfirmationWindow.xaml`
- Add: `src/MyCO.Manager/Views/ResetConfirmationWindow.xaml.cs`
- Modify: Manager tests

1. Add failing tests for known-target-only staging, root preservation, cancellation/no-op, successful commit, rollback, path containment, and reparse-point refusal.
2. Implement a same-root staging transaction over only config, calibration, managed avatars, logs, and backups; never touch the legacy migration source.
3. Add a localized confirmation window with Cancel as the default/safe action and a visibly red destructive action.
4. Orchestrate Runtime disable, startup-value removal, filesystem transaction, default config recreation, packaged-logo seeding, ViewModel/theme reload, and immediate onboarding. Roll back on failure and never report false success.

## Task 5: Documentation and verification

**Files:**
- Modify: `docs/architecture.md`
- Modify: `docs/DECISIONS.md`
- Modify: `docs/HANDOFF.md`
- Modify: `docs/DEVELOPMENT_LOG.md`
- Modify: `docs/CONTEXT.md`, `AGENTS.md`, `CLAUDE.md` only for current language-support statements

1. Record the narrow D-006 ownership change, four-language support, reset transaction, and the actual validation state.
2. Run `npm.cmd ci`, `npm.cmd run check`, Release build/test using the available SDK, and `git diff --check`.
3. Keep automated verification separate from unperformed real desktop visual acceptance; do not terminate current user processes to package or preview.
