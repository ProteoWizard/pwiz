<#
.SYNOPSIS
Iterate through every mzMLb test method, drop each one in turn, and report
how many statements coverage drops by. Identifies which tests are actually
load-bearing vs. fully subsumed by others (incl. the new harness round-trip).
#>
#requires -Version 7.0
$ErrorActionPreference = 'Stop'

$repo = Resolve-Path "$PSScriptRoot/.."

# Discover MzMlb test method names from the test source files (cheap parse:
# every `public void Foo()` inside an [TestMethod]).
$testFiles = @(
    "$repo/pwiz/test/MsData.Tests/MzMlbConnectionTests.cs",
    "$repo/pwiz/test/MsData.Tests/MzMlbReaderTests.cs",
    "$repo/pwiz/test/MsData.Tests/MzMlbRoundTripTests.cs"
)
$tests = @()
foreach ($f in $testFiles) {
    $lines = Get-Content $f
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*\[TestMethod\]' -and $i + 1 -lt $lines.Count) {
            $m = $lines[$i + 1] -match '^\s*public\s+void\s+(\w+)\s*\('
            if ($m) { $tests += $Matches[1] }
        }
    }
}

Write-Host "Discovered $($tests.Count) mzMLb tests:" -ForegroundColor Cyan
$tests | ForEach-Object { Write-Host "  $_" }
Write-Host ""

function Run-Cov($exclude) {
    $argList = @('-NoProfile', '-File', "$PSScriptRoot/mzmlb-coverage-experiment.ps1")
    if (-not [string]::IsNullOrEmpty($exclude)) {
        $argList += '-ExcludeTest'; $argList += $exclude
    }
    $output = & pwsh @argList 2>&1
    # Parse the "MzMlb total: X/Y statements covered (Z%)" line.
    $line = $output | Where-Object { $_ -match 'MzMlb total: (\d+)/(\d+)' } | Select-Object -Last 1
    if ($line -match 'MzMlb total: (\d+)/(\d+) statements covered \(([\d.]+)%\)') {
        return [pscustomobject]@{
            Excluded   = if ([string]::IsNullOrEmpty($exclude)) { '(baseline)' } else { $exclude }
            Covered    = [int]$Matches[1]
            Total      = [int]$Matches[2]
            Percent    = [double]$Matches[3]
        }
    }
    return [pscustomobject]@{
        Excluded = $exclude; Covered = -1; Total = -1; Percent = -1.0
    }
}

# 1. Baseline.
Write-Host "=== baseline (all tests) ===" -ForegroundColor Yellow
$baseline = Run-Cov ''
$baseline | Format-Table -AutoSize | Out-String | Write-Host

# 2. Drop each test in turn.
$results = @($baseline)
foreach ($t in $tests) {
    Write-Host "=== excluding $t ===" -ForegroundColor Yellow
    $r = Run-Cov $t
    $r | Add-Member -NotePropertyName 'DeltaStatements' `
                    -NotePropertyValue ($baseline.Covered - $r.Covered)
    $results += $r
    Write-Host "  delta: $($r.DeltaStatements) statements"
}

# 3. Summary.
Write-Host ""
Write-Host "=== SUMMARY (sorted by impact) ===" -ForegroundColor Green
$results | Sort-Object DeltaStatements -Descending | Format-Table Excluded, Covered, Total, Percent, DeltaStatements -AutoSize
