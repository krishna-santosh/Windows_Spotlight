# Windows_Spotlight

This command-line tool exports cached Windows Spotlight lock-screen and desktop images.

### Installation 
- Using WinGet from a normal terminal (no administrator privileges):
```
winget install --exact --id Windows-Spotlight.Windows-Spotlight --scope user
```

Use an up-to-date App Installer / WinGet. User-scope installation places the
portable executable under `%LOCALAPPDATA%\Microsoft\WinGet\Packages` and updates
your user PATH; open a new terminal after installation. Developer Mode is not
required: WinGet can add the package directory to PATH when it cannot create a
command symlink. Older WinGet versions had problems with this fallback.

An existing machine-wide installation retains its scope during upgrades. To
switch to user scope, uninstall that installation first (this may require admin
rights), then install with the command above. Your exported Pictures are outside
the installation directory.

Alternatively, download `Windows-Spotlight.exe` from the
[releases](https://github.com/krishna-santosh/Windows_Spotlight/releases), save it in
a folder you own, and run it directly. The application runs with your existing
permissions and requires .NET Framework 4.8, which is included on current Windows
11 systems.

### Usage
Run the following command in the terminal (CMD/PowerShell/Gitbash)

 ```
 Windows-Spotlight.exe
 ```

This exports locally cached lock-screen images to your Pictures folder under
`Windows_Spotlight_Images\Landscape` and `Windows_Spotlight_Images\Portrait`.
To include desktop wallpaper Spotlight images:

```
Windows-Spotlight.exe --source desktop
Windows-Spotlight.exe --source all --open-folder
```

`--source` accepts `lockscreen` (the default), `desktop`, or `all`. Desktop images
are discovered from the DesktopSpotlight registry data and all subfolders of the
Client.CBS IrisService cache; no user-specific numeric folder is hardcoded. If
there are no downloaded desktop images, available Windows bundled defaults are
exported instead. Missing caches are allowed. The tool does not download images
from the internet or enable Spotlight.

### Automatic background exports

Startup is disabled until you enable it explicitly:

```
Windows-Spotlight.exe --startup enable
Windows-Spotlight.exe --startup status
Windows-Spotlight.exe --startup disable
```

Enabling startup creates one Windows Task Scheduler task for the current user.
It saves both lock-screen and desktop images without a console window or opening
Explorer, one minute after sign-in and then hourly while you are signed in.
Each run exits when finished. It runs with your normal permissions, requires no
administrator privileges or stored password, works on battery power, and does
not wake the computer or require internet access. Manual and background exports
cannot run at the same time; already saved images continue to be skipped.

To collect only one source automatically:

```
Windows-Spotlight.exe --startup enable --source desktop
Windows-Spotlight.exe --startup enable --source lockscreen
```

Running `--startup enable` again updates the existing task and refreshes its
background executable. Without `--source`, it selects both sources each time.
Manual exports still default to lock-screen images. Startup commands do not
perform an immediate export; run `Windows-Spotlight.exe --source all` to save
images now.

The portable executable contains a windowless background build of the same app.
Enabling startup extracts it into `%LOCALAPPDATA%\Windows-Spotlight`, giving the
task a stable executable path even if the downloaded or WinGet executable moves.
**After upgrading, run `--startup enable` again** to update that background copy.
`--startup status` reports a missing or outdated copy, the configured source,
and Task Scheduler's last-run result and next-run time. The task is visible in
Task Scheduler as `Windows-Spotlight-<your user SID>`.

Background summaries and errors are written to
`%LOCALAPPDATA%\Windows-Spotlight\startup.log`. At the next run after the log
reaches 1 MiB, it rotates to `startup.log.1`, replacing the previous backup.
Disabling startup removes the task and keeps exported images, the background
copy, and diagnostic logs. An export already running may finish. Disable startup
before uninstalling the portable application if you want automatic exports to stop.

The help menu includes examples, output and log locations, source defaults, and
startup behavior. Commands return `0` on success (including no new images) and
`1` for invalid arguments, task-management failures, or failed image exports.

### Names, metadata, and duplicates

When cached metadata is available, exported JPEGs use the subject/location name
as their filename and contain XMP title, description/captions, copyright, and
learn-more link metadata. Unsafe filename characters are replaced, and different
photos with the same name receive a short hash suffix. Images without descriptive
metadata use their SHA-256 hash as the filename. JPEG pixels are not recompressed;
existing EXIF and other image data are preserved.

Each export also contains its original SHA-256 hash in XMP metadata. Existing
exports are scanned on each run, so the same source bytes are skipped even after
renaming a file or when found in both Spotlight sources. There is no separate
hash index or metadata sidecar. Previously saved unchanged images are recognized
by hashing their existing contents, and are left in place. Distinct crops or
resolutions are kept as separate images. Deleting an export permits exporting it
again; editors that strip or change metadata can affect duplicate detection.

Lock-screen exports keep the existing 16:9 / 9:16 selection. Desktop exports also
accept other aspect ratios (at least 1000 pixels on the longer side and 600 on the
shorter side), to accommodate desktop resolutions while excluding thumbnails.

XMP is readable by metadata-aware photo tools; Windows File Explorer may not
show every XMP field in its Details pane.

To access help menu

```
Windows-Spotlight.exe --help
```

### Build and verify

Build with Visual Studio / MSBuild and the .NET Framework 4.8 targeting pack.
There are no external NuGet dependencies:

```
msbuild Windows_Spotlight.sln /p:Configuration=Release
tests\bin\Release\WindowsSpotlight.Tests.exe
```

The fixture tests use generated images and metadata and do not read the user's
Spotlight registry or caches, modify Pictures, or register startup tasks. They
also check CLI validation, task configuration, the embedded windowless worker,
concurrent-export protection, and background logs. CI builds the solution and
runs these tests. Add `--system` to also verify actual cached images, exporting
only into a temporary test output directory that is removed afterward.
Add `--scheduler` for a real Task Scheduler registration/run/removal check using
a uniquely named temporary task. It runs only `--version`, never exports images,
and removes the task afterward.

For release packaging and WinGet submission, see [the release guide](docs/RELEASING.md).
