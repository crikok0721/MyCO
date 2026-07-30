[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,

    [Parameter(Mandatory)]
    [string]$CertificatePath,

    [Parameter(Mandatory)]
    [securestring]$CertificatePassword,

    [string]$ExpectedSubject = "CN=Crikok",

    [string]$TimestampUrl = ""
)

$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Authenticode signing is supported only on Windows."
}

$resolvedPublish = [System.IO.Path]::GetFullPath($PublishDirectory)
$resolvedCertificate = [System.IO.Path]::GetFullPath($CertificatePath)

if (-not (Test-Path -LiteralPath $resolvedPublish -PathType Container)) {
    throw "Publish directory does not exist: $resolvedPublish"
}
if (-not (Test-Path -LiteralPath $resolvedCertificate -PathType Leaf)) {
    throw "Signing certificate does not exist: $resolvedCertificate"
}

$certificate = Import-PfxCertificate `
    -FilePath $resolvedCertificate `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -Password $CertificatePassword `
    -Exportable:$false

if ($certificate.Subject -ne $ExpectedSubject) {
    Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force
    throw "Signing certificate subject '$($certificate.Subject)' does not match '$ExpectedSubject'."
}

$verificationStores = @()
try {
    foreach ($storeName in @("Root", "TrustedPublisher")) {
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store(
            $storeName,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $store.Add($certificate)
        $verificationStores += $store
    }

    $ownedFiles = Get-ChildItem -LiteralPath $resolvedPublish -Recurse -File |
        Where-Object {
            $_.Name -eq "MyCO.exe" -or
            ($_.Name -like "MyCO*.dll")
        } |
        Sort-Object FullName

    if ($ownedFiles.Count -eq 0) {
        throw "No project-owned MyCO PE files were found to sign."
    }

    foreach ($file in $ownedFiles) {
        $signParameters = @{
            FilePath = $file.FullName
            Certificate = $certificate
            HashAlgorithm = "SHA256"
        }
        if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
            $signParameters.TimestampServer = $TimestampUrl
        }

        Write-Host "Signing '$($file.FullName)' with Windows Authenticode."
        $signResult = Set-AuthenticodeSignature @signParameters
        if ($null -eq $signResult.SignerCertificate) {
            throw "Authenticode signing did not attach a certificate to '$($file.FullName)'."
        }
        if ($signResult.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
            throw "Authenticode signing used an unexpected certificate for '$($file.FullName)'."
        }

        Write-Host "Verifying '$($file.FullName)' with Get-AuthenticodeSignature."
        $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "Authenticode status for '$($file.FullName)' is '$($signature.Status)'."
        }
        if ($signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
            throw "Unexpected signer certificate for '$($file.FullName)'."
        }
    }

    [pscustomobject]@{
        Subject = $certificate.Subject
        Thumbprint = $certificate.Thumbprint
        TimestampUrl = if ([string]::IsNullOrWhiteSpace($TimestampUrl)) {
            "none"
        }
        else {
            $TimestampUrl
        }
        SignedFiles = $ownedFiles.Count
        Trust = "Verified only after temporary local trust of the self-signed certificate"
    }
}
finally {
    foreach ($store in $verificationStores) {
        try {
            $store.Remove($certificate)
            $store.Close()
        }
        catch {
            Write-Warning "Could not remove temporary verification trust: $($_.Exception.Message)"
        }
    }

    Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
}
