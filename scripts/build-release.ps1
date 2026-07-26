[CmdletBinding()]
param(
    [switch]$UseChinaMirrors,
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64"
)

# Reproduces CI locally: runtime checks, .NET build/test, self-contained publish, and zip.
$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$runtimeRoot = Join-Path $repoRoot "src\MyCodex.Runtime"
$managerProject = Join-Path $repoRoot "src\MyCodex.Manager\MyCodex.Manager.csproj"
$solution = Join-Path $repoRoot "MyCodex.sln"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishRoot = Join-Path $artifactsRoot "MyCodex-win-x64"
$archivePath = Join-Path $artifactsRoot "MyCodex-win-x64.zip"

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
}
finally {
    Pop-Location
}

$restoreArguments = @("restore", $solution)
$ridRestoreArguments = @("restore", $managerProject, "-r", $RuntimeIdentifier)
if ($UseChinaMirrors) {
    $nugetSource = "https://repo.huaweicloud.com/repository/nuget/v3/index.json"
    $restoreArguments += @("--source", $nugetSource)
    $ridRestoreArguments += @("--source", $nugetSource)
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

Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "README.en-US.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "PRIVACY.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "SECURITY.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "CHANGELOG.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "CONTRIBUTING.md") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $publishRoot
# Ship architecture, compatibility, privacy, and contribution guidance with the app.
Copy-Item -LiteralPath (Join-Path $repoRoot "docs") -Destination $publishRoot -Recurse

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $archivePath `
    -CompressionLevel Optimal

$hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
[pscustomobject]@{
    Executable = Join-Path $publishRoot "MyCodex.exe"
    Archive = $archivePath
    Sha256 = $hash.Hash.ToLowerInvariant()
} | Format-List
