<#
.SYNOPSIS
    Audits lines of code in the ProteoWizard/Skyline project.

.DESCRIPTION
    Uses cloc with CSV output to count actual source code lines (C#, C++, C, headers).
    Categorizes each file by its path to accurately separate:
    - Team code vs third-party/vendored code
    - Core vs test code
    - Different project areas

.PARAMETER Drilldown
    Show one extra level of detail - breakdown by subdirectory within each category.

.EXAMPLE
    pwsh -Command "& './ai/scripts/audit-loc.ps1'"

.EXAMPLE
    pwsh -Command "& './ai/scripts/audit-loc.ps1' -Drilldown"

.NOTES
    Requires cloc: winget install AlDanial.Cloc
    Report saved to ai/.tmp/loc-audit-YYYYMMDD-HHMM.md
#>

[CmdletBinding()]
param(
    [switch]$Drilldown
)

$ErrorActionPreference = "Stop"

# Find repository root
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    Write-Error "Not in a git repository"
    exit 1
}

# Check for cloc
$clocCmd = Get-Command cloc -ErrorAction SilentlyContinue
if (-not $clocCmd) {
    Write-Error @"
cloc is not installed. Install with:
    winget install AlDanial.Cloc
Then restart your shell.
"@
    exit 1
}

Write-Host "Lines of Code Audit" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot"
Write-Host ""

$codeLangs = "C#,C# Designer,C++,C,C/C++ Header"

function Format-Number { param([int]$n) return $n.ToString("N0") }

