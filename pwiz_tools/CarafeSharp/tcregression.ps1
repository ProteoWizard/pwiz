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
      3. regression.ps1 -Dataset StellarSubset twice, each in a process of its own: the two runs'
         models and training tables must be the same bytes, so a rerun on this agent repeats.

    Osprey's export feeding CarafeSharp's training is checked by Osprey's own tests, which run the
    one command with --training-input on Osprey's committed subset (SubsetPipelineTest).

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
    @{ Name = 'CarafeSharp regression (Stellar)'; Args = @('-File', (Join-Path $root 'regression.ps1'), '-Dataset', 'Stellar', '-NoBuild') }
)
$repeatFolder = Join-Path $root ('TestResults/regression-repeat-' + (Get-Date).ToString('yyyyMMdd-HHmmss'))
foreach ($name in 'first', 'second') {
    $steps += @{ Name = "CarafeSharp regression (StellarSubset, $name of two)"; Args = @('-File', (Join-Path $root 'regression.ps1'),
        '-Dataset', 'StellarSubset', '-NoBuild', '-WorkDir', $repeatFolder, '-RunName', $name) }
}
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

# The two StellarSubset runs: the same models and training tables, byte for byte.
$runs = @(Get-ChildItem -LiteralPath $repeatFolder -Directory -ErrorAction SilentlyContinue | Sort-Object Name)
$problem = $null
if ($runs.Count -ne 2) {
    $problem = "expected two StellarSubset runs in $repeatFolder, found $($runs.Count)"
} else {
    foreach ($file in 'ms2.safetensors', 'rt.safetensors', 'fragment_intensity_df.tsv', 'fragment_intensity_valid.tsv', 'psm_pdv.txt', 'rt_train_data.tsv') {
        $hashes = @($runs | ForEach-Object {
            $path = Join-Path (Join-Path $_.FullName 'out') $file
            if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'missing' }
        })
        Write-Host ("  repeat {0}: {1}" -f $file, $(if ($hashes[0] -eq $hashes[1]) { 'SAME' } else { "DIFFERS ($($hashes -join ' / '))" }))
        if ($hashes[0] -ne $hashes[1] -or $hashes[0] -eq 'missing') {
            $problem = "the two StellarSubset runs differ in $file"
        }
    }
}
if ($problem) {
    Write-Host "CarafeSharp repeat: $problem" -ForegroundColor Red
    if ($TeamCity) {
        Write-Host "##teamcity[buildProblem description='CarafeSharp repeat: $problem']"
    }
    if ($exitCode -eq 0) {
        $exitCode = 1
    }
}
exit $exitCode
