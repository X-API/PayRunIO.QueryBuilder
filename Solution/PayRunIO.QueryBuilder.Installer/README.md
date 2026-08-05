# PayRun.io Query Builder Installer

Builds the MSI package for the Query Builder desktop utility using WiX v5
(`WixToolset.Sdk`), so no Visual Studio installation or global WiX toolset is required.

## Building

The installer packages the application's self contained single file publish output, so the
application must be published first:

```powershell
dotnet publish ..\PayRunIO.QueryBuilder\PayRunIO.QueryBuilder.csproj `
    -c Release -r win-x64 --self-contained true

dotnet build PayRunIO.QueryBuilder.Installer.wixproj -c Release -p:PackageVersion=1.1.0
```

The package is written to `bin\Release\PayRunIO.QueryBuilder-<version>.msi`. Omitting
`PackageVersion` defaults to `1.1.0`.

In CI, pass the version so that every build produces a distinct, upgradeable package, and
pass an absolute `PublishRoot` when publishing to a staging directory:

```powershell
dotnet build PayRunIO.QueryBuilder.Installer.wixproj -c Release `
    -p:PackageVersion=<version> -p:PublishRoot=<absolute path>\
```

> `PublishRoot` must end with a trailing backslash.

### TeamCity

The build server owns the version numbering, so its build number is passed straight through:

```powershell
dotnet build PayRunIO.QueryBuilder.Installer.wixproj -c Release `
    -p:PackageVersion=%build.number% `
    -p:PublishRoot=%teamcity.build.checkoutDir%\PayRunIO.QueryBuilder\bin\Release\net8.0-windows7.0\win-x64\publish\
```

Do not pass a version as `ProductVersion`. A property given on the command line is global
and cannot be adjusted by the project, so a four part value would reach the compiler
unchanged and produce packages differing only in the field Windows Installer ignores —
every build would look like the same product and upgrades would silently do nothing.
`PackageVersion` exists precisely so the project can apply the truncation below.

Add the package to the artifact rules alongside the existing zips:

```
PayRunIO.QueryBuilder.Installer\bin\Release\PayRunIO.QueryBuilder-*.msi => .
```

## Versioning

The package version is whatever the build server supplies via `PackageVersion`; any
`<major>.<minor>.<patch>` value is accepted, so the numbering is not tied to a `1.1.x` line.

**Windows Installer ignores the fourth field of `ProductVersion`.** A package built as
`1.1.0.5` and one built as `1.1.0.6` are the same product as far as upgrade detection is
concerned, and installing the second over the first would silently do nothing. A four part
`PackageVersion` is therefore truncated to its first three fields. The build server emits
three part numbers, so this is a safety net rather than the normal path — but it means a
build number pattern in which **only the fourth field moves** would collapse every build to
the same package version and stop upgrades working. Keep at least one of the first three
fields moving between releases.

The assembly version in the application project keeps the familiar four part form; only the
package version is truncated.

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
| `DesktopShortcut` component | `1378F758-B2B5-473F-93AB-3D3B16DC122E` |
| `ShortcutChoiceMarker` component | `D4F1B0C8-6E1A-4A2C-9F3D-7B5E8C0A1D62` |

`ProductCode` is deliberately left to be generated per build, which is what a major upgrade
requires.

## Shortcuts

A **Start menu** entry under `PayRun.io` is always installed. A **desktop** shortcut is
optional, offered by the `ShortcutOptionsDlg` page and ticked by default.

There is deliberately **no taskbar option**. Taskbar pinning has not been programmable since
Windows 7: the `taskbarpin` verb was removed from the shell, and the pinned list is validated
against a hash under `HKCU\...\Taskband`, so a `.lnk` written into
`…\User Pinned\TaskBar` is discarded by Explorer. The only supported route is
`TaskbarLayoutModification.xml`, which is machine-wide Group Policy applied at logon and
needs administrator rights — the opposite of what this per-user package is for. Pinning is
left to the user.

### Controlling it from the command line

`INSTALLDESKTOPSHORTCUT` is a public property, so a silent install can set it either way:

```powershell
msiexec /i PayRunIO.QueryBuilder-<version>.msi /qn INSTALLDESKTOPSHORTCUT=0
```

Omitting it installs the shortcut, matching the wizard's default.

### How the choice survives an upgrade

Three registry values under `HKCU\Software\PayRun.io\QueryBuilder` drive this, and the
distinction between them is the whole design:

| Value | Written by | Means |
| ----- | ---------- | ----- |
| `desktopShortcut` | `DesktopShortcut` (conditional) | this install has the shortcut |
| `shortcutChoiceRecorded` | `ShortcutChoiceMarker` (unconditional) | this install *offered* the choice |

`desktopShortcut` alone is ambiguous: it is absent both for a user who declined the shortcut
and for anyone still on a release that predates the feature. Treating those the same silently
opts existing users out of a feature they were never shown. `shortcutChoiceRecorded` is what
tells them apart — hence a second component that exists only to write one registry value.

