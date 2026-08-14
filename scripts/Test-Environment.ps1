[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $projectRoot 'YouTubeMusicGameBar.sln'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

Write-Host 'YouTube Music Game Bar build environment'
Write-Host "Solution: $solution"

if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer was not found. Install Visual Studio 2022 with WinUI application development and UWP tools.'
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild was not found in an installed Visual Studio instance.'
}

$msbuildRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $msbuild))
$uwpTargets = Join-Path $msbuildRoot 'Microsoft\WindowsXaml'
$sdkLib = 'C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0'

Write-Host "MSBuild: $msbuild"
Write-Host "UWP XAML targets present: $(Test-Path -LiteralPath $uwpTargets)"
Write-Host "Windows SDK 10.0.22621 present: $(Test-Path -LiteralPath $sdkLib)"

if (-not (Test-Path -LiteralPath $sdkLib)) {
    throw 'Windows SDK 10.0.22621.0 is missing. Add it through Visual Studio Installer.'
}

if (-not (Test-Path -LiteralPath $uwpTargets)) {
    throw 'UWP XAML build targets are missing. Add Universal Windows Platform tools through Visual Studio Installer.'
}

Write-Host 'Environment checks passed.' -ForegroundColor Green
