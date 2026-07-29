# Technical Stack

Last updated: 2026-07-29

## Host

- .NET 8, Windows x64
- WPF, nullable C#, async cancellation for I/O
- `NotifyIcon` tray lifecycle and `WindowChrome` native window-state integration
- `System.Text.Json` and immutable records for configuration/contracts

Projects:

- `src/MyCodex.Manager`: WPF UI and application lifecycle
- `src/MyCodex.Core`: discovery, restart, CDP, injection, configuration, logging
- `tests/MyCodex.Tests`: xUnit regression tests
- `tools/MyCodex.CdpProbe`: development CDP compatibility probe
- `tools/MyCodex.VisualAcceptance`: isolated official-Desktop acceptance host

## Renderer Runtime

- TypeScript strict mode
- esbuild-generated IIFE bundle
- Node test runner plus jsdom fixtures
- Source: `src/MyCodex.Runtime/src`
- Generated embedded bundle: `src/MyCodex.Runtime/dist/mycodex.runtime.js`

The generated bundle is never edited by hand. Run `npm run check` after Runtime
source changes.

## Build and validation

```powershell
Push-Location .\src\MyCodex.Runtime
npm ci
npm run check
Pop-Location

dotnet build .\MyCodex.sln -c Release
dotnet test .\MyCodex.sln -c Release --no-build
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
  `assets/mycodex-source.ico`
- SHA-256 emitted by the release script
