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

<#
.SYNOPSIS
    Compares two version strings, treating omitted trailing fields as zero.
.DESCRIPTION
    [version]"1.1.256" -ne [version]"1.1.256.0" in .NET, because an unspecified Revision is -1
    rather than 0. Both forms describe the same release here - a three field PackageVersion
    yields a three field FileVersion but a four field managed AssemblyVersion - so the missing
    fields are normalised before comparing.
#>
function Test-VersionsEqual {
    param([string] $Left, [string] $Right)

    function Expand([string] $value) {
        $parsed = [version]$value

        return [version]::new(
            [Math]::Max($parsed.Major, 0),
            [Math]::Max($parsed.Minor, 0),
            [Math]::Max($parsed.Build, 0),
            [Math]::Max($parsed.Revision, 0))
    }

    return (Expand $Left) -eq (Expand $Right)
}

<#
.SYNOPSIS
    Gets the managed assembly version of a published application.
.DESCRIPTION
    Reads the version the update client will see at run time, which is the *managed* assembly
    version rather than the Win32 FileVersion resource.

    A self contained single file publish bundles the managed assembly inside a native host, so
    AssemblyName.GetAssemblyName cannot read the published .exe and the publish directory holds
    no .dll to read instead. The pre-bundle assembly one directory above the publish folder is
    therefore the reliable source, and it carries exactly the value the client will report.

    Falls back to the Win32 FileVersion only when no managed assembly can be found: that value
    may be short by a field, which the caller's comparison normalises.
#>
function Get-AssemblyVersion {
    param([string] $Path)

    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $publishDirectory = Split-Path -Parent $Path

    $candidates = @(
        # Sibling managed assembly: a framework dependent or non bundled publish.
        (Join-Path $publishDirectory "$fileName.dll"),
        # Pre-bundle assembly: the single file case, one level above the publish directory.
        (Join-Path (Split-Path -Parent $publishDirectory) "$fileName.dll"))

    foreach ($candidate in $candidates) {
        if (-not (Test-Path -LiteralPath $candidate)) {
            continue
        }

        try {
            return [System.Reflection.AssemblyName]::GetAssemblyName($candidate).Version.ToString()
        }
        catch {
            continue
        }
    }

    if (Test-Path -LiteralPath $Path) {
        $fileVersion = (Get-Item -LiteralPath $Path).VersionInfo.FileVersion

        if ($fileVersion) {
            Write-Host "  (no managed assembly found; falling back to FileVersion $fileVersion)"

            return $fileVersion
        }
    }

    return $null
}

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
        # Read the managed assembly version, because that is what the update client compares:
        # it calls Assembly.GetEntryAssembly().GetName().Version. The Win32 FileVersion resource
        # is NOT interchangeable - a three field PackageVersion such as 1.1.256 produces a
        # FileVersion of "1.1.256" while the managed AssemblyVersion is always padded to four
        # fields as 1.1.256.0. Comparing against FileVersion therefore failed a build whose
        # artefacts were perfectly consistent.
        $assemblyVersion = Get-AssemblyVersion -Path $ExePath

        if (-not $assemblyVersion) {
            $failures += "could not read the assembly version from $ExePath"
        }
        elseif (-not (Test-VersionsEqual $assemblyVersion $entry.latestVersion)) {
            $failures += "version mismatch: manifest advertises $($entry.latestVersion) but the " +
                         "packaged assembly reports $assemblyVersion. The installed application " +
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
if ($assemblyVersion) { Write-Host "  assembly: $assemblyVersion" }
