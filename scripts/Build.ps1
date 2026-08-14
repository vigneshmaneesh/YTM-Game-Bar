[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x86', 'x64', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'

# Some automated shells can expose both Path and PATH in the inherited Windows
# environment block. Roslyn treats environment keys case-insensitively and fails
# when it encounters both, so normalize them before starting MSBuild.
$inheritedPath = $env:PATH
Remove-Item Env:Path -ErrorAction SilentlyContinue
Remove-Item Env:PATH -ErrorAction SilentlyContinue
$env:Path = $inheritedPath

& (Join-Path $PSScriptRoot 'Test-Environment.ps1')

$projectRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $projectRoot 'YouTubeMusicGameBar.sln'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
$msbuild64 = Join-Path (Split-Path -Parent $msbuild) 'amd64\MSBuild.exe'
if (Test-Path -LiteralPath $msbuild64) {
    $msbuild = $msbuild64
}

& $msbuild $solution /restore /m /p:Configuration=$Configuration /p:Platform=$Platform /p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE."
}
