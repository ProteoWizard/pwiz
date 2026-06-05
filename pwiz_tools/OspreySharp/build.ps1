<#
.SYNOPSIS
    Build + test OspreySharp.  Self-contained entry point for local
    dev (via build.bat) and CI (via tcbuild.bat).

.DESCRIPTION
    Drives MSBuild on OspreySharp.sln, then runs the OspreySharp.Test
    suite via vstest.console.exe.  Optional dotCover wrap for coverage
    measurement, optional TeamCity service messages for CI reporting.

    Does NOT depend on the sibling ai/ checkout -- the pwiz repo can
    be built and tested standalone, which is what CI needs.  A
    superset script aimed at LLM-driven dev workflows (with line-
    ending fixes, ReSharper inspection, dataset-aware test runs)
    lives at ai/scripts/OspreySharp/Build-OspreySharp.ps1.

.PARAMETER Configuration
    Debug or Release.  Default Release (CI canonical).

.PARAMETER Framework
    Which target framework to test.  net8.0 (default), net472, or
    both.  The MSBuild step always builds every framework declared
    in the per-project files; this flag controls which framework's
    test DLLs get run.

.PARAMETER NoTests
    Build only.

.PARAMETER Coverage
    Wrap test execution in JetBrains dotCover.  Writes .dcvr
    coverage data under TestResults/.  Requires the dotcover
    global tool on PATH (install:
    dotnet tool install -g JetBrains.dotCover.GlobalTools).

.PARAMETER TeamCity
    Emit TeamCity service messages: progress lines during the
    build, vstest TRX import after tests, dotCover .dcvr import
    after coverage, and a buildProblem line on any failure.
    The agent's TeamCity runner consumes these automatically.

.PARAMETER Verbosity
    MSBuild verbosity (quiet|minimal|normal|detailed|diagnostic).
    Default minimal.

.EXAMPLE
    # Local dev
    .\build.bat

.EXAMPLE
    # Local dev: pick a specific framework, skip tests
    .\build.bat -Framework net472 -NoTests

.EXAMPLE
    # TeamCity (what tcbuild.bat invokes)
    .\build.ps1 -TeamCity -Coverage -Configuration Release -Framework net8.0
