# PayRun.io Query Builder Installer

Builds the MSI package for the Journal Manager desktop utility using WiX v5
(`WixToolset.Sdk`), so no Visual Studio installation or global WiX toolset is required.

## Building

The installer packages the application's self contained single file publish output, so the
application must be published first:

```powershell
dotnet publish ..\PayRunIO.QueryBuilder\PayRunIO.QueryBuilder.csproj `
    -c Release -r win-x64 --self-contained true -p:BuildNumber=0

dotnet build PayRunIO.QueryBuilder.Installer.wixproj -c Release -p:BuildNumber=0
```

The package is written to `bin\Release\PayRunIO.QueryBuilder-<version>.msi`.

In CI, pass the run number so that every build produces a distinct, upgradeable package,
and pass an absolute `PublishRoot` when publishing to a staging directory:

```powershell
dotnet build PayRunIO.QueryBuilder.Installer.wixproj -c Release `
    -p:BuildNumber=$env:GITHUB_RUN_NUMBER -p:PublishRoot=<absolute path>\
```

> `PublishRoot` must end with a trailing backslash.

When rebuilding the same source at a different `BuildNumber` locally, add `-t:Rebuild`.
Without it MSBuild considers the inputs unchanged, skips the link step and then fails to
find the renamed package.

## Versioning

**Windows Installer ignores the fourth field of `ProductVersion`.** A package built as
`1.1.0.5` and one built as `1.1.0.6` are the same product as far as upgrade detection is
concerned, and installing the second over the first would silently do nothing.

The `ProductVersion` used here is therefore three part, with the CI run number as the
**patch** field (`1.1.<BuildNumber>`). The assembly version in the application project keeps
the familiar four part form; only the package version is truncated.

Verify an upgrade by installing consecutive builds and confirming that exactly one entry
appears in Apps & Features.

## Stable identifiers

These GUIDs must never change. `UpgradeCode` is what allows a new package to replace an
installed one; changing it would leave both versions installed side by side.

| Purpose | GUID |
| ------- | ---- |
| `UpgradeCode` (product identity) | `5C41A2E9-0F80-4DB6-A49E-6BB156F0E7E8` |
| `MainExecutable` component | `45CC3CE8-23DB-4836-B00F-2BB2F3FF013A` |
| `StartMenuShortcut` component | `90C8E3A3-0C1A-45BA-A0E3-E04CD2EABA64` |

`ProductCode` is deliberately left to be generated per build, which is what a major upgrade
requires.

## Installer UI

The wizard is Welcome â†’ Confirm â†’ Install â†’ Finish.

**There is deliberately no licence agreement page.** Every stock WiX dialog set includes one
(`WixUI_Minimal` via `WelcomeEulaDlg`, `WixUI_InstallDir` and `WixUI_FeatureTree` via
`LicenseAgreementDlg`), and when no licence file is supplied WiX displays its own placeholder
text â€” which is where the lorem ipsum came from. Rather than ship a licence nobody needs, the
Welcome page's Next button is repointed at `VerifyReadyDlg` with `Order="10"`, which outranks
the stock `Order="1"` navigation and leaves `LicenseAgreementDlg` unreachable.

`LicenseAgreementDlg` still exists in the MSI's Dialog table, because it is part of the
imported dialog set. That is expected and harmless: nothing navigates to it. If you ever do
need a licence page, delete the `WelcomeDlg` override and supply a real RTF via
`<WixVariable Id="WixUILicenseRtf" Value="License.rtf" />`.

### Artwork

