<#
.SYNOPSIS
    Analyzes an HTTP recording JSON file to identify the largest requests.

.DESCRIPTION
    Parses an HttpInteraction JSON file and reports which requests contribute
    the most to the file size, helping identify opportunities for optimization.

.PARAMETER JsonPath
    Path to the JSON file to analyze

.PARAMETER TopN
    Number of top requests to show (default: 20)

.EXAMPLE
    .\analyze-http-json.ps1 -JsonPath "pwiz_tools\Skyline\TestConnected\PanoramaClientDownloadTestWebData.json"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$JsonPath,
    [Parameter(Mandatory=$false)]
    [int]$TopN = 20
)

if (-not (Test-Path $JsonPath))
{
    Write-Error "JSON file not found: $JsonPath"
    exit 1
}

Write-Host "Analyzing HTTP recording: $JsonPath" -ForegroundColor Cyan
Write-Host ""

# Load JSON
$jsonContent = Get-Content $JsonPath -Raw | ConvertFrom-Json
$interactions = $jsonContent.HttpInteractions

Write-Host "Total interactions: $($interactions.Count)" -ForegroundColor Gray
Write-Host ""

# Function to calculate response size
function Get-ResponseSize {
    param($interaction)
    
    $size = 0
    
    if ($interaction.ResponseBodyIsBase64)
    {
        # Base64 encoded binary content
        if ($interaction.ResponseBodyLines)
        {
            # Join base64 lines and decode to get actual size
            $base64String = $interaction.ResponseBodyLines -join ""
            # Base64 encoding increases size by ~33%, so decode to get original size
            try
            {
                $bytes = [Convert]::FromBase64String($base64String)
                $size = $bytes.Length
            }
            catch
            {
                # If decoding fails, estimate from base64 string length
                $size = [int](($base64String.Length * 3) / 4)
            }
        }
        elseif ($interaction.ResponseBody)
        {
            try
            {
                $bytes = [Convert]::FromBase64String($interaction.ResponseBody)
                $size = $bytes.Length
            }
            catch
            {
                $size = [int](($interaction.ResponseBody.Length * 3) / 4)
            }
        }
    }
    else
    {
        # Text content
        if ($interaction.ResponseBodyLines)
        {
            # Join lines (add newlines between, but JSON already has them)
            # For size calculation, we need the actual UTF-8 bytes
            $text = $interaction.ResponseBodyLines -join "`n"
            $size = [System.Text.Encoding]::UTF8.GetByteCount($text)
        }
        elseif ($interaction.ResponseBody)
        {
            $size = [System.Text.Encoding]::UTF8.GetByteCount($interaction.ResponseBody)
        }
    }
    
    # Also account for JSON overhead (property names, formatting, etc.)
    # Estimate ~200 bytes overhead per interaction
    $size += 200
    
    return $size
}

# Function to format file size
function Format-Size {
    param([long]$Size)
    
    if ($Size -ge 1MB)
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

# Analyze each interaction
$sizes = @()
$totalSize = 0

Write-Host "Calculating sizes..." -ForegroundColor Gray
foreach ($interaction in $interactions)
{
    $size = Get-ResponseSize -interaction $interaction
    $totalSize += $size
    
    # Extract URL path for readability (remove query string if too long)
    $url = $interaction.Url
    $uri = [System.Uri]$url
    $displayUrl = $uri.PathAndQuery
    if ($displayUrl.Length -gt 100)
    {
        $displayUrl = $displayUrl.Substring(0, 97) + "..."
    }
    
    $sizes += [PSCustomObject]@{
        Index = $sizes.Count
        Url = $url
        DisplayUrl = $displayUrl
        Method = $interaction.Method
        StatusCode = $interaction.StatusCode
        ContentType = $interaction.ContentType
        IsBase64 = $interaction.ResponseBodyIsBase64
        Size = $size
        SizeFormatted = Format-Size -Size $size
    }
}

# Sort by size descending
$sortedSizes = $sizes | Sort-Object -Property Size -Descending

# Get file size for comparison
$fileInfo = Get-Item $JsonPath
$fileSize = $fileInfo.Length

Write-Host ""
Write-Host "File Statistics:" -ForegroundColor Cyan
Write-Host ("  File size: {0}" -f (Format-Size -Size $fileSize))
Write-Host ("  Total interactions: {0}" -f $interactions.Count)
Write-Host ("  Estimated response data: {0}" -f (Format-Size -Size $totalSize))
Write-Host ("  JSON overhead: {0}" -f (Format-Size -Size ($fileSize - $totalSize)))
Write-Host ""

Write-Host "Top $TopN Largest Requests:" -ForegroundColor Cyan
Write-Host ("=" * 120)
Write-Host ("{0,-6} {1,-12} {2,-8} {3,-30} {4}" -f "Rank", "Size", "Method", "Status", "URL")
Write-Host ("-" * 120)

for ($i = 0; $i -lt [Math]::Min($TopN, $sortedSizes.Count); $i++)
{
    $item = $sortedSizes[$i]
    $percent = ($item.Size / $fileSize) * 100
    Write-Host ("{0,-6} {1,-12} {2,-8} {3,-30} {4}" -f `
        ($i + 1), `
        $item.SizeFormatted, `
        $item.Method, `
        "$($item.StatusCode) ($($item.ContentType))", `
        $item.DisplayUrl)
}

Write-Host ("-" * 120)
Write-Host ""

# Summary statistics
$largeRequests = $sortedSizes | Where-Object { $_.Size -ge 1MB }
$mediumRequests = $sortedSizes | Where-Object { $_.Size -ge 100KB -and $_.Size -lt 1MB }
$smallRequests = $sortedSizes | Where-Object { $_.Size -lt 100KB }

Write-Host "Size Distribution:" -ForegroundColor Cyan
Write-Host ("  >= 1 MB: {0} requests ({1})" -f `
    $largeRequests.Count, `
    (Format-Size -Size ($largeRequests | Measure-Object -Property Size -Sum).Sum))
Write-Host ("  >= 100 KB: {0} requests ({1})" -f `
    $mediumRequests.Count, `
    (Format-Size -Size ($mediumRequests | Measure-Object -Property Size -Sum).Sum))
Write-Host ("  < 100 KB: {0} requests ({1})" -f `
    $smallRequests.Count, `
    (Format-Size -Size ($smallRequests | Measure-Object -Property Size -Sum).Sum))
Write-Host ""

