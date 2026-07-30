# Dual-Codex visual acceptance

This workflow is **development/acceptance-only**. It keeps the controlling
Codex session (Codex A) alive while launching an isolated official Desktop
instance (Codex B), injecting the built Runtime, and displaying synthetic test
content for an actual Computer Use inspection.

It never reads or copies the normal Codex profile. Each run uses:

```text
%TEMP%\MyCO\VisualAcceptance\<run-id>\profile
```

The controller uses private CDP pipes. A target can be closed only after its
run-id, PID, executable path, process start time, and isolated profile metadata
all match. Identity uncertainty fails closed; there is no process-name-wide
shutdown fallback.

## Prerequisites

- Windows 10/11 x64;
- .NET 8 SDK;
- Node.js and npm;
- an installed official Codex Desktop;
- run these commands from the repository root.

## Build, start, inspect, restart, destroy, and stop

```powershell
Push-Location .\src\MyCO.Runtime
npm ci
npm run check
Pop-Location

dotnet build .\MyCO.sln -c Release
dotnet test .\MyCO.sln -c Release --no-build

$va = ".\tools\MyCO.VisualAcceptance\bin\Release\net8.0-windows\MyCO.VisualAcceptance.dll"
$start = (& dotnet $va start `
  --runtime ".\src\MyCO.Runtime\dist\MyCO.runtime.js") |
  Out-String |
  ConvertFrom-Json
if (-not $start.passed) {
  throw "Visual acceptance did not become ready: $($start.state.errorCode)"
}
$run = $start.state.runId

dotnet $va status --run-id $run

# Keep Codex A open. If the active Computer Use policy permits Codex Desktop
# inspection, select only the window whose HWND belongs to start.state.targetPid.
# If policy forbids it, record the check as blocked; never substitute DOM checks.

# Start is dark. Switch the same renderer without restarting MyCO or Codex B.
dotnet $va theme --run-id $run --mode light
dotnet $va status --run-id $run
# Inspect/capture light, then prove the transition is reversible.
dotnet $va theme --run-id $run --mode dark
dotnet $va status --run-id $run

dotnet $va restart --run-id $run
dotnet $va status --run-id $run

# Inspect the restarted Codex B with Computer Use, then verify destroy cleanup.
dotnet $va disable --run-id $run
dotnet $va status --run-id $run

dotnet $va stop --run-id $run
dotnet $va status --run-id $run
```

Pass `--executable "<exact official path>"` to `start` only when automatic
official-install discovery is ambiguous. Do not pass a user profile path.

Use `record` to persist actual Computer Use observations separately from DOM
assertions:

```powershell
dotnet $va record --run-id $run `
  --check assistant-prose-bubble `
  --result pass `
  --note "Computer Use observed the assistant prose bubble"
```

Valid results are `pass`, `fail`, and `blocked`. A DOM assertion is stored in
`automatedChecks`; it must never be recorded as a visual observation.

For beta.1, record separate Computer Use checks for `codex-dark`,
`codex-light`, `codex-light-to-dark`, `user-bubble-native`,
`native-surfaces`, and `no-layout-jump`. The `theme` command changes only the
synthetic renderer's visible theme, waits for the debounced detector, and adds
automated theme-marker/singleton/readability checks. It is not a visual pass.

Manager/tray/startup screenshots are a separate real-Windows pass against the
self-contained `MyCO.exe`. Store evidence under:

```text
artifacts\visual-acceptance\<run-id>\
```

Expected names are `codex-dark.png`, `codex-light.png`,
`manager-dark.png`, `manager-light.png`, `manager-system.png`,
`minimized-to-tray.png`, `restored-from-tray.png`, and
`startup-settings.png`. Do not use this fixture, an HTML render, a DOM dump, or
an automated assertion as a substitute for those screenshots.

## Fixture coverage

The official `app://` renderer displays synthetic content only:

- a native User bubble;
- Assistant Markdown, multiple paragraphs, and long text;
- `pre`/`code`;
- a tool card;
- a Diff panel and status line;
- buttons and an action toolbar.

The sticky header contains `RUN <run-id>` so Computer Use can distinguish
Codex B from Codex A.

## State and cleanup

While a run is active:

```text
%TEMP%\MyCO\VisualAcceptance\<run-id>\state.json
```

After a successful default `stop`, the run directory and isolated profile are
removed. A small final state remains at:

```text
%TEMP%\MyCO\VisualAcceptance\<run-id>.final.json
```

It records the run phase, exact target identity, Runtime/protocol versions,
automated checks, explicitly recorded visual checks, restart count, cleanup
result, and error code. Use `stop --preserve-artifacts` only while diagnosing a
development failure; clean that run afterward.

If the official Desktop ever rejects a second isolated instance, do not close
Codex A. Run the same command chain inside a Windows VM and keep the blocked
host state/error as evidence.
