# Technical Stack

Last updated: 2026-08-06

## Host

- .NET 8, Windows x64
- WPF, nullable C#, async cancellation for I/O
- `NotifyIcon` tray lifecycle and `WindowChrome` native window-state integration
- Windows SDK for .NET targeting pack (`Windows.UI.Notifications`) for the
  native ToastGeneric route; NotifyIcon remains the compatibility fallback
- `System.Text.Json` and immutable records for configuration/contracts

Projects:

- `src/MyCO.Manager`: WPF UI and application lifecycle
- `src/MyCO.Core`: discovery, restart, CDP, injection, configuration, logging
- `tests/MyCO.Tests`: xUnit regression tests
- `tools/MyCO.CdpProbe`: development CDP compatibility probe
- `tools/MyCO.VisualAcceptance`: isolated official-Desktop acceptance host

## Renderer Runtime

- TypeScript strict mode
- esbuild-generated IIFE bundle
- Node test runner plus jsdom fixtures
- Source: `src/MyCO.Runtime/src`
- Generated embedded bundle: `src/MyCO.Runtime/dist/MyCO.runtime.js`

The generated bundle is never edited by hand. Run `npm run check` after Runtime
source changes.

## Build and validation

```powershell
Push-Location .\src\MyCO.Runtime
npm ci
npm run check
Pop-Location

dotnet build .\MyCO.sln -c Release
dotnet test .\MyCO.sln -c Release --no-build
```

Release:

```powershell
.\scripts\build-release.ps1
```

Use `-UseChinaMirrors` only for the local build process when needed. Do not
commit regional registry or NuGet source settings.

## Packaging

- Self-contained `win-x64` publish
- ZIP distribution with required documents
- Multi-frame Windows ICO and WPF PNG generated from
  `assets/MyCO-source.ico`
- SHA-256 emitted by the release script