The stock WiX graphics are replaced with PayRun.io branded artwork in `Assets\`:

| File | Size | Where it appears | WiX variable |
| ---- | ---- | ---------------- | ------------ |
| `DialogBanner.bmp` | 493 x 312 | Side panel on Welcome and Finish | `WixUIDialogBmp` |
| `TopBanner.bmp` | 493 x 58 | Strip across the other pages | `WixUIBannerBmp` |

Both are **24 bit BMP**. Windows Installer will not render a 32bpp bitmap with an alpha
channel â€” it shows black â€” so the dimensions and bit depth are not negotiable.

`Assets\Generate-Banners.ps1` regenerates both from `Assets\PayRunIO_logo.png`. Run it only
when the branding changes; the bitmaps are committed so an ordinary build needs neither the
script nor PowerShell image support:

```powershell
pwsh .\Assets\Generate-Banners.ps1
```

#### Where the text goes â€” the constraint that drives the layout

Neither bitmap is a decorative strip beside the content: **WiX draws its text controls
directly on top of them**, in the system dialog colour (near black). Artwork placed under
that text is both overlapped and illegible, which is exactly what a first attempt produces.

The control geometry is stored in the MSI in dialog units; multiply by `493/370` (â‰ˆ1.333)
for pixel positions on these bitmaps:

| Page | Control | Dialog units | Pixels | Consequence |
| ---- | ------- | ------------ | ------ | ----------- |
| Welcome / Finish | `Title`, `Description` | X=135 W=220 | x=180 â†’ 473 | Only the left ~180px may carry artwork |
| Finish | `OptionalCheckBox` | X=135 Y=190 | x=180 y=253 | Sits on the bitmap â€” see the white note below |
| Confirm etc. | `InstallTitle` | X=15 W=300 | x=20 â†’ 420 | Only the right ~75px may carry artwork |

Hence the side panel is a branded column on the **left** with the rest white, and the top
banner is white on the **left** with a small logo pinned **right**. The two are mirror
images of each other, which is counter intuitive but correct.

One detail that only shows up once the wizard is running: **the branded panel stops at x=164,
not x=180.** Ending it flush against the text controls leaves the green edge touching the
wizard text; the 16px gap is the padding. The light zone is pure white to match the dialog
body.

### The launch checkbox background

The launch checkbox on the final page paints a grey block over the white part of the dialog
bitmap. **This is a Windows Installer limitation, not an artwork or attribute problem:**
Windows Installer never applies the transparent window style to checkbox controls, so the
control always fills its own rectangle with the system control colour.

Reported upstream as [wixtoolset/issues#1141](https://github.com/wixtoolset/issues/issues/1141)
and closed as won't fix â€” the WiX maintainers' position is *"that is a bug in windows
installer (not WiX) as it doesn't set the transparent windows style"*.

Two things that do **not** work, both worth knowing before trying them again:

- Making the bitmap's light zone pure white. The control never samples the bitmap.
- Setting the Transparent attribute (65536) on the checkbox. The flag is accepted into the
  Control table and then ignored at run time.

The [accepted workaround](https://www.mail-archive.com/wix-users@lists.sourceforge.net/msg51904.html)
is to shrink the checkbox to the tick box itself and render its caption in a separate `Text`
control, which *can* be transparent. `Fix-ExitDialogCheckBox.ps1` applies this to the linked
package â€” the control is authored inside `WixToolset.UI.wixext` and cannot be overridden from
`Package.wxs` â€” and is run automatically by the `FixExitDialogCheckBox` target in the
`.wixproj`. It resizes `OptionalCheckBox` to 12x12 with no caption, and inserts a transparent
`OptionalCheckBoxLabel` beside it carrying the original text.

Verify with:

```sql
SELECT Control,Width,Height,Attributes,Text FROM Control
WHERE Dialog_='ExitDialog' AND Control LIKE 'OptionalCheckBox%'
```

Expect the checkbox at 12x12 with empty text, and the label with attributes 65539
(Visible + Enabled + Transparent).

#### Why this is a post-build script and not .wxs authoring

Scripted edits to a linked MSI are unusual and worth justifying. The layout itself is
ordinary `.wxs` â€” the artwork, the licence page bypass and the launch action are all authored
declaratively in `Package.wxs`. Only this one control needs the script, because the control
belongs to a dialog defined inside `WixToolset.UI.wixext`.

Two native alternatives were tried and measured:

1. **Redefine `ExitDialog` in our own `.wxs`.** Does not link. WiX reports
   `WIX0091: Duplicate Dialog with identifier 'ExitDialog'` and states explicitly that
   *"access modifiers (global, library, file, section) cannot prevent these conflicts"*.
   A library dialog cannot be overridden or partially amended â€” the `Control` table entries
   come as one indivisible symbol set.

2. **Drop `ui:WixUI` and author the dialogs directly.** This does work and is fully native,
   but abandoning the stock dialog set means supplying *every* dialog Windows Installer
   requires. A minimal attempt failed ICE20 validation for `FilesInUse`, `FatalError`,
   `UserExit` and `ErrorDialog`, before adding the browse, maintenance and progress dialogs
   the wizard also needs. That is several hundred lines of vendored dialog authoring per
   application, which then has to be maintained against future WiX releases.

The script is roughly 40 lines of intent-revealing SQL against two rows, applied by an
MSBuild target so a plain `dotnet build` still produces a finished package. If the wizard ever
needs deeper customisation than this one control, option 2 becomes the better trade and the
script should be retired rather than extended.

To re-check the geometry for another application, query the built MSI:

```sql
SELECT Control,Type,X,Y,Width,Height FROM Control WHERE Dialog_='WelcomeDlg'
```

The panel is a **flat block of `#333B41` with a 2px `#00B000` edge** where it meets the light
text area. Gradients were tried first and looked washed out at this size, particularly across
the transition; a translucent decorative swoosh had the same problem, reading as texture over
a gradient but as a stray diagonal on a flat panel. Both were removed.

