<#!
.SYNOPSIS
    Synchronize ReSharper .DotSettings across Skyline, SkylineBatch, and AutoQC.

.DESCRIPTION
    Copies Skyline.sln.DotSettings as the canonical baseline to SkylineBatch.sln.DotSettings and AutoQC.sln.DotSettings
    applying intentional severity overrides (currently LocalizableElement: WARNING -> HINT) for batch tools.
    Skips rewrite if target already matches intended content to avoid unnecessary Git diffs.

.NOTES
    Run early in each build script to keep inspection configuration aligned.
    Extend $overrides map for future tool-specific severity adjustments.
    Safe for repeated invocation.

#>
param(
    [switch]$VerboseOutput = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }
function Write-Change($msg) { Write-Host $msg -ForegroundColor Green }
function Write-Skip($msg) { Write-Host $msg -ForegroundColor Yellow }
function Write-ErrorLine($msg) { Write-Host $msg -ForegroundColor Red }

# Resolve paths relative to script location
$scriptRoot = Split-Path -Parent $PSCommandPath
# script is in pwiz_tools/Skyline/ai/scripts; baseline is one directory up + Skyline.sln.DotSettings
$skylineRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)
$baselinePath = Join-Path $skylineRoot 'Skyline.sln.DotSettings'

if (-not (Test-Path $baselinePath)) {
    Write-ErrorLine "Baseline DotSettings not found: $baselinePath"
    exit 1
}

$targets = @(
    @{ Name = 'SkylineBatch'; Path = Join-Path $skylineRoot 'Executables/SkylineBatch/SkylineBatch.sln.DotSettings'; ApplyOverrides = $true },
    @{ Name = 'AutoQC';       Path = Join-Path $skylineRoot 'Executables/AutoQC/AutoQC.sln.DotSettings';             ApplyOverrides = $true }
)

# Map of overrides (regex pattern => replacement) applied only when ApplyOverrides = $true
# Intent: Lower localization noise for batch tools (treat as HINT rather than WARNING)
$overrides = @(
    @{ Pattern = '(?m)(<s:String x:Key="/Default/CodeInspection/Highlighting/InspectionSeverities/=LocalizableElement/@EntryIndexedValue">)WARNING(<[/]s:String>)'; Replacement = '$1HINT$2' }
)

$baselineContent = Get-Content -LiteralPath $baselinePath -Raw

foreach ($t in $targets) {
    $targetPath = $t.Path
    $name = $t.Name
    if (-not (Test-Path $targetPath)) {
        Write-Skip "Target missing ($name) - creating from baseline"
        $newContent = $baselineContent
        if ($t.ApplyOverrides) {
            foreach ($ov in $overrides) { $newContent = [Regex]::Replace($newContent, $ov.Pattern, $ov.Replacement) }
        }
        $newContent | Set-Content -LiteralPath $targetPath -Encoding UTF8
        Write-Change "Created $name DotSettings"
        continue
    }

    $current = Get-Content -LiteralPath $targetPath -Raw
    $desired = $baselineContent
    if ($t.ApplyOverrides) {
        foreach ($ov in $overrides) { $desired = [Regex]::Replace($desired, $ov.Pattern, $ov.Replacement) }
    }

    if ($current -eq $desired) {
        if ($VerboseOutput) { Write-Skip "No change needed for $name" }
        continue
    }

    $backupPath = "$targetPath.bak"
    $current | Set-Content -LiteralPath $backupPath -Encoding UTF8
    $desired | Set-Content -LiteralPath $targetPath -Encoding UTF8
    Write-Change "Updated $name DotSettings (backup saved: $backupPath)"

    if ($VerboseOutput) {
        # Simple diff summary: count differing lines
        $currentLines = $current -split "`r?`n"
        $desiredLines = $desired -split "`r?`n"
        $diffCount = 0
        for ($i=0; $i -lt [Math]::Max($currentLines.Length, $desiredLines.Length); $i++) {
            if ($currentLines[$i] -ne $desiredLines[$i]) { $diffCount++ }
        }
        Write-Info "Changed lines (approx): $diffCount"
    }
}

Write-Info 'DotSettings synchronization complete.'