Three `SetProperty` actions apply this, all scheduled **after `AppSearch`** (which populates
the searches) and all conditioned on `INSTALLDESKTOPSHORTCUT` being *empty*:

1. `KeepDesktopShortcutFromPreviousInstall` — previous install had it, so keep it.
2. `DropDesktopShortcutFromPreviousInstall` — previous install offered it and it was
   declined, so set an explicit `0`.
3. `DefaultDesktopShortcut` — everything left undecided (first install, or upgrade from a
   pre-feature release) gets the default of `1`.

Two traps, both of which shipped as bugs during development and are covered by the table
below:

- **The property must be declared with no value.** Putting `Value="1"` on the `Property`
  element makes a command-line `0` indistinguishable from the default by the time these
  actions run, and step 1 stamps it back to `1` — silently ignoring
  `INSTALLDESKTOPSHORTCUT=0` on an upgrade.
- **An unticked checkbox clears its property rather than setting `0`.** Empty is exactly the
  "not yet decided" state step 3 keys off, so the default was re-applied *after* the user had
  unticked the box and the shortcut appeared anyway. The `Next` button therefore publishes an
  explicit `0` or `1` before navigating. Silent installs never hit this, because they always
  pass a non-empty value — so this is invisible to any test that does not drive the wizard.

### Verifying

Both the silent and the UI paths need checking; they fail differently, as above.

| Scenario | Expected |
| -------- | -------- |
| Fresh install, defaults | shortcut created |
| Fresh install, `INSTALLDESKTOPSHORTCUT=0` | no shortcut |
| Wizard, box left ticked | shortcut created |
| Wizard, box unticked | **no shortcut** |
| Upgrade over an install that had it | still there |
| Upgrade over an install that declined it | still absent |
| Upgrade passing an explicit value | the explicit value wins |
| Upgrade from a pre-feature release | shortcut created (the default) |
| Uninstall | shortcut and both registry values removed |

## Installer UI

The wizard is Welcome â†’ Shortcuts â†’ Confirm â†’ Install â†’ Finish.

**There is deliberately no licence agreement page.** Every stock WiX dialog set includes one
(`WixUI_Minimal` via `WelcomeEulaDlg`, `WixUI_InstallDir` and `WixUI_FeatureTree` via
`LicenseAgreementDlg`), and when no licence file is supplied WiX displays its own placeholder
text â€” which is where the lorem ipsum came from. Rather than ship a licence nobody needs, the
Welcome page's Next button is repointed at `ShortcutOptionsDlg` with `Order="10"`, which
outranks the stock `Order="1"` navigation and leaves `LicenseAgreementDlg` unreachable.
`VerifyReadyDlg`'s Back is repointed to match, so the shortened path works in both directions.

`ShortcutOptionsDlg` is authored directly in `Package.wxs`. That is legal because it is a
**new** dialog — the `WIX0091` conflict described below applies only to redefining one that
already exists in `WixToolset.UI.wixext`.

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

**The script covers `ExitDialog` only, and should stay that way.** The desktop shortcut
checkbox on `ShortcutOptionsDlg` has exactly the same Windows Installer limitation, but that
dialog is authored here rather than imported, so the same workaround is expressed natively:
a 12x12 `CheckBox` with no caption beside a `Transparent="yes"` `Text` control. Author any
future checkbox that way and the script stays a single-purpose fix-up for the one control
that cannot be reached from `.wxs`.

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
$p = Start-Process "$env:LOCALAPPDATA\Programs\PayRunIO\QueryBuilder\PayRunIO.QueryBuilder.exe" -PassThru
Start-Sleep -Seconds 10
(Get-Process -Id $p.Id).MainWindowTitle
```

## Install scope

The package installs **per user** into `%LocalAppData%\Programs\PayRunIO\QueryBuilder`.
Every utility in the suite installs beneath the shared `PayRunIO` folder, and the product
folder is spelled without a space so that it matches the settings folder the application
creates at `%AppData%\PayRunIO\QueryBuilder`.
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

## Publishing a release

**Releases are published by hand.** The build produces the artefacts; nothing is uploaded
automatically, so a successful build is not a release and the version that reaches users stays
a deliberate decision.

Installed applications discover upgrades through a **per application manifest** in the
public-read `prio-utilities` bucket, named after the application identifier:

| Application | Manifest URL |
| ----------- | ------------ |
| Query Builder | `https://prio-utilities.s3.eu-west-2.amazonaws.com/query-builder.json` |
| Data Explorer | `…/data-explorer.json` |
| Import Mapper | `…/import-mapper.json` |
| Journal Manager | `…/journal-manager.json` |

