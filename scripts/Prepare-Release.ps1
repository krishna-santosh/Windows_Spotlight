[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v\d+\.\d+\.\d+\.\d+$')]
    [string] $Tag,

    [string] $BinaryDirectory = (Join-Path $PSScriptRoot '..\bin\Release'),
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\release')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedVersion = $Tag.Substring(1)
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $BinaryDirectory 'Windows-Spotlight.exe'
$config = Join-Path $BinaryDirectory 'Windows-Spotlight.exe.config'

foreach ($file in @($executable, $config)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Release asset is missing: $file"
    }
}

$fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion
$assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($executable).Version.ToString()
$project = [xml] (Get-Content -LiteralPath (Join-Path $repositoryRoot 'Windows_Spotlight.csproj') -Raw)
$publishVersion = $project.SelectSingleNode("//*[local-name()='ApplicationVersion']").InnerText
$manifest = [xml] (Get-Content -LiteralPath (Join-Path $repositoryRoot 'app.manifest') -Raw)
$manifestVersion = $manifest.SelectSingleNode("//*[local-name()='assemblyIdentity']").GetAttribute('version')

foreach ($version in @{
    File = $fileVersion
    Assembly = $assemblyVersion
    Publish = $publishVersion
    Manifest = $manifestVersion
}.GetEnumerator()) {
    if ($version.Value -cne $expectedVersion) {
        throw "$($version.Key) version '$($version.Value)' does not match tag '$Tag'."
    }
}

$reportedVersion = & $executable --version
if ($LASTEXITCODE -ne 0 -or $reportedVersion -cne "Windows Spotlight v$expectedVersion") {
    throw "The executable's --version output does not match '$Tag'."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$checksums = foreach ($file in @($executable, $config)) {
    $name = Split-Path $file -Leaf
    $target = Join-Path $OutputDirectory $name
    Copy-Item -LiteralPath $file -Destination $target -Force
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[System.IO.File]::WriteAllLines(
    (Join-Path $OutputDirectory 'SHA256SUMS.txt'),
    [string[]] $checksums,
    [System.Text.UTF8Encoding]::new($false)
)
Write-Output "Prepared $Tag assets in $OutputDirectory"