#>
param(
    [ValidateSet('Debug','Release')] [string]$Configuration = 'Release',
    [ValidateSet('net8.0','net472','both')] [string]$Framework = 'net8.0',
    [switch]$NoTests,
    [switch]$Coverage,
    [switch]$TeamCity,
    [ValidateSet('quiet','minimal','normal','detailed','diagnostic')]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptRoot = Split-Path -Parent $PSCommandPath
$sln        = Join-Path $scriptRoot 'OspreySharp.sln'
$platform   = 'x64'
if (-not (Test-Path $sln)) {
    Write-Error "OspreySharp.sln not found at $sln"
    exit 2
}

# --- TeamCity service-message helpers -----------------------------------
function Format-TcMessage([string]$s) {
    # https://www.jetbrains.com/help/teamcity/service-messages.html#Escaped+Values
    if ($null -eq $s) { return '' }
    return $s.Replace('|', '||').Replace("'", "|'").Replace("`n", '|n').Replace("`r", '|r').Replace('[', '|[').Replace(']', '|]')
}
function Write-Progress-Tc([string]$msg) {
    if ($TeamCity) {
        Write-Host ("##teamcity[progressMessage '{0}']" -f (Format-TcMessage $msg))
    } else {
        Write-Host "==> $msg" -ForegroundColor Cyan
    }
}
function Write-Problem-Tc([string]$msg) {
    if ($TeamCity) {
        Write-Host ("##teamcity[buildProblem description='{0}']" -f (Format-TcMessage $msg))
    }
    Write-Host "ERROR: $msg" -ForegroundColor Red
}

# --- Tool discovery -----------------------------------------------------
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    Write-Problem-Tc "vswhere.exe not found (install Visual Studio Installer)"
    exit 2
}
$vsInstall = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vsInstall) {
    Write-Problem-Tc "vswhere found no VS installation with MSBuild component"
    exit 2
}
$msbuild = Join-Path $vsInstall 'MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    Write-Problem-Tc "MSBuild not found at $msbuild"
    exit 2
}
$vstest = $null
if (-not $NoTests) {
    $candidates = @(
        (Join-Path $vsInstall 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'),
        (Join-Path $vsInstall 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe')
    )
    foreach ($c in $candidates) { if (Test-Path $c) { $vstest = $c; break } }
    if (-not $vstest) {
        Write-Problem-Tc "vstest.console.exe not found under $vsInstall"
        exit 2
    }
}
$dotcover = $null
if ($Coverage) {
    $cmd = Get-Command dotcover -ErrorAction SilentlyContinue
    if (-not $cmd) {
        Write-Problem-Tc "dotcover not on PATH (install JetBrains.dotCover.GlobalTools)"
        exit 2
    }
    $dotcover = $cmd.Source
}

# --- Build --------------------------------------------------------------
Write-Progress-Tc "Building OspreySharp.sln ($Configuration|$platform)"
$buildStart = Get-Date
$buildArgs = @(
    $sln,
    '/restore',
    "/p:Configuration=$Configuration",
    "/p:Platform=$platform",
    '/nologo',
    "/verbosity:$Verbosity"
)
& $msbuild @buildArgs
$buildExit = $LASTEXITCODE
$buildSec = ((Get-Date) - $buildStart).TotalSeconds
if ($buildExit -ne 0) {
    Write-Problem-Tc ("MSBuild failed in {0:F1}s (exit {1})" -f $buildSec, $buildExit)
    exit $buildExit
}
Write-Host ("Build succeeded in {0:F1}s" -f $buildSec) -ForegroundColor Green

if ($NoTests) {
    Write-Host "Skipping tests (-NoTests)" -ForegroundColor Yellow
    exit 0
}

# --- Test ---------------------------------------------------------------
$testFrameworks = if ($Framework -eq 'both') { @('net472','net8.0') } else { @($Framework) }
$trxDir = Join-Path $scriptRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $trxDir | Out-Null

$overallTestExit = 0
foreach ($fw in $testFrameworks) {
    $testDll = Join-Path $scriptRoot "OspreySharp.Test\bin\$platform\$Configuration\$fw\OspreySharp.Test.dll"
    if (-not (Test-Path $testDll)) {
        Write-Problem-Tc "Test DLL not found at $testDll"
        $overallTestExit = 2
        continue
    }

    $trxName = "OspreySharp.Test-$Configuration-$fw.trx"
    $trxPath = Join-Path $trxDir $trxName
    $vstestArgs = @(
        $testDll,
        "/Platform:$platform",
        "/Logger:trx;LogFileName=$trxName",
        "/ResultsDirectory:$trxDir"
    )

    $testStart = Get-Date
    if ($Coverage) {
        $dcvrPath = Join-Path $trxDir "OspreySharp.Test-$Configuration-$fw.dcvr"
        Write-Progress-Tc "Running tests under dotCover ($fw)"
        & $dotcover cover-dotnet `
            --Output=$dcvrPath `
            --Filters='+:OspreySharp.*;+:OspreySharp.Core;+:OspreySharp.ML;+:OspreySharp.Chromatography;+:OspreySharp.FDR;+:OspreySharp.IO;+:OspreySharp.Scoring;+:OspreySharp.Tasks' `
            --AttributeFilters='System.CodeDom.Compiler.GeneratedCodeAttribute' `
            -- `
            $vstest @vstestArgs
        $exit = $LASTEXITCODE
        if ($TeamCity -and (Test-Path $dcvrPath)) {
            Write-Host ("##teamcity[importData type='dotNetCoverage' tool='dotcover' path='{0}']" -f (Format-TcMessage $dcvrPath))
        }
    } else {
        Write-Progress-Tc "Running tests ($fw)"
        & $vstest @vstestArgs
        $exit = $LASTEXITCODE
    }
    $testSec = ((Get-Date) - $testStart).TotalSeconds

    if ($TeamCity -and (Test-Path $trxPath)) {
        Write-Host ("##teamcity[importData type='vstest' path='{0}']" -f (Format-TcMessage $trxPath))
    }
    if ($exit -eq 0) {
        Write-Host ("Tests ($fw) passed in {0:F1}s" -f $testSec) -ForegroundColor Green
    } else {
        Write-Problem-Tc ("Tests ($fw) FAILED in {0:F1}s (exit {1})" -f $testSec, $exit)
        $overallTestExit = $exit
    }
}

exit $overallTestExit
