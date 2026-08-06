# Preview Bubble Parity and Tray Notification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development and superpowers:verification-before-completion when executing this plan.

**Goal:** Make the Assistant and User bubbles in the shared Manager preview visibly use the same bubble chrome and remain readable when the Assistant palette is close to the preview canvas; then restore the requested one-per-Windows-boot tray notification without duplicate popups or false claims.

**Architecture:** Keep the existing shared `PreviewBubbleStyle`, role-specific text/background semantics, and single `PreviewBubbleMaxWidth` binding. Add one shared visible outline/surface contract to the style so a low-contrast Assistant fill cannot make the bubble appear to be bare text. Do not change Runtime User-surface boundaries or persisted user-selected colors.

For the tray issue, keep the existing WinForms `NotifyIcon.ShowBalloonTip` compatibility route and project-owned `Crikok.MyCO` identity. First separate the boot-scoped claim from presentation failure, verify the process identity/icon path, and add an executable notification seam so tests can prove the event is raised once. Do not replace it with a self-drawn window or silently switch to Toast packaging requirements.

**Tech Stack:** .NET 8 WPF, XAML, C# xUnit regression tests, existing `MainWindowViewModel` preview palette bindings.

## Global Constraints

- Preserve the native User bubble boundary in Runtime; this change is Manager Preview only.
- Do not overwrite user-configured palette values or introduce a schema migration for a visual-only preview fix.
- Keep Home and Appearance on the same `ChatPreviewControl` and shared geometry primitive.
- Keep all four locales unchanged; no new user-facing text is required.
- Add a failing regression test before production XAML/C# changes.
- Do not hand-edit `src/MyCO.Runtime/dist/MyCO.runtime.js` or touch official Codex files.

## Requirement Impact Check

- Direct: `PRE-003`, `PRE-001`, `TRAY-001`, `DOC-001`.
- Regression links: `ERR-003`, `SLD-004` (width cap), `VIS-001` (real WPF/Windows evidence).
- Preserve: `SEC-001`, Runtime protected-surface rules, role-specific palette editing, current effective geometry and DPI-safe layout.
- Evidence gaps: the supplied screenshot proves the Assistant surface is visually indistinguishable from the canvas; the current tray report proves no visible shell notification but cannot distinguish shell suppression from an already-persisted boot claim without runtime instrumentation. No live WPF or Windows shell inspection was performed in this audit.

## Root-Cause Evidence

1. `ChatPreviewControl.xaml` already binds both roles to `PreviewBubbleStyle`, `PreviewBubbleMaxWidth`, the same radius converter, padding and nickname spacing. The existing static test therefore passes while the screenshot still fails.
2. `AssistantPreviewBubble.Background` is `PreviewAssistantBubble`, which resolves to the persisted `LightAssistantBubble`; `UserPreviewBubble.Background` is a separate hard-coded light surface (`#E9EEEB`).
3. Existing configurations can still contain the former light Assistant default (`#F1F3F5`), which is nearly the same as `PreviewBackground` (`#F1F5F3`). With `BorderThickness=0`, the Assistant rounded rectangle has no visible edge, while the User fill remains visible.
4. The defect is therefore a missing shared visible bubble chrome/contrast guard, not a second geometry algorithm.

## Planned Changes

### Task 1: Add failing parity regression

**Files:**

- Modify: `tests/MyCO.Tests/ManagerUiRegressionTests.cs`
- Test source: `src/MyCO.Manager/Controls/ChatPreviewControl.xaml`

- [x] Add assertions that both named bubble borders use `PreviewBubbleStyle`, the same shared `BorderBrush` binding, and a non-zero shared `BorderThickness`.
- [x] Run the focused Manager UI regression test and verify it fails against the current `BorderThickness=0` / absent shared outline.

### Task 2: Implement the minimal shared visual contract

**Files:**

- Modify: `src/MyCO.Manager/Controls/ChatPreviewControl.xaml`
- Modify: `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs` only if a dedicated preview border property is required by the binding.

