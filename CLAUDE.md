# MyCO — Claude Code instructions

Windows 10/11 x64 .NET 8 WPF manager + embedded TypeScript Runtime for official
Codex/ChatGPT Desktop renderer. Local, reversible, privacy-safe, fail-closed.

## Build & test

```powershell
Push-Location .\src\MyCO.Runtime; npm ci; npm run check; Pop-Location
dotnet build .\MyCO.sln -c Release
dotnet test .\MyCO.sln -c Release --no-build
.\scripts\build-release.ps1          # self-contained ZIP + signing
```

Add `-UseChinaMirrors` for slow mainland routes; never commit regional sources.

## Source of truth (read order)

1. `CLAUDE.md` (this file)
2. `docs/CONTEXT.md` — current state, blockers, validation
3. `docs/architecture.md` — architecture, data flow
4. `docs/DECISIONS.md` — design decisions
5. `docs/TASK_LIST.md` — current priorities
6. `docs/TECH_STACK.md` — technical stack details

Historical: `docs/archive/CODEX_HANDOFF.md`, `docs/archive/development-notes.md`

## Key rules (non-negotiable)

- **Private CDP pipe first.** TCP only with per-attempt user consent, random port, 127.0.0.1.
- **No patching.** Don't modify app.asar, official binaries, profiles, cookies, credentials, or traffic.
- **Fail closed.** DOM confidence < 0.72 = no decoration. PID/path/start-time mismatch = no kill.
- **Assistant prose only.** Preserve native User bubble, code, pre, Diff, tools, status, buttons, editors, inputs.
- **Idempotent install, complete destroy.** install() can re-run; destroy() removes all MyCO state.
- **Restart exact identity.** Revalidate PID, path, start time, tree ownership. Never terminate by process name.
- **Atomic config.** Versioned, backward-compatible, corrupt → backup + restore defaults.
- **No commit/push/PR/release without explicit request.**
- **Launch-path handoff:** every completed task must include a separate final-report
  section stating where the modified program can be launched and how to launch it.
  If no executable was produced or the executable was not verified, say
  `not produced` / `not verified` explicitly; never point to an older or unrelated
  binary.
- **All UI text in 4 languages:** en-US, zh-CN, zh-TW, ja-JP.
- **User-facing wording:** ordinary UI text must use the current locale and
  must not expose development terms such as Runtime, pipe, lifecycle, schema,
  or signature. Keep MyCO, Codex, ChatGPT, Windows, GitHub, version numbers,
  and necessary file extensions when they are proper names or actionable
  labels. Translate technical concepts into user language such as appearance,
  connection, startup, settings, or sign-in information.
- **Diagnostics wording:** an abbreviation may remain on a diagnostics page
  only when the current locale explains it. Audit every touched page and new
  resource key for mixed technical terminology; list unrelated existing
  violations before expanding the change scope.

## Key files

| Area | Path |
|------|------|
| WPF UI | `src/MyCO.Manager/` |
| Core services | `src/MyCO.Core/` |
| TypeScript Runtime | `src/MyCO.Runtime/src/` |
| Generated bundle | `src/MyCO.Runtime/dist/MyCO.runtime.js` (do not hand-edit) |
| Version/protocol | `eng/MyCO.Version.props` |
| Tests (.NET) | `tests/MyCO.Tests/` |
| Tests (Runtime) | `src/MyCO.Runtime/tests/` |
| Release script | `scripts/build-release.ps1` |
| Icon source | `assets/MyCO-source.ico` |
| CI | `.github/workflows/build.yml` |
