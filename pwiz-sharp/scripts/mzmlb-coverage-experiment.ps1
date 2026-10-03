<#
.SYNOPSIS
Iterative coverage experiment for the mzMLb test suite. Runs MsData.Tests + the
fast Thermo harness fixture under dotCover, focused on Pwiz.Data.MsData.MzMlb,
and writes a JSON report. The outer loop drops one mzMLb test at a time and
records line coverage so we can see which tests are actually load-bearing
(coverage drops) vs. which are subsumed by other tests (coverage unchanged).

.PARAMETER ExcludeTest
Optional MSTest method-name fragment to exclude from MsData.Tests via
`--filter "FullyQualifiedName!~..."`. Pass nothing for the baseline.

.PARAMETER OutDir
Where to drop the .dcvr snapshot + HTML/JSON report. Defaults to TestResults/
mzmlb-cov-<label>.
#>
#requires -Version 7.0
param(
    [string] $ExcludeTest = '',
    [string] $OutDir = ''
)
$ErrorActionPreference = 'Stop'

$repo = Resolve-Path "$PSScriptRoot/.."
$label = if ([string]::IsNullOrEmpty($ExcludeTest)) { 'baseline' } else { $ExcludeTest }
if ([string]::IsNullOrEmpty($OutDir)) {
    $OutDir = Join-Path $repo "TestResults/mzmlb-cov-$label"
}
if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
New-Item -ItemType Directory -Path $OutDir | Out-Null

# Filter: include MzMlb tests; if -ExcludeTest set, exclude it too.
$mzmlbFilter = 'FullyQualifiedName~MzMlb'
if (-not [string]::IsNullOrEmpty($ExcludeTest)) {
    $mzmlbFilter = "$mzmlbFilter&FullyQualifiedName!~$ExcludeTest"
}

# Focus the coverage filter on the MzMlb namespace + the MzmlReader/MzmlWriter
# touch-points where the external-source/sink hooks live.
$coverageFilters = '+:module=Pwiz.Data.MsData;+:class=Pwiz.Data.MsData.MzMlb.*;+:class=Pwiz.Data.MsData.Readers.MzMlbReaderAdapter;-:module=*.Tests'

$snapshot1 = Join-Path $OutDir 'mzmlb.dcvr'
$snapshot2 = Join-Path $OutDir 'thermo.dcvr'
$merged    = Join-Path $OutDir 'coverage.dcvr'
$report    = Join-Path $OutDir 'coverage.xml'

Write-Host "[$label] running MsData.Tests (filter: $mzmlbFilter)" -ForegroundColor Cyan
& dotnet dotcover dotnet --Output="$snapshot1" --Filters="$coverageFilters" -- `
    test "$repo/pwiz/test/MsData.Tests/MsData.Tests.csproj" -c Release `
        --no-build --no-restore --nologo `
        --filter "$mzmlbFilter" 2>&1 | Where-Object { $_ -match 'Passed|Failed|Error|test files matched|Total tests' } | ForEach-Object { Write-Host "  $_" }

Write-Host "[$label] running Thermo.Tests (fast fixture)" -ForegroundColor Cyan
# PowerShell's parameter binder mishandles `-p:foo=bar` on a `& cmd` line; build
# the argv as an array and splat with @args to bypass binder reinterpretation.
$thermoArgs = @(
    'dotcover', 'dotnet',
    "--Output=$snapshot2",
    "--Filters=$coverageFilters",
    '--',
    'test', "$repo/pwiz/test/Thermo.Tests/Thermo.Tests.csproj",
    '-c', 'Release',
    '--no-build', '--no-restore', '--nologo',
    '-p:IAgreeToVendorLicenses=true',
    '--filter', 'FullyQualifiedName~Reader_Thermo_FT_HCD_MSX'
)
& dotnet @thermoArgs 2>&1 | Where-Object { $_ -match 'Passed|Failed|Error|test files matched|Total tests' } | ForEach-Object { Write-Host "  $_" }

Write-Host "[$label] merging snapshots" -ForegroundColor Cyan
& dotnet dotcover merge --Source="$snapshot1;$snapshot2" --Output="$merged" 2>&1 |
    Where-Object { $_ -notmatch '^\s*$' } | ForEach-Object { Write-Host "  $_" }

Write-Host "[$label] generating XML report" -ForegroundColor Cyan
& dotnet dotcover report --Source="$merged" --Output="$report" --ReportType=XML `
    --HideAutoProperties 2>&1 |
    Where-Object { $_ -notmatch '^\s*$' } | ForEach-Object { Write-Host "  $_" }

# Extract per-class line coverage for MzMlb namespace.
[xml] $xml = Get-Content $report
$rows = @()
foreach ($asm in $xml.Root.Assembly) {
    foreach ($ns in $asm.Namespace | Where-Object { $_.Name -like '*MzMlb*' -or $_.Name -like '*Readers*' }) {
        foreach ($cls in $ns.Type) {
            if ($cls.Name -like '*MzMlb*') {
                $rows += [pscustomobject]@{
                    Class      = "$($ns.Name).$($cls.Name)"
                    Statements = [int]$cls.TotalStatements
                    Covered    = [int]$cls.CoveredStatements
                    Percent    = [double]$cls.CoveragePercent
                }
            }
        }
    }
}
$rows | Sort-Object Class | Format-Table -AutoSize | Out-String | Write-Host
$totalStmts = ($rows | Measure-Object Statements -Sum).Sum
$totalCov   = ($rows | Measure-Object Covered    -Sum).Sum
Write-Host ("[$label] MzMlb total: {0}/{1} statements covered ({2:0.0}%)" -f `
    $totalCov, $totalStmts, ($totalCov * 100.0 / [Math]::Max(1, $totalStmts))) -ForegroundColor Yellow
