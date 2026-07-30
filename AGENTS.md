# MyCO repository instructions

## Scope

MyCO is a Windows 10/11 x64, .NET 8 WPF manager plus an embedded
TypeScript runtime. Keep application discovery, CDP transport, runtime
injection, DOM matching, and visual decoration separated.

## Required validation

From the repository root:

```powershell
Push-Location .\src\MyCO.Runtime
npm ci
npm run check
Pop-Location

dotnet build .\MyCO.sln -c Release
dotnet test .\MyCO.sln -c Release --no-build
```

Use `scripts\build-release.ps1` for a self-contained release build. Add
`-UseChinaMirrors` when the normal npm or NuGet route is too slow in mainland
China. Mirror selection must remain local to the build; do not commit
region-specific registry or package-source settings.

`src\MyCO.Runtime\dist\MyCO.runtime.js` is generated and embedded by the
WPF project. Change TypeScript under `src\MyCO.Runtime\src`, then regenerate
the bundle with `npm run check`; do not hand-edit the bundle.

When changing the shared version or protocol, keep
`eng\MyCO.Version.props`, Runtime package metadata, the generated bundle,
tests, compatibility documentation, and changelog consistent.

## Code and localization rules

- C#: nullable enabled, async cancellation for I/O, immutable records for data
  contracts, and no swallowed unexpected exceptions.
- TypeScript: strict mode, project-scoped `data-myco-*` hooks, no dependence
  on generated/minified class names, and no page-global CSS outside the
  MyCO scope.
- Runtime installation must be idempotent. `destroy()` must remove every
  MyCO observer, listener, attribute, element, style, and CSS variable.
- Any user-facing Manager text must be added consistently to English,
  Simplified Chinese, and Traditional Chinese resource dictionaries.
- Configuration writes remain atomic and backward compatible with versioned
  config/calibration schemas.
- Logs and diagnostics use explicit allowlists and must not contain message
  text, prompts, code, credentials, cookies, account details, or unnecessary
  paths.

## Non-negotiable security and compatibility boundaries

- Prefer inherited private CDP pipes. TCP fallback requires explicit
  per-attempt user consent, a random port, and `127.0.0.1` binding only.
- Do not modify `app.asar`, official binaries, or official installation files;
  do not inject native code, intercept traffic, or read credentials/cookies.
- Keep the Runtime-to-Host binding random, event-only, and allowlisted. It must
  not expose shell, filesystem, process, credential, or arbitrary network
  capabilities.
- DOM uncertainty fails closed. Decorate Assistant prose only; preserve the
  official User bubble and native code, pre, Diff, tool, status, toolbar,
  button, editor, and input surfaces.
- Production restart may close only a target whose PID, executable path, start
  time, and process-tree ownership were captured and revalidated. PID reuse,
  unreadable identity, or multiple matching roots must fail closed. Never use
  a process-name-wide termination fallback.
- Development visual acceptance must use the isolated profile under
  `%TEMP%\MyCO\VisualAcceptance\<run-id>`, close only the exact owned target
  PID, and leave the controlling Codex and all user sessions alive.
- Automated tests, DOM assertions, and logs do not prove visual correctness.
  For a visual acceptance claim, use Computer Use to inspect the isolated
  official Desktop window and record actual observations separately.
- Never copy, read, delete, or commit a real Codex profile, chat, DOM snapshot,
  Cookie, credential, personal path, or machine-specific acceptance artifact.

## Git and release hygiene

- Preserve unrelated user changes in a dirty worktree.
- Do not commit, push, create a pull request, or publish a release without an
  explicit user request.
- Keep generated build outputs and local acceptance artifacts out of Git.
- Release archives should come from GitHub Actions or
  `scripts\build-release.ps1`, include the required documents, and publish a
  SHA-256 checksum. Public releases should be signed when signing
  infrastructure is available.
