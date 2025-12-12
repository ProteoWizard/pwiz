# Analyze dotCover JSON results for specified code patterns
#
# Usage:
#   .\Analyze-Coverage.ps1 -CoverageJsonPath coverage.json -PatternsFile patterns.txt
#   .\Analyze-Coverage.ps1 -CoverageJsonPath coverage.json -Patterns "FilesTree","FileModel"
#
# The patterns file should contain one type name pattern per line (wildcards supported).
# Lines starting with # are treated as comments.
#
# Example patterns file:
#   # Files view components
#   FilesTree
#   FileModel
#   FileSystemService

param(
    [Parameter(Mandatory=$true, HelpMessage="Path to dotCover JSON coverage file")]
    [string]$CoverageJsonPath,

    [Parameter(ParameterSetName="File", HelpMessage="Path to file containing type name patterns (one per line)")]
    [string]$PatternsFile,

    [Parameter(ParameterSetName="Array", HelpMessage="Array of type name patterns")]
    [string[]]$Patterns,

    [Parameter(HelpMessage="Title for the coverage report")]
    [string]$ReportTitle = "CODE COVERAGE SUMMARY"
)

# Validate inputs
if (-not (Test-Path $CoverageJsonPath)) {
    Write-Host "Error: Coverage JSON file not found: $CoverageJsonPath" -ForegroundColor Red
    exit 1
}

# Load patterns from file or use provided array
$typePatterns = @()
if ($PatternsFile) {
    if (-not (Test-Path $PatternsFile)) {
        Write-Host "Error: Patterns file not found: $PatternsFile" -ForegroundColor Red
        exit 1
    }

    # Check if the patterns file contains .cs file paths (coverage file) or type name patterns
    $firstNonCommentLine = Get-Content $PatternsFile -Encoding UTF8 |
        Where-Object { $_ -and $_ -notmatch '^\s*#' -and $_.Trim() } |
        Select-Object -First 1

    if ($firstNonCommentLine -and $firstNonCommentLine -match '\.cs$') {
        # Patterns file contains .cs file paths - use Extract-TypeNames.ps1 to get fully-qualified type names
        $extractScript = Join-Path (Split-Path $PSCommandPath -Parent) "Extract-TypeNames.ps1"
        if (-not (Test-Path $extractScript)) {
            Write-Host "Error: Extract-TypeNames.ps1 not found at: $extractScript" -ForegroundColor Red
            exit 1
        }

        $extractedTypes = & $extractScript -CoverageFile $PatternsFile -Format FullyQualified
        if ($extractedTypes) {
            $typePatterns = $extractedTypes | Select-Object -ExpandProperty Output | Sort-Object -Unique
        }

        if ($typePatterns.Count -eq 0) {
            Write-Host "Error: No types extracted from .cs files in: $PatternsFile" -ForegroundColor Red
            exit 1
        }
    } else {
        # Patterns file contains type name patterns directly
        $typePatterns = Get-Content $PatternsFile -Encoding UTF8 |
            Where-Object { $_ -and $_ -notmatch '^\s*#' -and $_.Trim() } |
            ForEach-Object { $_.Trim() }

        if ($typePatterns.Count -eq 0) {
            Write-Host "Error: No patterns found in file: $PatternsFile" -ForegroundColor Red
            exit 1
        }
    }
}
elseif ($Patterns) {
    $typePatterns = $Patterns
}
else {
    Write-Host "Error: Must specify either -PatternsFile or -Patterns" -ForegroundColor Red
    Write-Host "Usage: .\Analyze-Coverage.ps1 -CoverageJsonPath <path> -PatternsFile <path>"
    Write-Host "   or: .\Analyze-Coverage.ps1 -CoverageJsonPath <path> -Patterns <pattern1>,<pattern2>,..."
    exit 1
}

# Read and parse JSON (handle BOM)
Write-Host "Reading coverage data from: $CoverageJsonPath" -ForegroundColor Gray
$jsonContent = Get-Content $CoverageJsonPath -Raw -Encoding UTF8
$coverage = $jsonContent | ConvertFrom-Json

function FindCoverageForType {
    param(
        [object]$node,
        [string[]]$patterns,
        [string]$path = ""
    )

    $results = @()

    if ($node.Kind -eq "Type") {
        $fullPath = if ($path) { "$path.$($node.Name)" } else { $node.Name }

        foreach ($pattern in $patterns) {
            $matched = $false

            # Check for exact match (fully-qualified type name) or wildcard match
            if ($pattern.Contains('.')) {
                # Pattern looks like a fully-qualified name - use exact match
                $matched = ($fullPath -eq $pattern)
            } else {
                # Pattern is a simple name - use wildcard match
                $matched = ($node.Name -like "*$pattern*")
            }

            if ($matched) {
                $coveragePercent = 0
                if ($node.TotalStatements -gt 0) {
                    $coveragePercent = [math]::Round(($node.CoveredStatements / $node.TotalStatements) * 100, 1)
                }
                $results += [PSCustomObject]@{
                    Path = $fullPath
                    Type = $node.Name
                    CoveredStatements = $node.CoveredStatements
                    TotalStatements = $node.TotalStatements
                    CoveragePercent = $coveragePercent
                }
                break  # Only match once per type
            }
        }
    }

    if ($node.Children) {
        $newPath = if ($node.Kind -eq "Namespace" -or $node.Kind -eq "Type") {
            if ($path) { "$path.$($node.Name)" } else { $node.Name }
        } else {
            $path
        }

        foreach ($child in $node.Children) {
            $results += FindCoverageForType -node $child -patterns $patterns -path $newPath
        }
    }

    return $results
}

