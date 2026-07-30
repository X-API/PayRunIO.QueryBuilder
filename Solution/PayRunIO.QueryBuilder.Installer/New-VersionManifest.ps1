<#
.SYNOPSIS
    Writes the update manifest for a built MSI, ready to be published alongside it.

.DESCRIPTION
    Reads the version from the package, computes its SHA-256 and size, and writes a manifest
    named after the application identifier - for example query-builder.json. The manifest and
    the MSI are then collected as build artefacts and uploaded to the bucket by hand.

    The script only writes a file. It performs no upload, needs no AWS credentials and has no
    dependency on the AWS CLI, so it runs on any build agent.

    One manifest per application is deliberate: releases are published manually, and a shared
    file would have to be downloaded, merged and re-uploaded every time, with the accompanying
    risk of reverting another utility's entry.

.PARAMETER MsiPath
    The built MSI the manifest will describe.

.PARAMETER ApplicationId
    The manifest application identifier. This is also the manifest file name and the key the
    client requests, so it must match the ApplicationId constant compiled into the application.

.PARAMETER Version
    The four part version to advertise. Defaults to the MSI's ProductVersion padded to four
    fields, so the package stays the single source of truth.

.PARAMETER OutputDirectory
    Where to write the manifest. Defaults to the directory holding the MSI, so the pair are
    picked up by a single artefact rule.

.PARAMETER Mandatory
    Advertise the release as mandatory, removing the user's option to skip or postpone.

.EXAMPLE
    pwsh .\New-VersionManifest.ps1 -MsiPath bin\Release\PayRunIO.QueryBuilder-1.1.255.msi

.EXAMPLE
    pwsh .\New-VersionManifest.ps1 -MsiPath bin\Release\app.msi -MinimumSupportedVersion 1.1.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $MsiPath,

    [string] $ApplicationId = 'query-builder',

    [string] $ApplicationName = 'PayRun.io Query Builder',

    [string] $Version,

    [string] $OutputDirectory,

    [string] $BaseUrl = 'https://prio-utilities.s3.eu-west-2.amazonaws.com',

    [string] $ReleaseNotesUrl = 'https://developer.payrun.io/docs/downloads/index.html',

    [string] $MinimumSupportedVersion = '1.0.0.0',

    [switch] $Mandatory
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $MsiPath)) {
    throw "Installer not found: $MsiPath"
}

$msi = Get-Item -LiteralPath $MsiPath

if (-not $OutputDirectory) {
    $OutputDirectory = $msi.DirectoryName
}

if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

if (-not $Version) {
    # Read ProductVersion from the package rather than parsing the file name, then pad to four
    # fields: the manifest advertises four part versions while Windows Installer carries three.
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.GetType().InvokeMember(
        'OpenDatabase', 'InvokeMethod', $null, $installer, @($msi.FullName, 0))
    $view = $database.GetType().InvokeMember(
        'OpenView', 'InvokeMethod', $null, $database,
        @("SELECT Value FROM Property WHERE Property='ProductVersion'"))
    $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null) | Out-Null
    $record = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)

    if (-not $record) {
        throw "Could not read ProductVersion from $($msi.Name)."
    }

    $productVersion = $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, @(1))
    $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null) | Out-Null

    $fields = $productVersion.Split('.')
    while ($fields.Length -lt 4) { $fields += '0' }
    $Version = ($fields[0..3]) -join '.'
}

$hash = (Get-FileHash -LiteralPath $msi.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')

# The download URL carries the version in the object name, so a published release is never
# overwritten in place. Overwriting would break clients that had already read the previous
# hash from the manifest: the download would succeed and then fail verification.
$manifest = [ordered]@{
    schemaVersion = 1
    generatedUtc  = $timestamp
    applications  = @(
        [ordered]@{
            id                      = $ApplicationId
            name                    = $ApplicationName
            latestVersion           = $Version
            releaseDateUtc          = $timestamp
            downloadUrl             = "$BaseUrl/$($msi.Name)"
            sha256                  = $hash
            sizeBytes               = $msi.Length
            releaseNotesUrl         = $ReleaseNotesUrl
            minimumSupportedVersion = $MinimumSupportedVersion
            mandatory               = [bool]$Mandatory
        }
    )
}

$manifestPath = Join-Path $OutputDirectory "$ApplicationId.json"

# Write UTF-8 *without* a BOM. This is not a stylistic choice: Windows PowerShell's
# -Encoding utf8 emits a BOM, S3 serves those bytes back verbatim, and System.Text.Json throws
# "'0xEF' is an invalid start of a value" when parsing a string that begins with U+FEFF.
# Because CheckForUpdateAsync swallows every exception, that would silently disable updates for
# every client - indistinguishable from the manifest simply being unreachable.
[System.IO.File]::WriteAllText(
    $manifestPath,
    ($manifest | ConvertTo-Json -Depth 6),
    (New-Object System.Text.UTF8Encoding $false))

Write-Host "Wrote $manifestPath"
Write-Host "  application : $ApplicationId"
Write-Host "  version     : $Version"
Write-Host "  size        : $($msi.Length) bytes"
Write-Host "  sha256      : $hash"
Write-Host "  downloadUrl : $BaseUrl/$($msi.Name)"
Write-Host ''
Write-Host 'Publish by uploading both files to the bucket root:'
Write-Host "  $($msi.Name)"
Write-Host "  $ApplicationId.json"
Write-Host 'Upload the MSI first - the manifest is what advertises it.'
