#requires -Version 7.0
<#
.SYNOPSIS
Per-project parallel `dotnet test` runner that serializes per-project stdout to
prevent TeamCity service-message corruption.

.DESCRIPTION
Replaces `dotnet test Pwiz.sln` (which runs per-project tests in parallel and
byte-interleaves their stdout) with this pattern:
  1. Spawn one `dotnet test <project>` job per test csproj, all in parallel.
  2. Each job's stdout/stderr is redirected to its own log file under
     TestResults/test-stdout/<project>.log — no shared write target, no
     interleaving.
  3. Wait for every job to finish.
  4. Emit each per-project log file to this script's stdout in declared order.

Net effect: wall-clock matches the previous parallel run, but TeamCity's parser
sees one project's full ##teamcity[testStarted/testFinished ...] stream
contiguously before the next project's stream begins. Counts stay stable.

When -CoverageSnapshotDir is supplied, each `dotnet test` invocation runs under
`dotnet dotcover dotnet -- test ...` writing a per-project snapshot, and the
script merges them at the end via `dotnet dotcover merge` — yielding one
coverage.dcvr identical in shape to the previous single-invocation flow.

.PARAMETER TestProjects
Comma- or whitespace-separated paths to test .csproj files to run, relative to
the working directory. (Single-string form is necessary because `pwsh -File`
doesn't split array values from the command line.)

.PARAMETER Configuration
Debug or Release. Maps to `-p:Configuration=...`.

.PARAMETER IAgreeToVendorLicenses
When true, adds `-p:IAgreeToVendorLicenses=true`.

.PARAMETER AutomatedBuild
When true, adds `-p:AutomatedBuild=true`.

.PARAMETER TestResultsDir
Directory the trx logger writes to. Per-project stdout logs go under
$TestResultsDir/test-stdout/.

.PARAMETER CoverageSnapshotDir
When set, each project runs under dotCover and a per-project snapshot is
written here. After all projects finish, the script merges them into
`coverage.dcvr` in this directory. When unset, dotCover is skipped entirely.

.PARAMETER CoverageFilters
dotCover --Filters string (e.g. "+:module=Pwiz.*;-:module=*.Tests"). Required
when CoverageSnapshotDir is set.
#>

param(
    [Parameter(Mandatory)] [string] $TestProjects,
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [switch] $IAgreeToVendorLicenses,
    [switch] $AutomatedBuild,
    [string] $TestResultsDir = '',
    [string] $CoverageSnapshotDir = '',
    [string] $CoverageFilters = ''
)

$ErrorActionPreference = 'Stop'

$projectList = $TestProjects -split '[,\s]+' | Where-Object { $_ }

if ([string]::IsNullOrEmpty($TestResultsDir)) {
    $TestResultsDir = Join-Path (Get-Location) 'TestResults'
}
$LogDir = Join-Path $TestResultsDir 'test-stdout'
foreach ($d in @($TestResultsDir, $LogDir)) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

$msbuildProps = @("-p:Configuration=$Configuration")
if ($IAgreeToVendorLicenses) { $msbuildProps += '-p:IAgreeToVendorLicenses=true' }
if ($AutomatedBuild) { $msbuildProps += '-p:AutomatedBuild=true' }

$testLoggers = @('--logger:trx', "--results-directory:$TestResultsDir")
if ($env:TEAMCITY_VERSION) {
    $testLoggers += '--logger:teamcity'
} else {
    $testLoggers += '--logger:console;verbosity=normal'
}

$useCoverage = -not [string]::IsNullOrEmpty($CoverageSnapshotDir)
if ($useCoverage) {
    if ([string]::IsNullOrEmpty($CoverageFilters)) {
        throw "CoverageFilters required when CoverageSnapshotDir is set."
    }
    if (-not (Test-Path $CoverageSnapshotDir)) {
        New-Item -ItemType Directory -Path $CoverageSnapshotDir -Force | Out-Null
    }
}

$jobs = foreach ($project in $projectList) {
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
    $logPath = Join-Path $LogDir "$projectName.log"
    $snapshotPath = if ($useCoverage) { Join-Path $CoverageSnapshotDir "$projectName.dcvr" } else { '' }

    Start-Job -Name $projectName -ScriptBlock {
        param($projectPath, $logPath, $msbuildProps, $testLoggers, $snapshotPath, $coverageFilters, $workingDir)
        Set-Location $workingDir

        $argList = @('test', $projectPath, '--no-build') + $msbuildProps + $testLoggers
        try {
            if ([string]::IsNullOrEmpty($snapshotPath)) {
                & dotnet @argList *>&1 | Out-File -FilePath $logPath -Encoding utf8
            } else {
                $coverArgs = @(
                    'dotcover', 'dotnet',
                    "--Output=$snapshotPath",
                    "--Filters=$coverageFilters",
                    '--ReturnTargetExitCode',
                    '--'
                ) + $argList
                & dotnet @coverArgs *>&1 | Out-File -FilePath $logPath -Encoding utf8
            }
            $LASTEXITCODE
        } catch {
            "ERROR: $($_.Exception.Message)" | Add-Content -Path $logPath -Encoding utf8
            1
        }
    } -ArgumentList $project, $logPath, $msbuildProps, $testLoggers, $snapshotPath, $CoverageFilters, (Get-Location).Path
}

$results = $jobs | Wait-Job | ForEach-Object {
    $exitCode = Receive-Job $_
    [pscustomobject]@{
        Name     = $_.Name
        ExitCode = ($exitCode | Select-Object -Last 1)
        LogPath  = Join-Path $LogDir "$($_.Name).log"
    }
}
$jobs | Remove-Job

foreach ($result in $results) {
    Write-Host "##teamcity[blockOpened name='$($result.Name)']"
    if (Test-Path $result.LogPath) {
        Get-Content -Path $result.LogPath -Raw | Write-Host
    } else {
        Write-Host "(no log produced)"
    }
    Write-Host "##teamcity[blockClosed name='$($result.Name)']"
}

if ($useCoverage) {
    $snapshots = $results | ForEach-Object { Join-Path $CoverageSnapshotDir "$($_.Name).dcvr" } | Where-Object { Test-Path $_ }
    if ($snapshots.Count -gt 0) {
        $merged = Join-Path $CoverageSnapshotDir 'coverage.dcvr'
        $sourceArgs = $snapshots | ForEach-Object { "--Source=$_" }
        & dotnet dotcover merge @sourceArgs "--Output=$merged"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "##teamcity[message text='dotCover merge failed (exit $LASTEXITCODE) — per-project snapshots remain in $CoverageSnapshotDir' status='WARNING']"
        }
    }
}

$failed = $results | Where-Object { $_.ExitCode -ne 0 }
if ($failed) {
    foreach ($f in $failed) {
        Write-Host "##teamcity[message text='$($f.Name) failed (exit $($f.ExitCode))' status='ERROR']"
    }
    exit 1
}
exit 0
