# Code signing policy

MyCO release binaries are Authenticode-signed as `CN=Crikok` with SHA-256.
Each tag-driven GitHub Actions release creates an
ephemeral signing key on its isolated Windows runner and deletes the private
key after packaging.

## Trust model

The certificate is self-signed. It proves that files carrying the documented
certificate thumbprint were signed with the corresponding MyCO release key,
but it is not rooted in the Windows public trust store. Windows and SmartScreen
can therefore still show an unknown-publisher or reputation warning.
The autonomous release path deliberately has no external timestamp dependency;
signature validity is not extended beyond the certificate lifetime.

Do not install the certificate into a Windows trust store merely to suppress a
warning. Verify the certificate fingerprint, archive SHA-256 file, GitHub
artifact attestation, source tag, and release workflow instead.

## Release controls

- The `.pfx` private key and password exist only on the release runner and are
  never committed or retained as workflow artifacts.
- Each release has a different certificate. The public certificate shipped
  with that release is the authority for that release only.
- Release tags must exactly match the version in `eng/MyCO.Version.props`.
- Project-owned PE files are signed before the ZIP is created.
- Windows PowerShell's Authenticode provider performs the default signing; the
  release does not depend on a separate SignTool process.
- The signing script temporarily trusts the public certificate only inside the
  build account for cryptographic verification, then removes that trust.
- A missing, mismatched, or invalid signature fails the release.
- Each release publishes the public certificate, archive checksum, SPDX SBOM,
  and GitHub build-provenance attestation.

## Verification

Compare the certificate thumbprint displayed by:

```powershell
(Get-AuthenticodeSignature .\MyCO.exe).SignerCertificate |
  Select-Object Subject, Thumbprint, NotBefore, NotAfter
```

Then verify the archive:

```powershell
Get-FileHash .\MyCO-win-x64.zip -Algorithm SHA256
gh attestation verify .\MyCO-win-x64.zip -R crikok0721/MyCO
```

Read the expected thumbprint from
`MyCO-self-signed-code-signing.cer` in the same release and compare it with the
signer embedded in `MyCO.exe`. The GitHub provenance attestation binds both to
the tagged workflow build.
