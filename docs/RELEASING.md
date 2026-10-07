# Releasing Windows Spotlight

The v2 release uses version `2.0.0.0` and Git tag `v2.0.0.0`. Keep the assembly
and file versions in `Properties/AssemblyInfo.cs`, the publish version in
`Windows_Spotlight.csproj`, the identity in `app.manifest`, and the CLI version
check in `tests/Program.cs` consistent when preparing future releases.

## Build and verify

After committing the reviewed changes and merging `dev` into `main`, use a
Visual Studio Developer PowerShell with the .NET Framework 4.8 targeting pack:

```powershell
msbuild Windows_Spotlight.sln /t:Rebuild /p:Configuration=Release
.\tests\bin\Release\WindowsSpotlight.Tests.exe
.\bin\Release\Windows-Spotlight.exe --version
(Get-FileHash .\bin\Release\Windows-Spotlight.exe -Algorithm SHA256).Hash
```

The expected version output is `Windows Spotlight v2.0.0.0`. No NuGet restore is
required. On a Windows machine with Spotlight images cached, also run:

```powershell
.\tests\bin\Release\WindowsSpotlight.Tests.exe --system
```

The system test writes only to its test output directory and removes its exports
afterward. Check the portable executable from a normal, non-administrator
terminal. It must run without a UAC prompt.

## Publish the GitHub release

Create a GitHub release from `main` tagged `v2.0.0.0` on the
[repository's releases page](https://github.com/krishna-santosh/spotlight-images/releases).

Upload `bin/Release/Windows-Spotlight.exe` as the asset named
`Windows-Spotlight.exe`. This is the portable command, not a setup/bootstrapper;
the application uses framework assemblies and does not need
`System.Drawing.Common.dll`. `Windows-Spotlight.exe.config` can be offered as an
additional asset for manual downloads; the application targets .NET Framework
4.8 and does not depend on custom binding redirects.

Publish the release before generating the WinGet manifests so WinGetCreate can
download and hash the final public executable. Do not replace that asset after
submitting its manifest; changed bytes require a new hash and manifest update.

## Update the existing WinGet package

Keep the existing package ID: `Windows-Spotlight.Windows-Spotlight`. Install
WinGetCreate, then generate the updated manifests without submitting yet:

```powershell
winget install --exact --id Microsoft.WingetCreate --scope user
$releaseUrl = 'https://github.com/krishna-santosh/spotlight-images/releases/download/v2.0.0.0/Windows-Spotlight.exe'
wingetcreate update Windows-Spotlight.Windows-Spotlight --version 2.0.0.0 --urls "$releaseUrl|neutral" --out .\winget-manifests
```

The architecture override keeps the existing `neutral` architecture for this
AnyCPU executable. Review the generated version, default-locale, and installer
YAML files. Confirm the new URL and SHA-256, `InstallerType: portable`, and
`Commands: [Windows-Spotlight]`. Update the description to mention both lock-screen
and desktop exports and use the current repository URLs.

Do not add installer switches for this portable EXE. WinGet controls installation
itself. A `Scope: user` manifest field does not enforce user scope for portable
packages; the user's command-line option or WinGet settings control that scope.

## Validate and test installation

Set `$manifestDir` to the generated directory containing the three YAML files
(normally the following path):

```powershell
$manifestDir = '.\winget-manifests\manifests\w\Windows-Spotlight\Windows-Spotlight\2.0.0.0'
winget validate --manifest $manifestDir
```

On a test machine, enable local manifests if needed. This one-time WinGet setting
requires an administrator terminal; it is only for testing unpublished manifests,
not for installing the published package:

```powershell
winget settings --enable LocalManifestFiles
```

Return to a normal terminal, with Developer Mode disabled, and test:

```powershell
winget install --manifest $manifestDir --scope user
```

Open a new terminal and check `Windows-Spotlight --version`, `--help`, an export,
a repeat export, and uninstall/reinstall. Test upgrading a user-scope v1 install
as well. Existing machine-scope installs retain their scope on upgrade; migrating
those users requires uninstalling the old machine install, then installing with
`--scope user`.

## Submit to the community repository

Submit the reviewed manifests, authenticating with GitHub when prompted:

```powershell
wingetcreate submit $manifestDir
```

This creates a pull request in `microsoft/winget-pkgs`. If GitHub authentication
is unavailable, fork that repository and submit the generated files under
`manifests/w/Windows-Spotlight/Windows-Spotlight/2.0.0.0/` manually. Keep the v1
manifests. Respond to validation/reviewer feedback and wait for the PR to merge
and the source to refresh. Users can then install with:

```powershell
winget install --exact --id Windows-Spotlight.Windows-Spotlight --scope user
```

Official references: [WinGetCreate update](https://github.com/microsoft/winget-create/blob/main/doc/update.md),
[WinGetCreate submit](https://github.com/microsoft/winget-create/blob/main/doc/submit.md),
[manifest validation](https://learn.microsoft.com/en-us/windows/package-manager/winget/validate),
and [portable scope behavior](https://github.com/microsoft/winget-cli/blob/master/doc/specs/%23182%20-%20Support%20for%20installation%20of%20portable%20standalone%20apps.md).