Two further details worth keeping if this is adapted for the other utilities:

- The supplied logo is flattened onto its own `#333B41` background. Drawn directly it leaves
  a visible rectangle over the panel gradient, so the script keys that colour out to
  transparency and crops to the artwork bounds before scaling.
- `Assets\Preview-Wizard.ps1` composites the real text controls at their real coordinates
  over the bitmaps and writes preview PNGs to the temp folder, so the layout can be checked
  without installing anything. Run it after changing the artwork â€” the wizard is otherwise
  the only place these errors show up.

The final page offers a **"Launch PayRun.io Query Builder"** checkbox, ticked by default. It
runs the `LaunchApplication` custom action (`WixShellExec` from `WixToolset.Util.wixext`)
against `WixShellExecTarget`, which resolves to the installed executable via `[#JournalManagerExe]`
rather than a hard coded path. The action is conditioned on `NOT Installed`, so it fires on a
fresh install or upgrade but not on repair or uninstall.

Because the package installs per user, the application starts unelevated â€” which matters, as
a process started elevated from an installer would otherwise inherit administrator rights.

## appsettings.json

Query Builder is the only utility that ships a loose `appsettings.json` beside the
executable, and it loads it with `optional: false` — the application will not start without
it. Two settings in the project and package exist because of that:

- **`ExcludeFromSingleFile`** in the csproj keeps the file out of the single file bundle, so
  the installed copy is a real, editable file.
- **`App.GetSettingsDirectory()`** resolves the base path from `Environment.ProcessPath`
  rather than `AppDomain.CurrentDomain.BaseDirectory`. This pairing is mandatory: for a
  bundled single file application `BaseDirectory` resolves to the temporary extraction
  directory (`%TEMP%\.net\PayRunIO.QueryBuilder\<hash>\`), so excluding the file from the
  bundle without also changing the lookup makes the application fail at startup with
  `FileNotFoundException`. Measured values from a real single file build:

  | API | Resolves to | `appsettings.json` found |
  | --- | ----------- | ------------------------ |
  | `AppDomain.CurrentDomain.BaseDirectory` | temp extraction directory | No |
  | `Environment.ProcessPath` directory | install folder | Yes |
- **`NeverOverwrite="yes"` *and* `Permanent="yes"`** on the `AppSettings` component.
  `NeverOverwrite` alone is a trap: a major upgrade removes the old product before installing
  the new one, the file is deleted by that removal, then skipped by the install because the
  component is never-overwrite — leaving the application unable to start. `Permanent` keeps
  the file in place so the edited copy survives.

The trade-off is that new default settings added in a later release never reach existing
installations, so **any new configuration key must be handled as absent by the application**.
The file is also deliberately left behind on uninstall.

### Verifying

Install, edit `appsettings.json`, install the next build, then confirm the executable version
advanced, the edit is still present, **and the application still launches**.

That last step matters: a startup configuration failure does not terminate the process,
because `Application_DispatcherUnhandledException` sets `e.Handled = true`. The process keeps
running with only an error dialog showing, so "the process is still alive" is not evidence of
a successful start. Check the window title instead:

| Window title | Meaning |
| ------------ | ------- |
| `PayRun.io Query Builder` | started correctly |
| `Unhandled Exception - FileNotFoundException` | configuration was not found |

```powershell
$p = Start-Process "$env:LOCALAPPDATA\Programs\PayRun.io Query Builder\PayRunIO.QueryBuilder.exe" -PassThru
Start-Sleep -Seconds 10
(Get-Process -Id $p.Id).MainWindowTitle
```

## Install scope

The package installs **per user** into `%LocalAppData%\Programs\PayRun.io Query Builder`.
No elevation prompt is raised, either on first install or when the application applies an
upgrade itself, and administrators do not need local administrator rights.

The trade off is one copy per Windows profile, which is not suited to shared or RDS
machines. Revisit `Scope="perUser"` in `Package.wxs` if that changes.

## Expected build warnings

Two ICE warnings are expected and benign:

- **ICE61** â€” raised because `AllowSameVersionUpgrades="yes"` makes the upgrade range
  include the current version, which is intentional so that repeat installs of the same
  version behave cleanly during testing.
- **ICE91** â€” notes that a per user directory does not vary by `ALLUSERS`. That is the
  intended behaviour for a per user package.

## Related

The published version is advertised to installed applications through the update manifest
at `https://developer.payrun.io/content/files/utilities/versions.json`. After publishing a
release, update that manifest (in the `docs` repository under
`devportal/content/files/utilities/`) with the new version, download URL, SHA-256 hash and
size, or installed copies will not be offered the upgrade.

