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
[releases](https://github.com/krishna-santosh/spotlight-images/releases), save it in
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
Spotlight registry or caches or modify Pictures. CI builds the solution and runs
these tests. Add `--system` to also verify actual cached images, exporting
only into a temporary test output directory that is removed afterward.

For release packaging and WinGet submission, see [the release guide](docs/RELEASING.md).
