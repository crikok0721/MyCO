[CmdletBinding()]
param(
    [switch]$UseChinaMirrors,
    [switch]$GenerateSbom,
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$ArtifactsRoot,
    [string]$SigningCertificatePath,
    [securestring]$SigningCertificatePassword,
    [string]$PublicSigningCertificatePath
)

# Reproduces CI locally: runtime checks, .NET build/test, self-contained publish, and zip.
$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
[xml]$versionFile = Get-Content -LiteralPath (Join-Path $repoRoot "eng\MyCO.Version.props") -Encoding UTF8
$version = [string]$versionFile.Project.PropertyGroup.MyCOVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Canonical MyCO version is missing from eng\MyCO.Version.props."
}
$runtimeRoot = Join-Path $repoRoot "src\MyCO.Runtime"
$managerProject = Join-Path $repoRoot "src\MyCO.Manager\MyCO.Manager.csproj"
$updaterProject = Join-Path $repoRoot "src\MyCO.Updater\MyCO.Updater.csproj"
$solution = Join-Path $repoRoot "MyCO.sln"
$artifactsRoot = if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    Join-Path $repoRoot "artifacts"
}
else {
    [System.IO.Path]::GetFullPath($ArtifactsRoot)
}
$artifactName = "MyCO-$RuntimeIdentifier"
$publishRoot = Join-Path $artifactsRoot $artifactName
$archivePath = Join-Path $artifactsRoot "$artifactName.zip"
$archiveHashPath = "$archivePath.sha256"

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$npmCommand = Get-Command npm.cmd -ErrorAction SilentlyContinue
if ($null -eq $npmCommand) {
    $npm = (Get-Command npm -ErrorAction Stop).Source
}
else {
    $npm = $npmCommand.Source
}

Push-Location $runtimeRoot
try {
    # China mirrors are opt-in and apply only to this process.
    if ($UseChinaMirrors) {
        & $npm ci --registry=https://registry.npmmirror.com
    }
    else {
        & $npm ci
    }
    if ($LASTEXITCODE -ne 0) {
        throw "npm ci failed with exit code $LASTEXITCODE."
    }
    & $npm run check
    if ($LASTEXITCODE -ne 0) {
        throw "Runtime validation failed with exit code $LASTEXITCODE."
    }
    & $npm audit --audit-level=high
    if ($LASTEXITCODE -ne 0) {
        throw "Runtime dependency audit failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$restoreArguments = @("restore", $solution)
$ridRestoreArguments = @("restore", $managerProject, "-r", $RuntimeIdentifier)
$ridUpdaterRestoreArguments = @("restore", $updaterProject, "-r", $RuntimeIdentifier)
if ($UseChinaMirrors) {
    $nugetSource = "https://repo.huaweicloud.com/repository/nuget/v3/index.json"
    $restoreArguments += @("--source", $nugetSource)
    $ridRestoreArguments += @("--source", $nugetSource)
    $ridUpdaterRestoreArguments += @("--source", $nugetSource)
}

& $dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) {
    throw "Solution restore failed with exit code $LASTEXITCODE."
}
& $dotnet @ridRestoreArguments
if ($LASTEXITCODE -ne 0) {
    throw "Runtime-specific restore failed with exit code $LASTEXITCODE."
}

& $dotnet build $solution -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Solution build failed with exit code $LASTEXITCODE."
}
& $dotnet test $solution -c $Configuration --no-build --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Solution tests failed with exit code $LASTEXITCODE."
}

if (Test-Path -LiteralPath $publishRoot) {
    # Resolve and verify the path before recursively deleting old publish output.
    $resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsRoot)
    $resolvedPublish = [System.IO.Path]::GetFullPath($publishRoot)
    if (-not $resolvedPublish.StartsWith(
        $resolvedArtifacts + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a publish path outside the artifacts directory."
    }
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

& $dotnet publish $managerProject `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    --no-restore `
    -o $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}
& $dotnet @ridUpdaterRestoreArguments
if ($LASTEXITCODE -ne 0) {
    throw "Updater runtime-specific restore failed with exit code $LASTEXITCODE."
}

# The updater is a separate process so it can replace the running Manager.
# Publish it into the same package without touching the Manager executable.
& $dotnet publish $updaterProject `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    --no-restore `
    -o $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "Updater publish failed with exit code $LASTEXITCODE."
}

if (-not [string]::IsNullOrWhiteSpace($SigningCertificatePath)) {
    if ($null -eq $SigningCertificatePassword) {
        throw "SigningCertificatePassword is required when SigningCertificatePath is supplied."
    }

    & (Join-Path $PSScriptRoot "Sign-Release.ps1") `
        -PublishDirectory $publishRoot `
        -CertificatePath $SigningCertificatePath `
        -CertificatePassword $SigningCertificatePassword
}

Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "README.en-US.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "README.ja-JP.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "PRIVACY.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "SECURITY.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "CHANGELOG.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "CONTRIBUTING.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $publishRoot
# Ship architecture, compatibility, privacy, and contribution guidance with the app.
Copy-Item -LiteralPath (Join-Path $repoRoot "docs") -Destination $publishRoot -Recurse

if (-not [string]::IsNullOrWhiteSpace($PublicSigningCertificatePath)) {
    Copy-Item -LiteralPath $PublicSigningCertificatePath `
        -Destination (Join-Path $publishRoot "MyCO-self-signed-code-signing.cer")
}

if ($GenerateSbom) {
    $sbomToolRoot = Join-Path ([System.IO.Path]::GetTempPath()) `
        "MyCO\sbom-tool\4.1.5"
    if (-not (Test-Path -LiteralPath (Join-Path $sbomToolRoot "sbom-tool.exe"))) {
        & $dotnet tool install `
            --tool-path $sbomToolRoot `
            Microsoft.Sbom.DotNetTool `
            --version 4.1.5
        if ($LASTEXITCODE -ne 0) {
            throw "SBOM tool installation failed with exit code $LASTEXITCODE."
        }
    }

    & (Join-Path $sbomToolRoot "sbom-tool.exe") generate `
        -b $publishRoot `
        -bc $repoRoot `
        -pn "MyCO" `
        -pv $version `
        -ps "Crikok" `
        -nsb "https://github.com/crikok0721/MyCO"
    if ($LASTEXITCODE -ne 0) {
        throw "SBOM generation failed with exit code $LASTEXITCODE."
    }
}

$fileHashManifest = Join-Path $publishRoot "SHA256SUMS.txt"
$hashLines = Get-ChildItem -LiteralPath $publishRoot -Recurse -File |
    Where-Object { $_.FullName -ne $fileHashManifest } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($publishRoot.Length).
            TrimStart([System.IO.Path]::DirectorySeparatorChar).
            Replace("\", "/")
        $fileHashResult = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        $fileHash = $fileHashResult.Hash.ToLowerInvariant()
        "$fileHash  $relative"
    }
[System.IO.File]::WriteAllLines(
    $fileHashManifest,
    $hashLines,
    [System.Text.UTF8Encoding]::new($false))

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $archivePath `
    -CompressionLevel Optimal

$hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
[System.IO.File]::WriteAllText(
    $archiveHashPath,
    "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($archivePath))`n",
    [System.Text.UTF8Encoding]::new($false))
[pscustomobject]@{
    Executable = Join-Path $publishRoot "MyCO.exe"
    Archive = $archivePath
    Sha256 = $hash.Hash.ToLowerInvariant()
    Sha256File = $archiveHashPath
} | Format-List
