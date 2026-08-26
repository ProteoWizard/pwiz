<#
.SYNOPSIS
    Stage the net8 Skyline + test-project build outputs into a single directory.

.DESCRIPTION
    The legacy Jam build dropped Skyline-daily.exe, TestRunner.exe and every test DLL into
    one shared bin (pwiz_tools\Skyline\bin\x64\<Config>). The net8 SDK build gives each project
    its own bin\<Config>\net8.0-windows, so nothing sees the others. TestRunner (and its
    container workers) load Skyline + the test DLLs from one directory, so assemble them here.

    This is a thin wrapper. The staging itself lives in TestRunnerLib's TestStager, which
    SkylineTester also calls, so there is ONE implementation rather than one per caller that can
    drift. It used to be implemented here in PowerShell, which meant every test run in
    SkylineTester shelled out to a script: its colored warnings arrived as escape codes, its
    robocopy retried a locked file a million times at thirty second intervals - indistinguishable
    from a hang - and reading its output streams could deadlock.

.EXAMPLE
    pwsh -File .\Stage-Net8Tests.ps1 -Configuration Debug
#>
param(
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$skylineDir = $PSScriptRoot

# Run the stager out of the build output, never the staged copy: staging with a stale stager is
# how staging quietly stops keeping up with its own fixes.
#
# Platform-aware, and newest wins. Visual Studio (and Build-Skyline.ps1) build x64 into
# bin\x64\<Config>\<TFM>, while a plain "dotnet build" with no platform writes
# bin\<Config>\<TFM>. Picking one blindly finds a binary from the OTHER build whenever an
# older one was left there - which is worse than finding none, because staging then runs a
# stager that does not match the source and fails on something unrelated to staging.
$stagerCandidates = @(
    (Join-Path $skylineDir "TestRunner\bin\x64\$Configuration\net8.0-windows\TestRunner.exe"),
    (Join-Path $skylineDir "TestRunner\bin\$Configuration\net8.0-windows\TestRunner.exe")
) | Where-Object { Test-Path $_ }

$stager = $stagerCandidates | Sort-Object { (Get-Item $_).LastWriteTimeUtc } -Descending | Select-Object -First 1
if (-not $stager) {
    throw ("TestRunner.exe was not found under TestRunner\bin (x64 or AnyCPU) for $Configuration. " +
           "Build TestRunner ($Configuration) first.")
}

& $stager stage=1 configuration=$Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Staging failed (exit $LASTEXITCODE)."
}
