[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PfxPath,

    [Parameter(Mandatory)]
    [string]$PublicCertificatePath,

    [Parameter(Mandatory)]
    [securestring]$Password,

    [string]$Subject = "CN=Crikok",

    [int]$ValidYears = 3
)

$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Release certificate generation is supported only on Windows."
}

$resolvedPfxPath = [System.IO.Path]::GetFullPath($PfxPath)
$resolvedPublicPath = [System.IO.Path]::GetFullPath($PublicCertificatePath)

foreach ($outputPath in @($resolvedPfxPath, $resolvedPublicPath)) {
    $parent = Split-Path -Parent $outputPath
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    if (Test-Path -LiteralPath $outputPath) {
        throw "Refusing to overwrite existing certificate output: $outputPath"
    }
}

$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears($ValidYears)

try {
    Export-PfxCertificate `
        -Cert $certificate `
        -FilePath $resolvedPfxPath `
        -Password $Password `
        -CryptoAlgorithmOption AES256_SHA256 | Out-Null

    Export-Certificate `
        -Cert $certificate `
        -FilePath $resolvedPublicPath `
        -Type CERT | Out-Null

    [pscustomobject]@{
        Subject = $certificate.Subject
        Thumbprint = $certificate.Thumbprint
        NotBefore = $certificate.NotBefore.ToUniversalTime()
        NotAfter = $certificate.NotAfter.ToUniversalTime()
        PublicCertificate = $resolvedPublicPath
        PrivateCertificate = $resolvedPfxPath
        Trust = "Self-signed; not publicly trusted by Windows"
    }
}
finally {
    Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force
}
