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
    # TestTutorial and TestPerf are in SkylineTester's TEST_DLLS list, so omitting them here left
    # the Tutorials tab (and the perf tests) empty even after a successful staging run.
    [string[]] $Projects = @('Skyline', 'CommonTest', 'Test', 'TestData', 'TestFunctional', 'TestConnected', 'TestRunner', 'TestTutorial', 'TestPerf'),
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

# Warn when a project is about to be staged from output older than its own sources. Staging
# happily copies whatever is on disk, so a project that was not rebuilt is staged silently - and
# build.bat deliberately excludes TestPerf and TestTutorial from the standard build while this
# script stages them by default, which makes that easy to hit.
#
# It no longer has to catch a stale *dependency*. The copies below used to pass /XO ("exclude
# older"), which skipped any file whose source was older than the staged copy - so a NuGet DLL,
# carrying the package's original timestamp, lost to whatever assembly was already staged and
# survived every re-stage. Without /XO a file that differs from its source is replaced whatever
# the timestamps say.
function Test-OutputStale([string] $project, [string] $outputDir) {
    $sourceDir = if ($project -eq 'Skyline') { $skylineDir } else { Join-Path $skylineDir $project }
    if (-not (Test-Path $sourceDir)) { return $false }
    # Skyline's directory contains the test projects as subdirectories; their sources are not its.
    # Only Skyline needs this: its directory physically contains the test projects.
    $siblings = @()
    if ($project -eq 'Skyline') {
        $siblings = $Projects | Where-Object { $_ -ne 'Skyline' } | ForEach-Object { Join-Path $skylineDir $_ }
    }
    $newestSource = Get-ChildItem -Path $sourceDir -Recurse -File -Include *.cs, *.resx, *.csproj -ErrorAction SilentlyContinue |
        Where-Object {
            $path = $_.FullName
            if ($path -match '\\(bin|obj)\\') { return $false }
            # For Skyline, drop anything under a sibling test project - those are not its sources.
            -not ($siblings | Where-Object { $path.StartsWith($_, [System.StringComparison]::OrdinalIgnoreCase) })
        } |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if (-not $newestSource) { return $false }
    $newestOutput = Get-ChildItem -Path $outputDir -File -Filter *.dll -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if ($newestOutput -and $newestSource.LastWriteTime -gt $newestOutput.LastWriteTime) {
        Write-Warning ("$project output looks stale: $($newestSource.Name) changed " +
            "$($newestSource.LastWriteTime.ToString('MM-dd HH:mm')) but the newest built assembly is " +
            "$($newestOutput.LastWriteTime.ToString('MM-dd HH:mm')). Staging it first so freshly " +
            "built projects win any shared file - rebuild $project if that is not intended.")
        return $true
    }
    return $false
}

# Order matters now that /XO is gone: the projects merge into one directory, so the last copy
# of a shared file wins. A project whose output predates its own sources cannot be the
# authority on a shared assembly - build.bat excludes TestPerf and TestTutorial from the
# standard build, so their output routinely carries dependencies several versions old, and
# staging them last is how a stale ClrMD overwrote the one the rebuilt projects had just
# staged. Stale first, freshly built last.
$toStage = @()
foreach ($project in $Projects) {
    $src = Get-ProjectOutput $project
    if (-not (Test-Path $src)) {
        Write-Warning "Skipping $project - no output at $src (build it first)."
        continue
    }
    $toStage += [pscustomobject]@{ Project = $project; Src = $src; Stale = (Test-OutputStale $project $src) }
}

foreach ($item in (@($toStage | Where-Object { $_.Stale }) + @($toStage | Where-Object { -not $_.Stale }))) {
    $project = $item.Project
    $src = $item.Src
    Write-Host "Staging $project  ($src)"
    # /E recurse (satellite resource dirs), /NP /NDL /NFL /NJH /NJS quiet. robocopy exit
    # codes 0-7 are success. No /XO: several projects merge into one staging directory and it
    # has to end up matching the build output, so a file that differs is replaced rather than
    # kept for being newer. Identical copies are still skipped on size and timestamp, so the
    # shared dependencies /XO was protecting are not recopied anyway.
    robocopy $src $StagingDir /E /NP /NDL /NFL /NJH /NJS | Out-Null
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
        robocopy $p.Src $p.Dst /E /NP /NDL /NFL /NJH /NJS | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed staging runtime $($p.Src) (exit $LASTEXITCODE)" }
    }
}

Write-Host ""
Write-Host "Staged net8 tests to: $StagingDir"
Write-Host "Run e.g.:  $StagingDir\TestRunner.exe test=<name> parallelmode=off"
