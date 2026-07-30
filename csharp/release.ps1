<#
.SYNOPSIS
    Builds the Better RDP installer and (optionally) publishes it as a GitHub release.

.DESCRIPTION
    Three steps: dotnet publish -> vpk pack -> vpk upload github.

    `vpk pack` produces, in artifacts/releases/:
      BetterRdp-win-Setup.exe    the installer users download (per-user, no admin)
      BetterRdp-<ver>-full.nupkg the payload
      BetterRdp-<ver>-delta.nupkg only the files that changed since the last release
      RELEASES-win                the feed the installed app reads to find updates

    Keep artifacts/releases/ between runs. vpk needs the previous release there to
    build a delta; without it every update is a full ~250 MB download.

.EXAMPLE
    ./release.ps1 -Version 0.0.5
    ./release.ps1 -Version 0.0.5 -LocalOnly     # build the installer, don't publish
#>
[CmdletBinding()]
param(
    # Semver for this release. Becomes the assembly version and the v-prefixed git tag.
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    # Build the installer but skip the GitHub upload.
    [switch]$LocalOnly,

    # SHA-1 thumbprint of an Authenticode cert in the current user's Personal store.
    # Unrelated to BETTER_RDP_SIGN_THUMBPRINT, which signs the generated .rdp files.
    # Omit to ship unsigned (users get a one-time SmartScreen warning).
    [string]$SignThumbprint,

    # Defaults to the gh CLI's token. Needs repo scope.
    [string]$GitHubToken = $(if (Get-Command gh -ErrorAction SilentlyContinue) { gh auth token 2>$null })
)

$ErrorActionPreference = 'Stop'

$repoUrl    = 'https://github.com/Hasakev/BetterRDP'
$rid        = 'win-x64'
$appProject = Join-Path $PSScriptRoot 'src\BetterRdp.App\BetterRdp.App.csproj'
$icon       = Join-Path $PSScriptRoot 'src\BetterRdp.App\Assets\AppIcon.ico'
$publishDir = Join-Path $PSScriptRoot "..\artifacts\publish\$rid"
$releaseDir = Join-Path $PSScriptRoot '..\artifacts\releases'

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw "vpk not found. Install it with: dotnet tool install -g vpk"
}

# Stale files in the publish dir get packed into the release, so start clean.
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "==> Publishing $Version ($rid)" -ForegroundColor Cyan
dotnet publish $appProject `
    -c Release `
    -r $rid `
    -p:Platform=x64 `
    -p:Version=$Version `
    --self-contained true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# PDBs are ~40 MB of debug symbols that no user needs and that churn every build,
# inflating the delta package.
Get-ChildItem $publishDir -Filter *.pdb -Recurse | Remove-Item -Force

# artifacts/ is gitignored, so on a fresh clone the previous release isn't on disk and
# vpk would emit a full-only release — making every client re-download ~100 MB. Pull the
# last published release back down first so it has something to diff against.
if (-not $LocalOnly -and $GitHubToken) {
    Write-Host "==> Fetching previous release for delta" -ForegroundColor Cyan
    vpk download github --repoUrl $repoUrl --token $GitHubToken --outputDir $releaseDir
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not fetch previous release; this one will be full-only."
    }
}

Write-Host "==> Packing installer" -ForegroundColor Cyan
$packArgs = @(
    'pack'
    '--packId', 'BetterRdp'
    '--packVersion', $Version
    '--packDir', $publishDir
    '--packTitle', 'Better RDP'
    '--packAuthors', 'Hasakev'
    '--mainExe', 'BetterRdp.App.exe'
    '--icon', $icon
    '--outputDir', $releaseDir
)
if ($SignThumbprint) {
    $packArgs += '--signParams'
    $packArgs += "/fd sha256 /sha1 $SignThumbprint /tr http://timestamp.digicert.com /td sha256"
}
vpk @packArgs
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

if ($LocalOnly) {
    Write-Host "==> Built $releaseDir (not published)" -ForegroundColor Green
    return
}

if (-not $GitHubToken) {
    throw "No GitHub token. Run 'gh auth login' or pass -GitHubToken."
}

Write-Host "==> Publishing release v$Version to GitHub" -ForegroundColor Cyan
vpk upload github `
    --repoUrl $repoUrl `
    --token $GitHubToken `
    --outputDir $releaseDir `
    --tag "v$Version" `
    --releaseName "Better RDP $Version" `
    --merge `
    --publish
if ($LASTEXITCODE -ne 0) { throw "vpk upload failed" }

Write-Host "==> Released v$Version. Installed clients pick it up on next launch." -ForegroundColor Green
