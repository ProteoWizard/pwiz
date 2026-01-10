<#
.SYNOPSIS
    Audits test data file sizes in the Skyline project.

.DESCRIPTION
    Scans pwiz_tools\Skyline for test data files (.zip, .data directories, .json files)
    and reports their sizes in descending order. Helps identify large test data files
    that might need optimization.

.PARAMETER SkylineRoot
    Root directory of the Skyline project (default: pwiz_tools\Skyline relative to repo root)

.PARAMETER IncludeJson
    Include all .json files (default: only *WebData.json files)

.EXAMPLE
    .\audit-testdata.ps1
    Audit all test data files and show sizes in descending order

.EXAMPLE
    .\audit-testdata.ps1 -IncludeJson
    Include all .json files, not just *WebData.json files
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$SkylineRoot = $null,
    [Parameter(Mandatory=$false)]
    [switch]$IncludeJson = $false
)

# Determine Skyline root directory
if ([string]::IsNullOrEmpty($SkylineRoot))
{
    $scriptPath = $PSScriptRoot
    if ([string]::IsNullOrEmpty($scriptPath))
    {
        $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    # From ai\scripts, go up to repo root, then into pwiz_tools\Skyline
    $repoRoot = Split-Path -Parent (Split-Path -Parent $scriptPath)
    $SkylineRoot = Join-Path $repoRoot "pwiz_tools\Skyline"
}

if (-not (Test-Path $SkylineRoot))
{
    Write-Error "Skyline root directory not found: $SkylineRoot"
    exit 1
}

Write-Host "Scanning test data files in: $SkylineRoot" -ForegroundColor Cyan
Write-Host ""

# Function to format file size
function Format-FileSize {
    param([long]$Size)
    
    if ($Size -ge 1GB)
    {
        return "{0:N2} GB" -f ($Size / 1GB)
    }
    elseif ($Size -ge 1MB)
    {
        return "{0:N2} MB" -f ($Size / 1MB)
    }
    elseif ($Size -ge 1KB)
    {
        return "{0:N2} KB" -f ($Size / 1KB)
    }
    else
    {
        return "$Size bytes"
    }
}

# Function to get directory size
function Get-DirectorySize {
    param([string]$Path)
    
    $size = 0
    try
    {
        Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            $size += $_.Length
        }
    }
    catch
    {
        # Ignore errors (e.g., access denied)
    }
    return $size
}

# Function to check if a path should be excluded (build artifacts, test results)
function Should-ExcludePath {
    param([string]$Path)
    
    # Normalize path separators for comparison
    $normalizedPath = $Path.Replace('\', '/').ToLower()
    
    # Exclude build and test output directories
    $excludePatterns = @(
        '/bin/',
        '/obj/',
        '/testresults/',
        '/testoutput/',
        '^bin/',
        '^obj/',
        '^testresults/',
        '^testoutput/'
    )
    
    foreach ($pattern in $excludePatterns)
    {
        if ($normalizedPath -match $pattern)
        {
            return $true
        }
    }
    
    return $false
}

$results = @()

# Find .zip files
Write-Host "Scanning .zip files..." -ForegroundColor Gray
$zipFiles = Get-ChildItem -Path $SkylineRoot -Filter "*.zip" -Recurse -File -ErrorAction SilentlyContinue
foreach ($file in $zipFiles)
{
    $relativePath = $file.FullName.Substring($SkylineRoot.Length + 1)
    if (-not (Should-ExcludePath -Path $relativePath))
    {
        $results += [PSCustomObject]@{
            Type = "ZIP"
            Path = $relativePath
            Size = $file.Length
            SizeFormatted = Format-FileSize -Size $file.Length
        }
    }
}

# Find .data directories
Write-Host "Scanning .data directories..." -ForegroundColor Gray
$dataDirs = Get-ChildItem -Path $SkylineRoot -Directory -Recurse -ErrorAction SilentlyContinue | 
    Where-Object { $_.Name -like "*.data" }
foreach ($dir in $dataDirs)
{
    $relativePath = $dir.FullName.Substring($SkylineRoot.Length + 1)
    if (-not (Should-ExcludePath -Path $relativePath))
    {
        $size = Get-DirectorySize -Path $dir.FullName
        $results += [PSCustomObject]@{
            Type = "DATA"
            Path = $relativePath
            Size = $size
            SizeFormatted = Format-FileSize -Size $size
        }
    }
}

# Find .json files
Write-Host "Scanning .json files..." -ForegroundColor Gray
if ($IncludeJson)
{
    $jsonFiles = Get-ChildItem -Path $SkylineRoot -Filter "*.json" -Recurse -File -ErrorAction SilentlyContinue
}
else
{
    $jsonFiles = Get-ChildItem -Path $SkylineRoot -Filter "*WebData.json" -Recurse -File -ErrorAction SilentlyContinue
}
foreach ($file in $jsonFiles)
{
    $relativePath = $file.FullName.Substring($SkylineRoot.Length + 1)
    if (-not (Should-ExcludePath -Path $relativePath))
    {
        $results += [PSCustomObject]@{
            Type = "JSON"
            Path = $relativePath
            Size = $file.Length
            SizeFormatted = Format-FileSize -Size $file.Length
        }
    }
}

# Sort by size descending
$results = $results | Sort-Object -Property Size -Descending

# Display results
Write-Host ""
Write-Host "Test Data File Sizes (sorted by size, descending):" -ForegroundColor Cyan
Write-Host ("=" * 100)

$totalSize = 0
$countByType = @{
    ZIP = 0
    DATA = 0
    JSON = 0
}

foreach ($result in $results)
{
    $totalSize += $result.Size
    $countByType[$result.Type]++
    
    $color = switch ($result.Type)
    {
        "ZIP" { "White" }
        "DATA" { "Yellow" }
        "JSON" { "Green" }
        default { "White" }
    }
    
    Write-Host ("{0,-6} {1,12} {2}" -f $result.Type, $result.SizeFormatted, $result.Path) -ForegroundColor $color
}

Write-Host ("=" * 100)
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host ("  Total files: {0}" -f $results.Count)
Write-Host ("  ZIP files: {0}" -f $countByType.ZIP)
Write-Host ("  DATA directories: {0}" -f $countByType.DATA)
Write-Host ("  JSON files: {0}" -f $countByType.JSON)
Write-Host ("  Total size: {0}" -f (Format-FileSize -Size $totalSize))
Write-Host ""

# Show size thresholds
$thresholds = @(
    @{ Size = 50MB; Label = ">= 50 MB" }
    @{ Size = 10MB; Label = ">= 10 MB" }
    @{ Size = 5MB; Label = ">= 5 MB" }
    @{ Size = 2MB; Label = ">= 2 MB" }
    @{ Size = 1MB; Label = ">= 1 MB" }
)

Write-Host "Files by size threshold:" -ForegroundColor Cyan
foreach ($threshold in $thresholds)
{
    $count = ($results | Where-Object { $_.Size -ge $threshold.Size }).Count
    if ($count -gt 0)
    {
        Write-Host ("  {0}: {1} files" -f $threshold.Label, $count) -ForegroundColor Yellow
    }
}

