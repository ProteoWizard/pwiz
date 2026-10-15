<#
.SYNOPSIS
    Export a Skyline document's per-precursor quant to SWATHbenchmark.csv and run
    the LFQbench R pipeline (Navarro et al. 2016 style) to produce per-species
    log2 fold-change plots — for the ProteoBench DIA-LFQ AIF benchmark (PXD028735).

.DESCRIPTION
    Two-stage pipeline:
      1. SkylineCmd  → SWATHbenchmark.csv using SWATHbenchmark_report.skyr
      2. Rscript     → generateReport.R with species_mix = HYE_PROTEOBENCH
    LFQbench writes its plots (MA scatter + marginal box plots, log2-ratio density,
    accuracy/precision summaries) into subfolders of the output directory.

.PARAMETER SkyFile
    Path to the imported Skyline document (.sky). The cached perftest doc lives at
    %LOCALAPPDATA%\SkylinePerfTests\DiannPerfDoc-stable\DiannSearchPerf.sky.

.PARAMETER OutDir
    Directory where SWATHbenchmark.csv is written and LFQbench creates its
    plot subdirectories. Defaults to a sibling of the .sky file.

.PARAMETER SkylineCmd
    Path to SkylineCmd.exe. Defaults to the local debug build.

.PARAMETER Rscript
    Path to Rscript.exe. Defaults to the R 4.2 install.

.EXAMPLE
    pwsh -File ./pwiz_tools/Skyline/scripts/Generate-LFQbench-ProteoBench.ps1 `
        -SkyFile "$env:LOCALAPPDATA\SkylinePerfTests\DiannPerfDoc-stable\DiannSearchPerf.sky"

.NOTES
    Prerequisite (one-time):
      - R (>= 4.0)
      - LFQbench package: install via devtools::install_github("IFIproteomics/LFQbench")
        (the bundled install_R_packages_fresh.R uses deprecated APIs — install
        manually with modern devtools.)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$SkyFile,

    [string]$OutDir,

    [string]$SkylineCmd = "$PSScriptRoot\..\bin\x64\Debug\SkylineCmd.exe",

    [string]$Rscript = "C:\Program Files\R\R-4.2.0\bin\Rscript.exe"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SkyFile)) {
    throw "Skyline document not found: $SkyFile"
}
if (-not (Test-Path $SkylineCmd)) {
    throw "SkylineCmd not found at $SkylineCmd"
}
if (-not (Test-Path $Rscript)) {
    throw "Rscript not found at $Rscript"
}

# Default the output to a sibling of the .sky file.
if (-not $OutDir) {
    $skyDir = Split-Path -Parent $SkyFile
    $OutDir = Join-Path $skyDir 'LFQbench-output'
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$skyrPath = Join-Path $PSScriptRoot 'SWATHbenchmark_report.skyr'
$reportPath = Join-Path $OutDir 'SWATHbenchmark.csv'

Write-Host "[1/2] Exporting SWATHbenchmark.csv from $SkyFile" -ForegroundColor Cyan
& $SkylineCmd `
    --in="$SkyFile" `
    --report-add="$skyrPath" `
    --report-conflict-resolution=overwrite `
    --report-name="SWATHbenchmark" `
    --report-file="$reportPath" `
    --report-format=csv
if ($LASTEXITCODE -ne 0) { throw "SkylineCmd report export failed (exit $LASTEXITCODE)" }
Write-Host "  wrote $reportPath" -ForegroundColor Gray

$rScript = Join-Path $PSScriptRoot 'generateReport.R'
Write-Host "[2/2] Running LFQbench: $rScript $OutDir HYE_PROTEOBENCH" -ForegroundColor Cyan
& $Rscript $rScript $OutDir HYE_PROTEOBENCH
if ($LASTEXITCODE -ne 0) { throw "Rscript exited $LASTEXITCODE" }

Write-Host ""
Write-Host "LFQbench plots in $OutDir (look for *.pdf and subdirectories)" -ForegroundColor Green
