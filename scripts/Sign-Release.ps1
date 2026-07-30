[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,

    [Parameter(Mandatory)]
    [string]$CertificatePath,

    [Parameter(Mandatory)]
    [securestring]$CertificatePassword,

    [string]$ExpectedSubject = "CN=Crikok",

    [string]$TimestampUrl = "",

    [int]$SignTimeoutSeconds = 60
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

$signTool = Get-ChildItem `
    -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin" `
    -Recurse `
    -Filter "signtool.exe" `
    -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if ($null -eq $signTool) {
    throw "SignTool was not found. Install the Windows SDK signing tools."
}

$plainPassword = [System.Net.NetworkCredential]::new(
    "",
    $CertificatePassword).Password
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $resolvedCertificate,
    $plainPassword,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)

if ($certificate.Subject -ne $ExpectedSubject) {
    throw "Signing certificate subject '$($certificate.Subject)' does not match '$ExpectedSubject'."
}

function Invoke-DirectPfxSigning {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath
    )

    $arguments = @(
        "sign",
        "/f", $resolvedCertificate,
        "/p", $plainPassword,
        "/fd", "SHA256",
        "/d", "MyCO")
    if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
        $arguments += @("/tr", $TimestampUrl, "/td", "SHA256")
    }
    $arguments += $FilePath

    if ($arguments | Where-Object { $_.Contains('"') }) {
        throw "Signing arguments must not contain double quotes."
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $signTool.FullName
    $startInfo.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join " "
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        Write-Host "Signing '$FilePath' from the ephemeral PFX file."
        [void]$process.Start()

        if (-not $process.WaitForExit($SignTimeoutSeconds * 1000)) {
            $process.Kill()
            throw "Signing '$FilePath' exceeded the $SignTimeoutSeconds-second timeout."
        }

        $output = $process.StandardOutput.ReadToEnd()
        $errorOutput = $process.StandardError.ReadToEnd()
        if (-not [string]::IsNullOrWhiteSpace($output)) {
            Write-Host $output.Trim()
        }
        if (-not [string]::IsNullOrWhiteSpace($errorOutput)) {
            Write-Warning $errorOutput.Trim()
        }
        if ($process.ExitCode -ne 0) {
            throw "Signing '$FilePath' failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
    }
}

try {
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
        Invoke-DirectPfxSigning -FilePath $file.FullName

        $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
        if ($signature.Status -in @(
            [System.Management.Automation.SignatureStatus]::NotSigned,
            [System.Management.Automation.SignatureStatus]::HashMismatch,
            [System.Management.Automation.SignatureStatus]::NotSupported,
            [System.Management.Automation.SignatureStatus]::Incompatible)) {
            throw "Authenticode status for '$($file.FullName)' is '$($signature.Status)'."
        }
        if ($null -eq $signature.SignerCertificate) {
            throw "No signer certificate was embedded in '$($file.FullName)'."
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
        Trust = "Self-signed; signature identity verified without adding local trust"
    }
}
finally {
    $certificate.Dispose()
    $plainPassword = $null
}
