#requires -Version 7
<#
.SYNOPSIS
    TeamCity nightly entry point for CarafeSharp, run by pwiz_tools/Osprey/tctest.bat after
    Osprey's regression.

.DESCRIPTION
    The real-data checks too slow for every commit (the commits run build.ps1 and its
    TestRegressionSubset):
      1. build.ps1 -TestData Fetch -RequireData: fetches the test data it reads, then builds and
         runs every test, the Carafe parity tests included.
      2. regression.ps1 -Dataset Stellar: the full fine-tune on Stellar's export, against its golden.
      3. regression.ps1 -Leg Chained: Osprey's export of its committed Stellar subset, trained on.

    The test data goes in <Downloads>\Perftests\carafesharp, not <Downloads>\Perftests, unless
    CARAFESHARP_TESTDATA names a folder: a per-commit build on the same agent finds data in the
    default folder by itself, and would then run the parity tests on every commit.

    Each step runs whatever the one before did; the exit code is the first failure's.

.PARAMETER TeamCity
    Report each failure as a TeamCity build problem, and pass -TeamCity to build.ps1.
#>
param([switch]$TeamCity)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
. (Join-Path $root 'scripts/TestData.ps1')

if (-not $env:CARAFESHARP_TESTDATA) {
    $env:CARAFESHARP_TESTDATA = Join-Path (Join-Path (Get-TestDataDownloadsPath) 'Perftests') 'carafesharp'
    New-Item -ItemType Directory -Force -Path $env:CARAFESHARP_TESTDATA | Out-Null
}
Write-Host "CarafeSharp test data: $env:CARAFESHARP_TESTDATA"

$teamCityArgs = if ($TeamCity) { @('-TeamCity') } else { @() }
$steps = @(
    @{ Name = 'CarafeSharp tests with the test data'; Args = @('-File', (Join-Path $root 'build.ps1')) + $teamCityArgs +
        @('-TestData', 'Fetch', '-TestDataPackage', 'testfiles,export,regression-export', '-RequireData') },
    @{ Name = 'CarafeSharp regression (Stellar)'; Args = @('-File', (Join-Path $root 'regression.ps1'), '-Dataset', 'Stellar', '-NoBuild') },
    @{ Name = 'CarafeSharp regression (chained with Osprey)'; Args = @('-File', (Join-Path $root 'regression.ps1'), '-Leg', 'Chained', '-NoBuild') }
)
$exitCode = 0
foreach ($step in $steps) {
    if ($TeamCity) {
        Write-Host "##teamcity[progressMessage '$($step.Name)']"
    }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    & pwsh -NoProfile @($step.Args)
    $code = $LASTEXITCODE
    Write-Host ("{0}: exit {1} in {2:F1} min" -f $step.Name, $code, $clock.Elapsed.TotalMinutes)
    if ($code -ne 0) {
        if ($TeamCity) {
            Write-Host "##teamcity[buildProblem description='$($step.Name) failed (exit $code)']"
        }
        if ($exitCode -eq 0) {
            $exitCode = $code
        }
    }
}
exit $exitCode