# Find Skyline project/assembly (structure differs between dotCover versions)
# Newer versions use "Project", older versions use "Assembly"
$skylineProject = $coverage.Children | Where-Object {
    ($_.Kind -eq "Project" -or $_.Kind -eq "Assembly") -and $_.Name -like "*Skyline*"
} | Select-Object -First 1

if (-not $skylineProject) {
    Write-Host "Skyline project/assembly not found in coverage data" -ForegroundColor Red
    Write-Host "Available items:" -ForegroundColor Yellow
    $coverage.Children | Where-Object { $_.Kind -eq "Project" -or $_.Kind -eq "Assembly" } | ForEach-Object {
        Write-Host "  - $($_.Name) ($($_.Kind))" -ForegroundColor Gray
    }
    exit 1
}

Write-Host "Analyzing coverage for patterns: $($typePatterns -join ', ')" -ForegroundColor Cyan
Write-Host "Project: $($skylineProject.Name)" -ForegroundColor Gray
Write-Host ""

# Find all matching types
$matchedCoverage = FindCoverageForType -node $skylineProject -patterns $typePatterns

if ($matchedCoverage.Count -eq 0) {
    Write-Host "No types found matching patterns" -ForegroundColor Yellow
    Write-Host "Searched for: $($typePatterns -join ', ')"
    exit 0
}

# Group by type and calculate totals
$summary = $matchedCoverage | Group-Object Type | ForEach-Object {
    $type = $_.Name
    $items = $_.Group
    $totalCovered = ($items | Measure-Object -Property CoveredStatements -Sum).Sum
    $totalStatements = ($items | Measure-Object -Property TotalStatements -Sum).Sum
    $uncovered = $totalStatements - $totalCovered
    $coveragePercent = 0
    if ($totalStatements -gt 0) {
        $coveragePercent = [math]::Round(($totalCovered / $totalStatements) * 100, 1)
    }

    [PSCustomObject]@{
        Type = $type
        CoveredStatements = $totalCovered
        TotalStatements = $totalStatements
        UncoveredStatements = $uncovered
        CoveragePercent = $coveragePercent
        Methods = $items.Count
    }
} | Sort-Object UncoveredStatements -Descending

$separator = "=" * 80
Write-Host $separator -ForegroundColor Cyan
Write-Host $ReportTitle -ForegroundColor Cyan
Write-Host $separator -ForegroundColor Cyan
Write-Host ""

$overallCovered = ($summary | Measure-Object -Property CoveredStatements -Sum).Sum
$overallTotal = ($summary | Measure-Object -Property TotalStatements -Sum).Sum
$overallUncovered = $overallTotal - $overallCovered
$overallPercent = 0
if ($overallTotal -gt 0) {
    $overallPercent = [math]::Round(($overallCovered / $overallTotal) * 100, 1)
}

Write-Host "Overall Coverage: $overallPercent% ($overallCovered / $overallTotal statements, $overallUncovered uncovered)" -ForegroundColor $(if ($overallPercent -ge 80) { "Green" } elseif ($overallPercent -ge 50) { "Yellow" } else { "Red" })
Write-Host ""

# Show priority section - types with most uncovered statements
$priorityTypes = $summary | Where-Object { $_.UncoveredStatements -ge 20 }
if ($priorityTypes.Count -gt 0) {
    $priorityUncovered = ($priorityTypes | Measure-Object -Property UncoveredStatements -Sum).Sum
    $priorityPercent = if ($overallUncovered -gt 0) { [math]::Round(($priorityUncovered / $overallUncovered) * 100, 0) } else { 0 }
    Write-Host "Priority (20+ uncovered statements, accounting for $priorityPercent% of uncovered):" -ForegroundColor Yellow
    foreach ($item in $priorityTypes) {
        Write-Host "  $($item.Type): $($item.UncoveredStatements) uncovered ($($item.CoveragePercent)% of $($item.TotalStatements))" -ForegroundColor Yellow
    }
    Write-Host ""
}

Write-Host "Coverage by Type (sorted by uncovered count):" -ForegroundColor Cyan
Write-Host ""

foreach ($item in $summary) {
    $color = if ($item.CoveragePercent -ge 80) { "Green" }
             elseif ($item.CoveragePercent -ge 50) { "Yellow" }
             else { "Red" }

    Write-Host "  $($item.Type): $($item.CoveragePercent)% ($($item.UncoveredStatements) uncovered / $($item.TotalStatements) total)" -ForegroundColor $color
}

# Show types with no coverage (0%)
$uncovered = $summary | Where-Object { $_.CoveragePercent -eq 0 -and $_.TotalStatements -gt 0 }
if ($uncovered.Count -gt 0) {
    Write-Host ""
    Write-Host "Types with no coverage:" -ForegroundColor Red
    foreach ($item in $uncovered) {
        Write-Host "  - $($item.Type): $($item.TotalStatements) statements" -ForegroundColor Red
    }
}

Write-Host $separator -ForegroundColor Cyan