One file per application means publishing one utility never involves merging or re-uploading
another's entry, and a malformed manifest can only affect the application it belongs to. The
client derives the URL from the `applicationId` it already passes to `StartBackgroundCheck`, so
there is no extra configuration to keep in step.

### Build artefacts

`New-VersionManifest.ps1` runs automatically from the `WriteVersionManifest` target, so a plain
`dotnet build` of the installer emits both files side by side in `bin\Release`:

```
PayRunIO.QueryBuilder-1.1.300.msi
query-builder.json
```

The version, SHA-256 and size are read from the built package, so the MSI is the single source
of truth. To generate the manifest by hand — or with a different identifier when this is reused
for another utility:

```powershell
pwsh .\New-VersionManifest.ps1 -MsiPath bin\Release\PayRunIO.QueryBuilder-1.1.300.msi
pwsh .\New-VersionManifest.ps1 -MsiPath <msi> -ApplicationId data-explorer -ApplicationName 'PayRun.io Data Explorer'
```

Pass `-Mandatory` to remove the user's option to skip or postpone, or `-MinimumSupportedVersion`
to force an upgrade for anything older.

### TeamCity

**No extra build step is needed.** `New-VersionManifest.ps1` and `Test-ReleaseArtefacts.ps1` both
run from targets inside the `.wixproj`, so the existing installer build produces and verifies the
manifest. No AWS credentials or CLI are required on the agent.

Two things must be configured:

**1. Pass the same version to both projects.** The application publish needs it too, not just the
installer:

```powershell
dotnet publish PayRunIO.QueryBuilder\PayRunIO.QueryBuilder.csproj -c Release -r win-x64 `
    --self-contained true -p:PackageVersion=%build.number%

dotnet build PayRunIO.QueryBuilder.Installer\PayRunIO.QueryBuilder.Installer.wixproj -c Release `
    -p:PackageVersion=%build.number% -p:PublishRoot=<absolute path>\
```

The update client compares the manifest against the **running assembly's** version, so if the
application is published without `PackageVersion` it reports `1.1.0.0` regardless of the package
that installed it, and every launch is offered the update it already applied. The
`VerifyReleaseArtefacts` target fails the build if the two diverge, so this cannot ship silently.

**2. Pin the artifact rules to the build number**, rather than using a wildcard:

```
PayRunIO.QueryBuilder.Installer\bin\Release\PayRunIO.QueryBuilder-%build.number%.msi => .
PayRunIO.QueryBuilder.Installer\bin\Release\query-builder.json => .
```

A wildcard (`PayRunIO.QueryBuilder-*.msi`) collects every package left in the output directory on
a non-clean agent checkout, while there is only ever **one** `query-builder.json` — describing the
newest. That yields several MSIs and a manifest matching just one of them, with nothing to
indicate which. Pinning the name means a rule that stops matching fails the build visibly instead.

Note that the build number's fourth field is truncated from the *package* version (Windows
Installer ignores it) but kept in the *assembly* version, so a build number of `1.2.1234.0`
produces `PayRunIO.QueryBuilder-1.2.1234.msi` advertising `latestVersion` `1.2.1234.0`. The
version comparison treats the two forms as equal, so this is consistent rather than a mismatch.

### Uploading

Download both artefacts from the build and upload them to the **bucket root**.

**Upload the MSI first.** The manifest is what advertises the release, so publishing it first
offers clients a download that 403s until the package upload finishes.

Set `Content-Type: application/json` on the manifest, and a short `Cache-Control` such as
`max-age=300`: S3 sends no cache headers by default, and an intermediate proxy holding a stale
manifest would hide the release.

### Details that are load-bearing

- **The manifest is UTF-8 without a BOM.** `Set-Content -Encoding utf8` on Windows PowerShell
  emits a BOM, S3 serves it back verbatim, and `System.Text.Json` throws
  `'0xEF' is an invalid start of a value`. Since `CheckForUpdateAsync` swallows every exception,
  a BOM disables updates for every client *silently*. The script writes via `UTF8Encoding($false)`
  for this reason — do not "tidy" it back to `Set-Content`.
- **`WriteVersionManifest` runs after `FixExitDialogCheckBox`**, which rewrites the MSI in place.
  Hashing the package before that edit advertises a hash no download can ever match, and the
  client discards a package that fails verification.
- **The object name carries the version**, so a release is never overwritten in place.
  Overwriting breaks clients that already read the previous hash: the download succeeds and
  then fails verification.
- **Never edit a published manifest to point at a different build** without changing the hash.
  The hash is the only integrity control until the packages are Authenticode signed.

Note that the bucket denies listing, so S3 answers a request for a missing object with **403,
not 404**. A mistyped URL, a permissions fault, an unpublished application and an offline
machine are all indistinguishable to the client — which is why `UpdateService` traces its
failures rather than discarding them. Attach a debugger or run DebugView to see the trace.

The stale copy at `docs/devportal/content/files/utilities/versions.json` is superseded and no
longer read by any client.

