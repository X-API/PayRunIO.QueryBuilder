<#
.SYNOPSIS
    Fails the build unless the MSI and its update manifest are a matched, publishable pair.

.DESCRIPTION
    Checks the four things that would otherwise reach users as a silent failure:

      1. The manifest's SHA-256 matches the built package. A mismatch makes every client
         download the update and then discard it as corrupt, with no explanation offered.
      2. The manifest's downloadUrl ends with the built package's file name. The name is what
         the client stages the download under, and Windows Installer reuses it for later
         repair and uninstall, so a wrong name breaks those operations with 1603.
      3. The manifest's sizeBytes matches the package.
      4. The advertised version matches the version compiled into the packaged executable.
         This is the important one: the client compares the manifest against the running
         assembly's version, so if the installed application reports an older version than the
         package that installed it, every launch is offered the update it already applied.

.PARAMETER MsiPath
    The built MSI.

.PARAMETER ManifestPath
    The manifest describing it. Defaults to <ApplicationId>.json beside the MSI.

.PARAMETER ExePath
    The published executable that was packaged, used for the version cross-check. Optional:
    skipped when not supplied, since the check needs the publish output rather than the MSI.

.EXAMPLE
    pwsh .\Test-ReleaseArtefacts.ps1 -MsiPath bin\Release\PayRunIO.QueryBuilder-1.2.1234.msi
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $MsiPath,

    [string] $ManifestPath,

    [string] $ApplicationId = 'query-builder',

    [string] $ExePath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $MsiPath)) {
    throw "Installer not found: $MsiPath"
}

$msi = Get-Item -LiteralPath $MsiPath

if (-not $ManifestPath) {
    $ManifestPath = Join-Path $msi.DirectoryName "$ApplicationId.json"
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "Manifest not found: $ManifestPath. Did the WriteVersionManifest target run?"
}

# A BOM here would make System.Text.Json throw on the client, and because the update check
# swallows every exception that failure would be invisible. Check the bytes, not the text.
$bytes = [System.IO.File]::ReadAllBytes($ManifestPath)

if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    throw "$ManifestPath starts with a UTF-8 BOM. The client cannot parse it."
}

$manifest = [System.Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
$entry = $manifest.applications | Where-Object { $_.id -eq $ApplicationId } | Select-Object -First 1

if (-not $entry) {
    throw "Manifest $ManifestPath contains no entry for '$ApplicationId'."
}

$failures = @()

$actualHash = (Get-FileHash -LiteralPath $msi.FullName -Algorithm SHA256).Hash.ToLowerInvariant()

if ($entry.sha256 -ne $actualHash) {
    $failures += "sha256 mismatch: manifest says $($entry.sha256), package is $actualHash"
}

if ($entry.sizeBytes -ne $msi.Length) {
    $failures += "sizeBytes mismatch: manifest says $($entry.sizeBytes), package is $($msi.Length)"
}

if (-not $entry.downloadUrl.EndsWith($msi.Name, [StringComparison]::OrdinalIgnoreCase)) {
    $failures += "downloadUrl '$($entry.downloadUrl)' does not end with the package name '$($msi.Name)'"
}

if ($entry.sha256 -eq ('0' * 64)) {
    $failures += 'sha256 is a placeholder, which disables verification on the client'
}

if ($ExePath) {
    if (-not (Test-Path -LiteralPath $ExePath)) {
        $failures += "executable not found for version cross-check: $ExePath"
    }
    else {
        $fileVersion = (Get-Item -LiteralPath $ExePath).VersionInfo.FileVersion

        # Compare as versions so that 1.2.1234 and 1.2.1234.0 are treated as equal.
        if ([version]$fileVersion -ne [version]$entry.latestVersion) {
            $failures += "version mismatch: manifest advertises $($entry.latestVersion) but the " +
                         "packaged executable reports $fileVersion. The installed application " +
                         'would be offered this update forever. Pass the same -p:PackageVersion ' +
                         'to both the application publish and the installer build.'
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Release artefact verification FAILED for $($msi.Name):"
    $failures | ForEach-Object { Write-Host "  - $_" }

    throw 'The MSI and manifest are not a publishable pair.'
}

Write-Host "Release artefacts verified: $($msi.Name)"
Write-Host "  version : $($entry.latestVersion)"
Write-Host "  sha256  : $actualHash"
Write-Host "  size    : $($msi.Length) bytes"
if ($ExePath) { Write-Host "  exe     : $((Get-Item -LiteralPath $ExePath).VersionInfo.FileVersion)" }