- [x] Bind `PreviewBubbleStyle.BorderBrush` to the existing theme-aware `PreviewBorder` value and set one shared `BorderThickness` (1 DIP) in the style.
- [x] Keep `PreviewAssistantBubble` and `PreviewUserBubble` background/text bindings role-specific, so custom palette choices still render.
- [x] Do not change Runtime CSS, User native surfaces, width calculations, or persisted configuration.

### Task 3: Verify and document

**Files:**

- Modify: `docs/ERROR_LEDGER.md` (`ERR-003` evidence/status)
- Modify: `docs/REQUIREMENTS.md` (`PRE-003` evidence)
- Modify: `docs/CONTEXT.md`, `docs/DEVELOPMENT_LOG.md` only with actual test results.

- [x] Run the focused test and the complete .NET suite.
- [x] Run `git diff --check` and inspect the final diff.
- [x] If a disposable WPF session is not available, keep real visual status `Implemented Unverified`; do not claim screenshot parity from source tests.

### Task 4: Add a failing tray-notification regression and repair the claim/presentation boundary

**Files to inspect or modify after the preview test is red:**

- Modify: `tests/MyCO.Tests/ManagerThemeAndTrayTests.cs`
- Modify: `src/MyCO.Manager/Services/TrayService.cs` and/or a small Manager service seam for notification presentation.
- Modify: `src/MyCO.Manager/Views/MainWindow.xaml.cs` and `src/MyCO.Manager/ViewModels/MainWindowViewModel.cs` only if the claim must be committed after a successful presentation attempt.
- Inspect only: `src/MyCO.Manager/App.xaml.cs`, `src/MyCO.Core/Startup/CodexLaunchAssociationService.cs`, `src/MyCO.Manager/Services/TrayNotificationPolicy.cs`.

- [x] Add a failing behavior test that a user-initiated minimize raises exactly one notification request per current boot, while background startup and repeated minimizes do not request another.
- [x] Verify the request carries the exact title `It's MyCO!!!!!`, the four-locale `TrayMinimizedNotification` body, the packaged `Assets/MyCO.ico` route, and the existing `Crikok.MyCO` process identity.
- [x] Ensure a failed/throwing presentation attempt cannot permanently consume the boot claim; keep the one-per-boot invariant after a successful request and preserve balloon-click restore behavior.
- [x] Keep `ShowBalloonTip` as the Windows 10/11 compatibility fallback unless evidence proves the shell route is unavailable; do not add a fake popup, TCP path, elevation, or official Codex changes.
- [x] If the legacy shell still suppresses the balloon in the disposable Windows profile, record that as a real-environment evidence gap and compare a Toast implementation only as a separately authorized dependency/packaging change.

### Task 5: Verify tray behavior and update evidence

**Files:**

- Modify: `docs/ERROR_LEDGER.md` (add or update the tray presentation defect only after the failing test confirms it)
- Modify: `docs/REQUIREMENTS.md` (`TRAY-001` evidence/status)
- Modify: `docs/CONTEXT.md`, `docs/DEVELOPMENT_LOG.md` only with actual results.

- [x] Run focused tray tests, complete .NET tests, `git diff --check`, and inspect the final diff.
- [x] If real Windows shell observation is unavailable, keep `TRAY-001` visual evidence `Unverified`; automated tests cannot prove a desktop balloon appeared.

## Verification Commands

```powershell
$sdk='C:\Users\crikok\AppData\Local\MyCO-dotnet-sdk'
$env:PATH="$sdk;$env:PATH"
dotnet test .\tests\MyCO.Tests\MyCO.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ManagerUiRegressionTests"
dotnet build .\MyCO.sln -c Release --no-restore
dotnet test .\MyCO.sln -c Release --no-build
git diff --check
```

## Rollback

Revert only the preview/tray source, test, and evidence changes from this plan. Do not alter Runtime, user configuration, official Codex files, or generated release artifacts.
