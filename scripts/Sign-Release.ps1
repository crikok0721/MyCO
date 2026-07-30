[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,

    [Parameter(Mandatory)]
    [string]$CertificatePath,

    [Parameter(Mandatory)]
    [securestring]$CertificatePassword,

    [string]$ExpectedSubject = "CN=Crikok",

    [string]$TimestampUrl = "http://timestamp.acs.microsoft.com",

    [int]$SignToolTimeoutSeconds = 90
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

function Invoke-SignTool {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$Operation
    )

    $standardOutput = [System.IO.Path]::GetTempFileName()
    $standardError = [System.IO.Path]::GetTempFileName()
    try {
        $process = Start-Process `
            -FilePath $signTool.FullName `
            -ArgumentList $Arguments `
            -RedirectStandardOutput $standardOutput `
            -RedirectStandardError $standardError `
            -NoNewWindow `
            -PassThru

        if (-not $process.WaitForExit($SignToolTimeoutSeconds * 1000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            throw "$Operation exceeded the $SignToolTimeoutSeconds-second timeout."
        }

        $output = Get-Content -LiteralPath $standardOutput -Raw -ErrorAction SilentlyContinue
        $errorOutput = Get-Content -LiteralPath $standardError -Raw -ErrorAction SilentlyContinue
        if (-not [string]::IsNullOrWhiteSpace($output)) {
            Write-Host $output.Trim()
        }
        if (-not [string]::IsNullOrWhiteSpace($errorOutput)) {
            Write-Warning $errorOutput.Trim()
        }
        if ($process.ExitCode -ne 0) {
            throw "$Operation failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        Remove-Item -LiteralPath $standardOutput, $standardError `
            -Force -ErrorAction SilentlyContinue
    }
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
        Invoke-SignTool `
            -Operation "Signing '$($file.FullName)'" `
            -Arguments @(
                "sign",
                "/sha1", $certificate.Thumbprint,
                "/s", "My",
                "/fd", "SHA256",
                "/tr", $TimestampUrl,
                "/td", "SHA256",
                "/d", "MyCO",
                $file.FullName)

        Invoke-SignTool `
            -Operation "Verifying '$($file.FullName)'" `
            -Arguments @("verify", "/pa", "/tw", "/all", $file.FullName)

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
        TimestampUrl = $TimestampUrl
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
