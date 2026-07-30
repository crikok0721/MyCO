# Release process

MyCO uses Semantic Versioning. The repository version is defined once in
`eng/MyCO.Version.props`; the release tag is the same value prefixed with `v`.

## Release gate

1. Runtime lint, tests, and bundle pass.
2. npm high-severity audit passes.
3. .NET restore, Release build, and tests pass.
4. A self-contained `win-x64` publish succeeds.
5. The isolated release runner creates an ephemeral certificate and every
   project-owned PE file is Authenticode-signed as `CN=Crikok`.
6. The signed files pass temporary local-chain verification.
7. The package contains release documents, an SPDX SBOM, and file hashes.
8. The archive SHA-256 file and GitHub provenance attestation are generated.
9. The tag matches the repository version exactly.
10. A disposable Windows environment completes launch, configuration,
    disable/recovery, removal, and visual smoke checks.

## Build locally

Unsigned validation build:

```powershell
.\scripts\build-release.ps1 -GenerateSbom
```

Signed build:

```powershell
$password = Read-Host "Certificate password" -AsSecureString
.\scripts\build-release.ps1 `
  -GenerateSbom `
  -SigningCertificatePath C:\secure\MyCO-release-signing.pfx `
  -SigningCertificatePassword $password `
  -PublicSigningCertificatePath security\MyCO-self-signed-code-signing.cer
```

Never place a `.pfx`, password, or unredacted signing log in the repository,
release package, issue, or workflow artifact. Official GitHub releases create
an independent certificate on the release runner; local certificates are only
for reproducing the signing mechanics.
