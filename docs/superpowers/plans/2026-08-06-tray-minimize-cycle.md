# Tray Minimize Notification Per-MyCO-Cycle Plan

## Goal

Show the branded tray reminder on the first user-initiated minimize in each
running MyCO process, regardless of whether the action came from the title-bar
minimize/taskbar path or from the close-choice dialog's “Minimize to tray” path.
Do not show it for background startup or later minimizes in the same process.

## Evidence and root cause

- `MainWindow.PrepareForBackground(userInitiated: true)` is reached by the
  direct minimize path, the WPF minimized-state path, and
  `CloseChoice.MinimizeToTray`; all raise `UserMinimizedToTray`.
- `MainWindowViewModel.TryPresentTrayMinimizeNotification` currently compares
  a persisted `TrayMinimizeNotificationBootId` with
  `SystemBootIdentity.Current()`. That enforces one notification per Windows
  boot, not one per MyCO startup cycle.
- The persisted claim is loaded on every MyCO start, so restarting MyCO in the
  same Windows session suppresses the first minimize of the new process.
- `TrayService` already owns one `NotifyIcon`, uses the packaged
  `Assets/MyCO.ico`, the `Crikok.MyCO` AUMID, the exact title
  `It's MyCO!!!!!`, and the four-language body. The existing BalloonTip route
  matches the supplied Windows visual reference and remains the minimum-change
  compatibility route.

## Requirement impact check

- Supersede `TRAY-001`'s Windows-boot scope with new `TRAY-002`'s per-MyCO-cycle
  scope; retain title, icon, localization, no-duplicate and no-fake-window
  requirements.
- Add `ERR-006` for the wrong lifecycle scope; retain `ERR-005` for failed
  presentation not consuming the current-cycle claim.
- Preserve `SEC-001`, single-instance forwarding, AUMID/icon ownership,
  current-user-only behavior, native tray restore/exit actions, and all official
  Codex boundaries.

## Implementation sequence

1. Add failing tests before production changes:
   - first direct minimize presents once;
   - close-choice minimize presents once;
   - background startup does not present;
   - second minimize in the same MyCO process does not present;
   - a new MyCO process starts with a fresh claim even when a legacy persisted
     boot ID exists;
   - a throwing presentation leaves the current-cycle claim available;
   - exact title, localized body, packaged icon and AUMID remain wired.
2. Replace the runtime gate with an in-memory per-process boolean owned by the
   current `MainWindowViewModel`/tray lifecycle. Keep the legacy serialized
   field for backward-compatible config round-tripping, but stop using it to
   suppress a new MyCO process and stop writing notification claims on tray
   display.
3. Keep both user paths on the single `UserMinimizedToTray` event and guard
   against duplicate event delivery without changing restore/exit behavior.
4. Update the requirement ledger, error ledger, architecture, decision, task,
   context and development-log entries with actual evidence.
5. Rebuild and verify the self-contained release package after all source and
   documentation changes.

## Notification route decision

Keep WinForms `NotifyIcon.ShowBalloonTip` with the existing packaged icon and
`Crikok.MyCO` identity. It matches the supplied reference without adding
Windows App SDK/Toast packaging or a self-drawn popup. If a disposable Windows
10/11 Shell observation still suppresses the request, record that as an
environment gate and propose Toast as a separately authorized follow-up; do not
silently expand this change.

## Verification

```powershell
Push-Location .\src\MyCO.Runtime; npm.cmd ci; npm.cmd run check; Pop-Location
dotnet build .\MyCO.sln -c Release
dotnet test .\MyCO.sln -c Release --no-build
git diff --check
.\scripts\build-release.ps1
```

Real Windows Shell display and DPI/theme observations remain explicitly
`Unverified` unless performed in an isolated profile.
