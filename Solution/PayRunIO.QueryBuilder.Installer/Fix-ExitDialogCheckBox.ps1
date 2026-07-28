# Removes the grey block behind the launch checkbox on the installer's final page.
#
# Windows Installer does not apply the transparent window style to checkbox controls, so an
# ExitDialog checkbox always paints its own rectangle in the system control colour. Over the
# white area of a branded dialog bitmap that shows as a grey band. This is a Windows
# Installer limitation rather than a WiX defect and is closed as won't fix upstream:
#   https://github.com/wixtoolset/issues/issues/1141
#
# Setting the Transparent attribute (65536) on the control does NOT work -- the flag is
# simply ignored. The accepted workaround is to shrink the checkbox to the tick box itself
# and render its caption in a separate Text control, which CAN be transparent.
#
# WiX authors the control inside WixToolset.UI.wixext, where it cannot be overridden from
# Package.wxs, so both changes are applied to the linked package here.
#
# Invoked automatically after Build by the .wixproj.
param(
    [Parameter(Mandatory)]
    [string]$MsiPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MsiPath)) {
    throw "MSI not found: $MsiPath"
}

# Control table attribute flags.
$visible     = 1
$enabled     = 2
$transparent = 65536

# The checkbox is reduced to just the tick box. Windows renders the box itself at roughly
# 12x12 dialog units; the control is sized to match so no grey surround remains.
$checkBoxSize = 12

# Geometry of the stock control, which the replacement caption is aligned to.
$controlX = 135
$controlY = 190
$captionX = $controlX + $checkBoxSize + 3
$captionW = 220 - $checkBoxSize - 3

$installer = New-Object -ComObject WindowsInstaller.Installer

# 1 = transact, so the package is only updated if every statement succeeds.
$db = $installer.GetType().InvokeMember(
    "OpenDatabase", "InvokeMethod", $null, $installer, @($MsiPath, 1))

function Invoke-MsiSql {
    param([string]$Sql)

    $view = $db.GetType().InvokeMember("OpenView", "InvokeMethod", $null, $db, @($Sql))
    $view.GetType().InvokeMember("Execute", "InvokeMethod", $null, $view, $null)
    $view.GetType().InvokeMember("Close", "InvokeMethod", $null, $view, $null)
    [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null
}

try {
    # Read the caption off the existing control so the text stays in one place: it is set
    # from WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT in Package.wxs.
    $read = $db.GetType().InvokeMember(
        "OpenView", "InvokeMethod", $null, $db,
        @("SELECT ``Text`` FROM ``Control`` WHERE ``Dialog_`` = 'ExitDialog' AND ``Control`` = 'OptionalCheckBox'"))
    $read.GetType().InvokeMember("Execute", "InvokeMethod", $null, $read, $null)
    $record = $read.GetType().InvokeMember("Fetch", "InvokeMethod", $null, $read, $null)

    $caption = $null
    if ($record) {
        $caption = $record.GetType().InvokeMember("StringData", "GetProperty", $null, $record, 1)
    }

    $read.GetType().InvokeMember("Close", "InvokeMethod", $null, $read, $null)
    [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($read) | Out-Null

    if ([string]::IsNullOrWhiteSpace($caption)) {
        $caption = "[WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT]"
    }

    # 1. Shrink the checkbox to the tick box and clear its caption, leaving no grey area
    #    beyond the box Windows draws itself.
    Invoke-MsiSql (
        "UPDATE ``Control`` SET ``Width`` = $checkBoxSize, ``Height`` = $checkBoxSize, ``Text`` = NULL " +
        "WHERE ``Dialog_`` = 'ExitDialog' AND ``Control`` = 'OptionalCheckBox'")

    # 2. Add a transparent Text control carrying the caption, beside the tick box.
    #    Removed first so the script is safe to run more than once against a package.
    Invoke-MsiSql (
        "DELETE FROM ``Control`` WHERE ``Dialog_`` = 'ExitDialog' AND ``Control`` = 'OptionalCheckBoxLabel'")

    $labelAttributes = $visible + $enabled + $transparent

    $insert = $db.GetType().InvokeMember(
        "OpenView", "InvokeMethod", $null, $db,
        @("INSERT INTO ``Control`` (``Dialog_``,``Control``,``Type``,``X``,``Y``,``Width``,``Height``,``Attributes``,``Text``) " +
          "VALUES ('ExitDialog','OptionalCheckBoxLabel','Text',$captionX,$controlY,$captionW,17,$labelAttributes,?)"))

    # The caption is passed as a parameter so quotes in the text cannot break the statement.
    $params = $installer.GetType().InvokeMember("CreateRecord", "InvokeMethod", $null, $installer, @(1))
    $params.GetType().InvokeMember("StringData", "SetProperty", $null, $params, @(1, $caption))

    $insert.GetType().InvokeMember("Execute", "InvokeMethod", $null, $insert, @($params))
    $insert.GetType().InvokeMember("Close", "InvokeMethod", $null, $insert, $null)
    [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($insert) | Out-Null

    $db.GetType().InvokeMember("Commit", "InvokeMethod", $null, $db, $null)

    Write-Host "  ExitDialog checkbox reduced to tick box with transparent caption."
}
finally {
    [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($db) | Out-Null
    [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null
}
