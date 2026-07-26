# Contributing

Thank you for helping MyCodex remain small, local, and resilient.

## Before opening a change

- Search existing issues and describe the user-visible problem.
- Keep Desktop discovery, injection, DOM matching, and visual decoration
  separate.
- Prefer capability detection over application-version conditionals.
- Preserve fail-closed behavior: uncertain nodes must remain unchanged.
- Never add telemetry, credential access, network interception, native
  injection, `app.asar` modification, or official-application patching.

## Local validation

Run:

```powershell
cd src\MyCodex.Runtime
npm ci
npm run check
cd ..\..
dotnet restore MyCodex.sln
dotnet build MyCodex.sln -c Release --no-restore
dotnet test MyCodex.sln -c Release --no-build
```

On a supported Windows machine, also launch the manager and verify appearance,
calibration, diagnostics, disable/cleanup, keyboard selection/copy, composer
input, scrolling, and native code/tool/status/action surfaces.

## Compatibility tests

DOM fixtures must be authored specifically for this project. Do not commit:

- a real OpenAI DOM dump or snapshot;
- OpenAI JavaScript/CSS bundles, binaries, icons, or source;
- screenshots or logs containing real conversations;
- credentials, cookies, tokens, account details, or personal paths.

A compatibility fix should include synthetic fixtures representing the old and
new structures. Vary wrappers, generated classes, attributes, depth, and layout
without adding real conversation content.

## Code style

- C#: nullable enabled, async cancellation for I/O, immutable records for data
  contracts, no swallowed unexpected exceptions.
- TypeScript: strict mode, scoped `data-mycodex-*` hooks, no dependency on
  minified class names, no page-global CSS outside the skin scope.
- Runtime mutations must be idempotent and fully reversible by `destroy()`.
- Logs and diagnostics must use an explicit allowlist.

## Pull requests

Explain:

- the behavior changed;
- privacy/security impact;
- compatibility and failure behavior;
- automated tests and manual verification performed;
- any remaining limitations.

By contributing, you agree that your contribution is licensed under the MIT
License.
