# Releasing Windows Spotlight

Use this procedure for each release. Choose a four-part numeric version in the
format `MAJOR.MINOR.BUILD.REVISION`; the Git tag must be that version prefixed
with `v`.

Set the following variables in PowerShell once and reuse them throughout the
commands below:

```powershell
$version = Read-Host 'Release version (MAJOR.MINOR.BUILD.REVISION)'
$tag = "v$version"
$repositoryUrl = 'https://github.com/krishna-santosh/spotlight-images'
$packageId = 'Windows-Spotlight.Windows-Spotlight'
$assetName = 'Windows-Spotlight.exe'
```

Keep the assembly and file versions in `Properties/AssemblyInfo.cs`, the publish
version in `Windows_Spotlight.csproj`, the identity in `app.manifest`, and the CLI
version check in `tests/Program.cs` consistent with `$version`. Review the changes
and merge them into `main` before tagging.

## Build and verify

Use a Visual Studio Developer PowerShell with the targeting pack required by the
project:

```powershell
msbuild Windows_Spotlight.sln /t:Rebuild /p:Configuration=Release
.\tests\bin\Release\WindowsSpotlight.Tests.exe
.\bin\Release\Windows-Spotlight.exe --version
(Get-FileHash .\bin\Release\Windows-Spotlight.exe -Algorithm SHA256).Hash
```

Confirm that the reported version matches `$tag`. On a Windows machine with
Spotlight images cached, also run:

```powershell
.\tests\bin\Release\WindowsSpotlight.Tests.exe --system
```

The system test writes only to its test output directory and removes its exports
afterward. Check the portable executable from a normal, non-administrator
terminal. It must run without a UAC prompt.

## Publish the GitHub release

From an up-to-date `main` checkout containing all release changes, run:

```powershell
git switch main
git pull --ff-only origin main
git tag -a $tag -m "Windows Spotlight $tag"
git push origin $tag
```

Pushing a `v*` tag starts `.github/workflows/release.yml`. The workflow requires
a four-part numeric tag and checks that the tagged commit belongs to `main`.
It builds Release and runs the fixture tests, then verifies that the tag matches
the assembly, file, publish, manifest, and CLI versions. Only after all these
checks pass does it publish a GitHub release with generated release notes and
the following assets:

- `Windows-Spotlight.exe`
- `Windows-Spotlight.exe.config`
- `SHA256SUMS.txt` (hashes of both files)

The workflow uses the repository's built-in `GITHUB_TOKEN`; no personal access
token is needed. If repository policy restricts Actions write permissions, allow
the release job's `contents: write` permission. Follow the run on the Actions tab
and verify the published release and its assets.

To preview the assets locally after a Release build, without publishing:

```powershell
.\scripts\Prepare-Release.ps1 -Tag $tag
```

This writes the same three files into the ignored `artifacts/release` directory.

Run the system smoke test locally before pushing the tag: GitHub-hosted runners
do not have your Spotlight cache. Do not overwrite published release assets on a
workflow rerun. If the release already exists, review it before retrying;
`gh release create` fails rather than replacing it. Ship changed binaries under
a new version and tag.

Wait for the release workflow to succeed before generating the WinGet manifests
so WinGetCreate can download and hash the final public executable. WinGet
submission remains a separate, manual step.

## Update the existing WinGet package

Keep the existing package ID. Install WinGetCreate if needed, then generate the
updated manifests without submitting yet:

```powershell
winget install --exact --id Microsoft.WingetCreate --scope user
$releaseUrl = "$repositoryUrl/releases/download/$tag/$assetName"
wingetcreate update $packageId --version $version --urls "$releaseUrl|neutral" --out .\winget-manifests
```

The `neutral` override matches the project's AnyCPU build. If the distribution
architecture changes, update the override and installer metadata accordingly.
Review the generated version, default-locale, and installer YAML files. Confirm
the version, release URL, SHA-256, installer type, architecture, and command
alias. Update descriptions, release notes, and repository links as needed to
reflect the release.

Do not add installer switches for this portable EXE. WinGet controls installation
itself. A `Scope: user` manifest field does not enforce user scope for portable
packages; the user's command-line option or WinGet settings control that scope.

## Validate and test installation

Set `$manifestDir` to the generated directory containing the three YAML files
(normally the following path):

```powershell
$manifestDir = ".\winget-manifests\manifests\w\Windows-Spotlight\Windows-Spotlight\$version"
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
a repeat export, and uninstall/reinstall. Also test upgrading from a previously
published version using the supported installation scopes.

## Submit to the community repository

Submit the reviewed manifests, authenticating with GitHub when prompted:

```powershell
wingetcreate submit $manifestDir
```

This creates a pull request in `microsoft/winget-pkgs`. If GitHub authentication
is unavailable, fork that repository and submit the generated files under
the existing package's manifest path in a new directory named `$version`
manually. Keep previously published version directories. Respond to validation
and reviewer feedback and wait for the PR to merge and the source to refresh.
Verify availability with:

```powershell
winget show --exact --id $packageId --version $version
winget install --exact --id $packageId --scope user
```

Official references: [WinGetCreate update](https://github.com/microsoft/winget-create/blob/main/doc/update.md),
[WinGetCreate submit](https://github.com/microsoft/winget-create/blob/main/doc/submit.md),
[manifest validation](https://learn.microsoft.com/en-us/windows/package-manager/winget/validate),
and [portable scope behavior](https://github.com/microsoft/winget-cli/blob/master/doc/specs/%23182%20-%20Support%20for%20installation%20of%20portable%20standalone%20apps.md).
