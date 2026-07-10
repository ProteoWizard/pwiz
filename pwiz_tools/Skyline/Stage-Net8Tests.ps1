<#
.SYNOPSIS
    Stage the net8 Skyline + test-project build outputs into a single directory.

.DESCRIPTION
    The legacy Jam build dropped Skyline-daily.exe, TestRunner.exe and every test DLL into
    one shared bin (pwiz_tools\Skyline\bin\x64\<Config>). The net8 SDK build gives each project
    its own bin\<Config>\net8.0-windows, so nothing sees the others. TestRunner (and its
    container workers) load Skyline + the test DLLs from one directory, so assemble them here.

    Each source project's output is merged (robocopy) into the staging dir; the shared
    dependencies (Skyline-daily.dll, etc.) simply overwrite identically, and the union yields a
    complete run directory. Run TestRunner.exe from the staging dir (or mount pwizRoot into the
    container and point at the staged path).

.EXAMPLE
    pwsh -File .\Stage-Net8Tests.ps1 -Configuration Debug
#>
param(
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Debug',
    [string] $StagingDir = '',
    [string[]] $Projects = @('Skyline', 'Test', 'TestData', 'TestFunctional', 'TestConnected', 'TestRunner'),
    # Bundle a portable .NET 8 Desktop runtime into <staging>\dotnet so the Docker workers can run
    # the net8 apphost without any runtime installed in the container (pointed at via DOTNET_ROOT).
    [switch] $NoRuntime,
    [string] $DotnetSource = (Join-Path $env:ProgramFiles 'dotnet'),
    [string] $RuntimeMajorMinor = '8.0'
)

$ErrorActionPreference = 'Stop'
$skylineDir = $PSScriptRoot
$tfm = 'net8.0-windows'

if ([string]::IsNullOrEmpty($StagingDir)) {
    $StagingDir = Join-Path $skylineDir "bin\staging-net8\$Configuration"
}
New-Item -ItemType Directory -Force -Path $StagingDir | Out-Null

# Skyline.csproj sits at the Skyline root (bin directly under it); the test projects are in
# their own subdirectories.
function Get-ProjectOutput([string] $project) {
    if ($project -eq 'Skyline') {
        return Join-Path $skylineDir "bin\$Configuration\$tfm"
    }
    return Join-Path $skylineDir "$project\bin\$Configuration\$tfm"
}

foreach ($project in $Projects) {
    $src = Get-ProjectOutput $project
    if (-not (Test-Path $src)) {
        Write-Warning "Skipping $project - no output at $src (build it first)."
        continue
    }
    Write-Host "Staging $project  ($src)"
    # /E recurse (satellite resource dirs), /XO keep newest on identical shared deps,
    # /NP /NDL /NFL /NJH /NJS quiet. robocopy exit codes 0-7 are success.
    robocopy $src $StagingDir /E /XO /NP /NDL /NFL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed staging $project (exit $LASTEXITCODE)" }
}

# Stage a portable .NET 8 Desktop runtime so the Docker workers (which have no .NET installed)
# can run the net8 apphost. The apphost is pointed here via DOTNET_ROOT (set on `docker run`).
# Minimal set the apphost needs: host\fxr\<ver>\hostfxr.dll + the two shared frameworks. dotnet.exe
# is included so `dotnet TestRunner.dll` also works as a fallback.
function Get-HighestVersionDir([string] $parent, [string] $prefix) {
    if (-not (Test-Path $parent)) { return $null }
    Get-ChildItem -Path $parent -Directory |
        Where-Object { $_.Name -like "$prefix*" } |
        Sort-Object { try { [version]$_.Name } catch { [version]'0.0.0' } } |
        Select-Object -Last 1
}

if (-not $NoRuntime) {
    $runtimeDest = Join-Path $StagingDir 'dotnet'
    $netCore = Get-HighestVersionDir (Join-Path $DotnetSource 'shared\Microsoft.NETCore.App') $RuntimeMajorMinor
    $winDesktop = Get-HighestVersionDir (Join-Path $DotnetSource 'shared\Microsoft.WindowsDesktop.App') $RuntimeMajorMinor
    $fxr = Get-HighestVersionDir (Join-Path $DotnetSource 'host\fxr') $RuntimeMajorMinor
    if (-not $netCore -or -not $winDesktop -or -not $fxr) {
        throw "Could not find a $RuntimeMajorMinor.x runtime under $DotnetSource (NETCore.App/WindowsDesktop.App/host\fxr). Pass -DotnetSource or -NoRuntime."
    }
    Write-Host ""
    Write-Host "Staging .NET runtime  (NETCore.App $($netCore.Name), WindowsDesktop.App $($winDesktop.Name), fxr $($fxr.Name))"
    New-Item -ItemType Directory -Force -Path $runtimeDest | Out-Null

    $dotnetExe = Join-Path $DotnetSource 'dotnet.exe'
    if (Test-Path $dotnetExe) { Copy-Item $dotnetExe (Join-Path $runtimeDest 'dotnet.exe') -Force }

    $pairs = @(
        @{ Src = $fxr.FullName;        Dst = Join-Path $runtimeDest "host\fxr\$($fxr.Name)" },
        @{ Src = $netCore.FullName;    Dst = Join-Path $runtimeDest "shared\Microsoft.NETCore.App\$($netCore.Name)" },
        @{ Src = $winDesktop.FullName; Dst = Join-Path $runtimeDest "shared\Microsoft.WindowsDesktop.App\$($winDesktop.Name)" }
    )
    foreach ($p in $pairs) {
        robocopy $p.Src $p.Dst /E /XO /NP /NDL /NFL /NJH /NJS | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed staging runtime $($p.Src) (exit $LASTEXITCODE)" }
    }
}

Write-Host ""
Write-Host "Staged net8 tests to: $StagingDir"
Write-Host "Run e.g.:  $StagingDir\TestRunner.exe test=<name> parallelmode=off"