# Categorization rules - order matters (first match wins)
# Returns: category name
function Get-FileCategory {
    param([string]$FilePath)

    $path = $FilePath.Replace('\', '/').ToLower()

    # Skip build artifacts
    if ($path -match '/bin/' -or $path -match '/obj/' -or $path -match '/testresults/') {
        return "Skip"
    }

    # === Third-party / Vendored ===

    # Hardklor (git submodule in Executables)
    if ($path -match 'executables/hardklor/') {
        return "ThirdParty-Hardklor"
    }

    # BullseyeSharp in Executables (git submodule)
    if ($path -match 'executables/bullseyesharp/') {
        return "ThirdParty-BullseyeSharp"
    }

    # ZedGraph
    if ($path -match 'shared/zedgraph/') {
        return "ThirdParty-ZedGraph"
    }

    # alglib
    if ($path -match 'shared/common/dataanalysis/alglib/') {
        return "ThirdParty-alglib"
    }

    # === Test Code ===

    # Test projects in Executables
    if ($path -match 'executables/[^/]*test/') {
        return "Test-Executables"
    }

    # Skyline test directories
    if ($path -match 'skyline/(test|testfunctional|testtutorial|testutil|testdata|testrunner|testrunnerlib|testperf|testconnected|commontest|skylinetester|skylinenightly|skylinenightlyshim)/') {
        return "Test-Skyline"
    }

    # === Executables (team code) ===
    if ($path -match 'skyline/executables/') {
        return "Executables"
    }

    # === Shared Libraries ===
    # BiblioSpec is the Skyline library builder - integral to Skyline
    if ($path -match 'shared/' -or $path -match 'pwiz_tools/bibliospec/') {
        return "Shared"
    }

    # === Skyline Core ===
    if ($path -match 'skyline/') {
        return "Core"
    }

    # === ProteoWizard C++ ===
    if ($path -match '^pwiz/') {
        return "PwizCpp"
    }

    # === Bumbershoot (not team-maintained) ===
    if ($path -match 'pwiz_tools/bumbershoot/') {
        return "Bumbershoot"
    }

    # === Pwiz Tools (MSConvertGUI, SeeMS, etc. - in Pwiz installer) ===
    if ($path -match 'pwiz_tools/') {
        return "PwizTools"
    }

    return "Other"
}

# Extract subdirectory for drilldown - returns the relevant folder name within the category
function Get-FileSubdir {
    param([string]$FilePath, [string]$Category)

    $path = $FilePath.Replace('\', '/').ToLower()

    switch -Regex ($Category) {
        '^Core$' {
            # Extract folder after skyline/ (e.g., Model, Controls, EditUI)
            if ($path -match 'skyline/([^/]+)/') { return $matches[1] }
            return "(root)"
        }
        '^Shared$' {
            # BiblioSpec or Shared subfolder
            if ($path -match 'pwiz_tools/bibliospec/') { return "BiblioSpec" }
            if ($path -match 'shared/([^/]+)/') { return $matches[1] }
            return "(root)"
        }
        '^Test-Skyline$' {
            # The test directory name itself (Test, TestFunctional, etc.)
            if ($path -match 'skyline/([^/]+)/') { return $matches[1] }
            return "(root)"
        }
        '^Test-Executables$' {
            # Test project name under Executables
            if ($path -match 'executables/([^/]+)/') { return $matches[1] }
            return "(root)"
        }
        '^Executables$' {
            # Tool name under Executables
            if ($path -match 'executables/([^/]+)/') { return $matches[1] }
            return "(root)"
        }
        '^PwizCpp$' {
            # Folder under pwiz/
            if ($path -match '^pwiz/([^/]+)/') { return $matches[1] }
            return "(root)"
        }
        '^PwizTools$' {
            # Tool under pwiz_tools/
            if ($path -match 'pwiz_tools/([^/]+)/') { return $matches[1] }
            return "(root)"
        }
        '^Bumbershoot$' {
            # Tool under Bumbershoot/
            if ($path -match 'bumbershoot/([^/]+)/') { return $matches[1] }
            return "(root)"
        }
        default { return "(other)" }
    }
}

# Initialize category totals
$categories = @{}
$categoryOrder = @(
    "Core", "Shared", "Test-Skyline", "Test-Executables", "Executables",
    "PwizCpp", "PwizTools", "Bumbershoot",
    "ThirdParty-ZedGraph", "ThirdParty-alglib", "ThirdParty-Hardklor", "ThirdParty-BullseyeSharp",
    "Other", "Skip"
)
foreach ($cat in $categoryOrder) {
    $categories[$cat] = @{ Files = 0; Blank = 0; Comment = 0; Code = 0; Languages = @{}; Subdirs = @{} }
}

Write-Host "Running cloc on pwiz_tools..." -ForegroundColor Gray
$pwizToolsOutput = & cloc "pwiz_tools" --csv --by-file --include-lang="$codeLangs" --exclude-dir=obj,bin,TestResults --quiet 2>$null

Write-Host "Running cloc on pwiz/..." -ForegroundColor Gray
$pwizOutput = & cloc "pwiz" --csv --by-file --include-lang="$codeLangs" --exclude-dir=obj,bin,TestResults --quiet 2>$null

# Combine outputs
$allOutput = @()
if ($pwizToolsOutput) { $allOutput += $pwizToolsOutput }
if ($pwizOutput) { $allOutput += $pwizOutput }

Write-Host "Categorizing files..." -ForegroundColor Gray

$fileCount = 0
foreach ($line in $allOutput) {
    # Skip header and empty lines
    if ($line -match '^language,' -or [string]::IsNullOrWhiteSpace($line)) { continue }

    # Parse CSV: language,filename,blank,comment,code
    $parts = $line -split ','
    if ($parts.Count -lt 5) { continue }

    $lang = $parts[0]
    $filename = $parts[1]
    $blank = [int]$parts[2]
    $comment = [int]$parts[3]
    $code = [int]$parts[4]

    $category = Get-FileCategory -FilePath $filename

    $categories[$category].Files++
    $categories[$category].Blank += $blank
    $categories[$category].Comment += $comment
    $categories[$category].Code += $code

    # Track by language within category
    if (-not $categories[$category].Languages.ContainsKey($lang)) {
        $categories[$category].Languages[$lang] = @{ Files = 0; Blank = 0; Comment = 0; Code = 0 }
    }
    $categories[$category].Languages[$lang].Files++
    $categories[$category].Languages[$lang].Blank += $blank
    $categories[$category].Languages[$lang].Comment += $comment
    $categories[$category].Languages[$lang].Code += $code

    # Track by subdirectory for drilldown
    $subdir = Get-FileSubdir -FilePath $filename -Category $category
    if (-not $categories[$category].Subdirs.ContainsKey($subdir)) {
        $categories[$category].Subdirs[$subdir] = @{ Files = 0; Blank = 0; Comment = 0; Code = 0 }
    }
    $categories[$category].Subdirs[$subdir].Files++
    $categories[$category].Subdirs[$subdir].Blank += $blank
    $categories[$category].Subdirs[$subdir].Comment += $comment
    $categories[$category].Subdirs[$subdir].Code += $code

    $fileCount++
}

Write-Host "Processed $fileCount files" -ForegroundColor Gray
Write-Host ""

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

# Calculate summary totals
$skylineCore = $categories['Core'].Code
$shared = $categories['Shared'].Code
$skylineTests = $categories['Test-Skyline'].Code + $categories['Test-Executables'].Code
$executables = $categories['Executables'].Code
$pwizCpp = $categories['PwizCpp'].Code
$pwizTools = $categories['PwizTools'].Code
$bumbershoot = $categories['Bumbershoot'].Code

$slnCoreTotal = $skylineCore + $shared
$slnTotal = $slnCoreTotal + $skylineTests
$skylineTotal = $slnTotal + $executables

$pwizTotal = $pwizCpp + $pwizTools

$thirdPartyTotal = $categories['ThirdParty-ZedGraph'].Code +
                   $categories['ThirdParty-alglib'].Code +
                   $categories['ThirdParty-Hardklor'].Code +
                   $categories['ThirdParty-BullseyeSharp'].Code

$teamTotal = $skylineTotal + $pwizTotal

# Build report
$report = @"
# Lines of Code Audit

Generated: $timestamp

## Summary

This report counts **actual source code** only (C#, C++, C, C/C++ headers).
Data files (XML, CSV, JSON, HTML, etc.) are excluded.
Each file is categorized by its path to separate team code from third-party/vendored code.

### Skyline.sln Projects

| Category | Files | Code | Comments | Blank |
|----------|------:|-----:|---------:|------:|
| Skyline Core (main app) | $(Format-Number $categories['Core'].Files) | $(Format-Number $categories['Core'].Code) | $(Format-Number $categories['Core'].Comment) | $(Format-Number $categories['Core'].Blank) |
| Shared Libraries | $(Format-Number $categories['Shared'].Files) | $(Format-Number $categories['Shared'].Code) | $(Format-Number $categories['Shared'].Comment) | $(Format-Number $categories['Shared'].Blank) |
| Skyline Tests | $(Format-Number $categories['Test-Skyline'].Files) | $(Format-Number $categories['Test-Skyline'].Code) | $(Format-Number $categories['Test-Skyline'].Comment) | $(Format-Number $categories['Test-Skyline'].Blank) |
| Executables Tests | $(Format-Number $categories['Test-Executables'].Files) | $(Format-Number $categories['Test-Executables'].Code) | $(Format-Number $categories['Test-Executables'].Comment) | $(Format-Number $categories['Test-Executables'].Blank) |
| Executables (team tools) | $(Format-Number $categories['Executables'].Files) | $(Format-Number $categories['Executables'].Code) | $(Format-Number $categories['Executables'].Comment) | $(Format-Number $categories['Executables'].Blank) |

**Skyline.sln Core (non-test):** $(Format-Number $slnCoreTotal) lines
**Skyline.sln Tests:** $(Format-Number $skylineTests) lines
**Skyline.sln Total:** $(Format-Number $slnTotal) lines
**Executables (team code):** $(Format-Number $executables) lines

### ProteoWizard (in Pwiz installer)

| Category | Files | Code | Comments | Blank |
|----------|------:|-----:|---------:|------:|
| pwiz/ (C++ core) | $(Format-Number $categories['PwizCpp'].Files) | $(Format-Number $categories['PwizCpp'].Code) | $(Format-Number $categories['PwizCpp'].Comment) | $(Format-Number $categories['PwizCpp'].Blank) |
| Pwiz Tools (MSConvertGUI, SeeMS, etc.) | $(Format-Number $categories['PwizTools'].Files) | $(Format-Number $categories['PwizTools'].Code) | $(Format-Number $categories['PwizTools'].Comment) | $(Format-Number $categories['PwizTools'].Blank) |

**ProteoWizard Total:** $(Format-Number $pwizTotal) lines

### Bumbershoot (not team-maintained)

| Category | Files | Code | Comments | Blank |
|----------|------:|-----:|---------:|------:|
| Bumbershoot | $(Format-Number $categories['Bumbershoot'].Files) | $(Format-Number $categories['Bumbershoot'].Code) | $(Format-Number $categories['Bumbershoot'].Comment) | $(Format-Number $categories['Bumbershoot'].Blank) |

### Third-Party / Vendored Code (excluded from team totals)

| Library | Files | Code | Comments | Blank | Notes |
|---------|------:|-----:|---------:|------:|-------|
| ZedGraph | $(Format-Number $categories['ThirdParty-ZedGraph'].Files) | $(Format-Number $categories['ThirdParty-ZedGraph'].Code) | $(Format-Number $categories['ThirdParty-ZedGraph'].Comment) | $(Format-Number $categories['ThirdParty-ZedGraph'].Blank) | Charting library |
| alglib | $(Format-Number $categories['ThirdParty-alglib'].Files) | $(Format-Number $categories['ThirdParty-alglib'].Code) | $(Format-Number $categories['ThirdParty-alglib'].Comment) | $(Format-Number $categories['ThirdParty-alglib'].Blank) | Math library |
| Hardklor | $(Format-Number $categories['ThirdParty-Hardklor'].Files) | $(Format-Number $categories['ThirdParty-Hardklor'].Code) | $(Format-Number $categories['ThirdParty-Hardklor'].Comment) | $(Format-Number $categories['ThirdParty-Hardklor'].Blank) | Git submodule (includes sqlite) |
| BullseyeSharp | $(Format-Number $categories['ThirdParty-BullseyeSharp'].Files) | $(Format-Number $categories['ThirdParty-BullseyeSharp'].Code) | $(Format-Number $categories['ThirdParty-BullseyeSharp'].Comment) | $(Format-Number $categories['ThirdParty-BullseyeSharp'].Blank) | Git submodule |

### Team Totals

| Metric | Lines |
|--------|------:|
| **Skyline Total** | **$(Format-Number $skylineTotal)** |
| - Core (non-test) | $(Format-Number $slnCoreTotal) |
| - Tests | $(Format-Number $skylineTests) |
| - Executables | $(Format-Number $executables) |
| **ProteoWizard Total** | **$(Format-Number $pwizTotal)** |
| - C++ Core | $(Format-Number $pwizCpp) |
| - Pwiz Tools | $(Format-Number $pwizTools) |
| **Team Total** | **$(Format-Number $teamTotal)** |
| Bumbershoot (not maintained) | $(Format-Number $bumbershoot) |
| Third-party (excluded) | $(Format-Number $thirdPartyTotal) |

---

## Language Breakdown (Team Code Only)

"@

# Aggregate languages across team categories
$teamLangs = @{}
$teamCats = @("Core", "Shared", "Test-Skyline", "Test-Executables", "Executables", "PwizCpp", "PwizTools")
foreach ($cat in $teamCats) {
    foreach ($lang in $categories[$cat].Languages.Keys) {
        if (-not $teamLangs.ContainsKey($lang)) {
            $teamLangs[$lang] = @{ Files = 0; Blank = 0; Comment = 0; Code = 0 }
        }
        $teamLangs[$lang].Files += $categories[$cat].Languages[$lang].Files
        $teamLangs[$lang].Blank += $categories[$cat].Languages[$lang].Blank
        $teamLangs[$lang].Comment += $categories[$cat].Languages[$lang].Comment
        $teamLangs[$lang].Code += $categories[$cat].Languages[$lang].Code
    }
}

$report += @"
| Language | Files | Code | Comments | Blank |
|----------|------:|-----:|---------:|------:|

"@

foreach ($lang in $teamLangs.Keys | Sort-Object { $teamLangs[$_].Code } -Descending) {
    $l = $teamLangs[$lang]
    $report += "| $lang | $(Format-Number $l.Files) | $(Format-Number $l.Code) | $(Format-Number $l.Comment) | $(Format-Number $l.Blank) |`n"
}

$report += @"

---

## Notes

### Skyline (team-maintained, in Skyline installer)
- **Skyline Core**: Main Skyline application (Model, Controls, EditUI, etc.)
- **Shared Libraries**: Common, ProteomeDb, BiblioSpec, MSGraph, PanoramaClient, etc.
- **Skyline Tests**: Test, TestFunctional, TestTutorial, TestPerf, SkylineTester, SkylineNightly, etc.
- **Executables**: Satellite tools (AutoQC, SkylineBatch, DevTools, etc.)

### ProteoWizard (team-maintained, in Pwiz installer)
- **Pwiz C++ Core**: Native data reader library (pwiz/)
- **Pwiz Tools**: MSConvertGUI, SeeMS, etc.

### Not team-maintained
- **Bumbershoot**: Legacy proteomics tools from Vanderbilt (myrimatch, idpicker, etc.)

### Third-party/vendored code (excluded from totals)
- **ZedGraph**: Charting library (C#)
- **alglib**: Math/statistics library (C#)
- **Hardklor**: Git submodule including vendored sqlite (~143K lines)
- **BullseyeSharp**: Git submodule for code coverage
"@

# Add drilldown section to report if requested
if ($Drilldown) {
    $report += @"

---

## Drilldown by Subdirectory

"@

    $drilldownCats = @("Core", "Shared", "Test-Skyline", "Executables", "PwizCpp", "PwizTools", "Bumbershoot")
    foreach ($cat in $drilldownCats) {
        $catData = $categories[$cat]
        if ($catData.Code -eq 0) { continue }

        $suffix = if ($cat -eq "Bumbershoot") { " (not team-maintained)" } else { "" }
        $report += @"

### $cat$suffix ($(Format-Number $catData.Code) lines)

| Subdirectory | Files | Code | % |
|--------------|------:|-----:|--:|

"@

        $sortedSubdirs = $catData.Subdirs.GetEnumerator() | Sort-Object { $_.Value.Code } -Descending
        foreach ($entry in $sortedSubdirs) {
            $subdir = $entry.Key
            $data = $entry.Value
            $pct = [math]::Round(($data.Code / $catData.Code) * 100, 1)
            $report += "| $subdir | $(Format-Number $data.Files) | $(Format-Number $data.Code) | $pct% |`n"
        }
    }
}

# Console output
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host "LINES OF CODE SUMMARY (actual code only)" -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host ""
Write-Host "Skyline (team-maintained):" -ForegroundColor Green
Write-Host ("  {0,-35} {1,12} lines" -f "Core (main app)", (Format-Number $skylineCore))
Write-Host ("  {0,-35} {1,12} lines" -f "Shared Libraries", (Format-Number $shared))
Write-Host ("  {0,-35} {1,12} lines" -f "Tests", (Format-Number $skylineTests))
Write-Host ("  {0,-35} {1,12} lines" -f "Executables", (Format-Number $executables))
Write-Host ("  {0,-35} {1,12} lines" -f "SKYLINE TOTAL", (Format-Number $skylineTotal)) -ForegroundColor Green
Write-Host ""
Write-Host "ProteoWizard (team-maintained):" -ForegroundColor Yellow
Write-Host ("  {0,-35} {1,12} lines" -f "C++ Core (pwiz/)", (Format-Number $pwizCpp))
Write-Host ("  {0,-35} {1,12} lines" -f "Pwiz Tools", (Format-Number $pwizTools))
Write-Host ("  {0,-35} {1,12} lines" -f "PROTEOWIZARD TOTAL", (Format-Number $pwizTotal)) -ForegroundColor Yellow
Write-Host ""
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host ("{0,-37} {1,12} lines" -f "TEAM TOTAL", (Format-Number $teamTotal)) -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host ""
Write-Host "Not team-maintained:" -ForegroundColor Gray
Write-Host ("  {0,-35} {1,12} lines" -f "Bumbershoot", (Format-Number $bumbershoot))
Write-Host ""
Write-Host "Third-party/Vendored (excluded):" -ForegroundColor Gray
Write-Host ("  {0,-35} {1,12} lines" -f "ZedGraph", (Format-Number $categories['ThirdParty-ZedGraph'].Code))
Write-Host ("  {0,-35} {1,12} lines" -f "alglib", (Format-Number $categories['ThirdParty-alglib'].Code))
Write-Host ("  {0,-35} {1,12} lines" -f "Hardklor (incl. sqlite)", (Format-Number $categories['ThirdParty-Hardklor'].Code))
Write-Host ("  {0,-35} {1,12} lines" -f "BullseyeSharp", (Format-Number $categories['ThirdParty-BullseyeSharp'].Code))
Write-Host ("  {0,-35} {1,12} lines" -f "Third-party Total", (Format-Number $thirdPartyTotal)) -ForegroundColor Gray
Write-Host ""

# Drilldown console output
if ($Drilldown) {
    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Magenta
    Write-Host "DRILLDOWN BY SUBDIRECTORY" -ForegroundColor Magenta
    Write-Host ("=" * 70) -ForegroundColor Magenta

    $drilldownCats = @("Core", "Shared", "Test-Skyline", "Executables", "PwizCpp", "PwizTools", "Bumbershoot")
    foreach ($cat in $drilldownCats) {
        $catData = $categories[$cat]
        if ($catData.Code -eq 0) { continue }

        Write-Host ""
        $color = if ($cat -eq "Bumbershoot") { "Gray" } else { "Yellow" }
        Write-Host "$cat ($(Format-Number $catData.Code) lines):" -ForegroundColor $color

        $sortedSubdirs = $catData.Subdirs.GetEnumerator() | Sort-Object { $_.Value.Code } -Descending
        foreach ($entry in $sortedSubdirs) {
            $subdir = $entry.Key
            $data = $entry.Value
            $pct = [math]::Round(($data.Code / $catData.Code) * 100, 1)
            Write-Host ("  {0,-35} {1,10} lines ({2,5}%)" -f $subdir, (Format-Number $data.Code), $pct)
        }
    }
    Write-Host ""
}

# Save report
$tmpDir = Join-Path $repoRoot "ai/.tmp"
if (-not (Test-Path $tmpDir)) {
    New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
}

$dateStamp = Get-Date -Format "yyyyMMdd-HHmm"
$reportPath = Join-Path $tmpDir "loc-audit-$dateStamp.md"
$report | Set-Content -Path $reportPath -Encoding UTF8

Write-Host "Report saved: $reportPath" -ForegroundColor Green
