# Security policy

## Supported versions

MyCodex is currently an alpha project. Security fixes target the latest
published version only.

| Version | Supported |
| --- | --- |
| 0.2.x alpha | Yes |
| Older versions | No |

## Reporting a vulnerability

Do not post sensitive vulnerability details, tokens, logs containing private
data, or proof-of-concept conversation data in a public issue. Use the
repository's private GitHub Security Advisory reporting flow when available.

Include:

- affected MyCodex version and official Desktop version;
- Windows version and architecture;
- minimal reproduction steps;
- expected and observed behavior;
- privacy-reviewed diagnostics;
- impact and any suggested mitigation.

## Security boundaries

- CDP uses inherited private pipes by default and creates no listening socket.
- Loopback TCP is used only after an explicit per-attempt user confirmation,
  with a random port bound only to `127.0.0.1`.
- The runtime-to-host Binding has a random per-session name and accepts only
  whitelisted event types.
- Injected JavaScript cannot request shell execution, file access, process
  launch, or arbitrary network activity through MyCodex.
- MyCodex does not read cookies, intercept network traffic, modify `app.asar`,
  patch official binaries, or inject native code.
- Avatar imports are size/dimension-limited, checked by file signature, copied
  under a content-hash name, and served only from the managed non-reparse path.
- Low-confidence DOM matches fail closed: unknown nodes remain unmodified.

## Supply-chain guidance

Release archives should be produced by the checked-in GitHub Actions workflow
or from a clean checkout using `scripts/build-release.ps1`. Lockfiles are
committed. China mirror support changes download endpoints for a single local
build only; package names and locked versions remain unchanged.
