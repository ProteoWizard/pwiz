# Remove UTF-8 BOM from source files
# Preserves file timestamps and validates conversion success
#
# Usage:
#   .\remove-bom.ps1                    # Dry-run mode (shows what would be changed)
#   .\remove-bom.ps1 -Execute           # Actually remove BOMs
#   .\remove-bom.ps1 -FileList files.txt -Execute  # Process specific files
#
# This script is part of the UTF-8 BOM standardization effort.
# See todos/active/TODO-20251019_utf8_no_bom.md for context.

param(
    [switch]$Execute = $false,
    [string]$FileList = "",
    [string[]]$ExcludePatterns = @("*.tli", "*.tlh")
)

# Default to files-with-bom.txt in script directory if not specified
if ([string]::IsNullOrEmpty($FileList)) {
    $FileList = Join-Path $PSScriptRoot "files-with-bom.txt"
}

$utf8Bom = @(0xEF, 0xBB, 0xBF)
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

# Color output
function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }
function Write-Success($msg) { Write-Host $msg -ForegroundColor Green }
function Write-Warning($msg) { Write-Host $msg -ForegroundColor Yellow }
function Write-Error($msg) { Write-Host $msg -ForegroundColor Red }

Write-Info "UTF-8 BOM Removal Tool"
Write-Info "======================"
Write-Host ""

if (-not $Execute) {
    Write-Warning "DRY-RUN MODE - No files will be modified"
    Write-Warning "Use -Execute to actually remove BOMs"
    Write-Host ""
}

# Check if file list exists
if (-not (Test-Path $FileList)) {
    Write-Error "File list not found: $FileList"
    Write-Host "Run ai/scripts/validate-bom-compliance.ps1 first to generate the file list"
    exit 1
}

# Read file list
$files = Get-Content $FileList | Where-Object { $_.Trim() -ne "" }
Write-Info "Found $($files.Count) files in $FileList"
Write-Host ""

# Filter out excluded patterns
$filteredFiles = @()
$excludedFiles = @()

foreach ($file in $files) {
    $excluded = $false
    foreach ($pattern in $ExcludePatterns) {
        if ($file -like $pattern) {
            $excluded = $true
            $excludedFiles += $file
            break
        }
    }
    if (-not $excluded) {
        $filteredFiles += $file
    }
}

if ($excludedFiles.Count -gt 0) {
    Write-Warning "Excluding $($excludedFiles.Count) files based on patterns:"
    foreach ($pattern in $ExcludePatterns) {
        $count = ($excludedFiles | Where-Object { $_ -like $pattern }).Count
        if ($count -gt 0) {
            Write-Warning "  $pattern`: $count files"
        }
    }
    Write-Host ""
}

Write-Info "Processing $($filteredFiles.Count) files..."
Write-Host ""

$processed = 0
$skipped = 0
$errors = 0
$removed = 0

foreach ($file in $filteredFiles) {
    $processed++

    # Convert forward slashes to backslashes for Windows
    $filePath = $file.Replace('/', '\')
    $fullPath = Join-Path (Get-Location) $filePath

    # Skip files that don't exist
    if (-not [System.IO.File]::Exists($fullPath)) {
        Write-Warning "[$processed/$($filteredFiles.Count)] SKIP: File not found - $file"
        $skipped++
        continue
    }

    try {
        # Read all bytes
        $bytes = [System.IO.File]::ReadAllBytes($fullPath)

        # Check if file has BOM
        if ($bytes.Length -ge 3 -and
            $bytes[0] -eq $utf8Bom[0] -and
            $bytes[1] -eq $utf8Bom[1] -and
            $bytes[2] -eq $utf8Bom[2]) {

            if ($Execute) {
                # Preserve original timestamps
                $creationTime = [System.IO.File]::GetCreationTime($fullPath)
                $lastWriteTime = [System.IO.File]::GetLastWriteTime($fullPath)
                $lastAccessTime = [System.IO.File]::GetLastAccessTime($fullPath)

                # Remove BOM (skip first 3 bytes)
                $newBytes = $bytes[3..($bytes.Length - 1)]

                # Write file without BOM
                [System.IO.File]::WriteAllBytes($fullPath, $newBytes)

                # Restore timestamps
                [System.IO.File]::SetCreationTime($fullPath, $creationTime)
                [System.IO.File]::SetLastWriteTime($fullPath, $lastWriteTime)
                [System.IO.File]::SetLastAccessTime($fullPath, $lastAccessTime)

                # Verify BOM was removed
                $verifyBytes = [System.IO.File]::ReadAllBytes($fullPath)
                if ($verifyBytes.Length -ge 3 -and
                    $verifyBytes[0] -eq $utf8Bom[0] -and
                    $verifyBytes[1] -eq $utf8Bom[1] -and
                    $verifyBytes[2] -eq $utf8Bom[2]) {
                    Write-Error "[$processed/$($filteredFiles.Count)] FAILED: BOM still present - $file"
                    $errors++
                } else {
                    Write-Success "[$processed/$($filteredFiles.Count)] REMOVED: $file"
                    $removed++
                }
            } else {
                Write-Warning "[$processed/$($filteredFiles.Count)] WOULD REMOVE: $file"
                $removed++
            }
        } else {
            Write-Host "[$processed/$($filteredFiles.Count)] NO BOM: $file" -ForegroundColor Gray
            $skipped++
        }
    } catch {
        Write-Error "[$processed/$($filteredFiles.Count)] ERROR: $file - $($_.Exception.Message)"
        $errors++
    }
}

Write-Host ""
Write-Info "=== Summary ==="
Write-Host "Total files processed: $processed" -ForegroundColor White
Write-Success "BOMs removed:          $removed"
Write-Host "Skipped (no BOM):      $skipped" -ForegroundColor Gray
Write-Host "Excluded (patterns):   $($excludedFiles.Count)" -ForegroundColor Gray

if ($errors -gt 0) {
    Write-Error "Errors encountered:    $errors"
}

Write-Host ""

if (-not $Execute) {
    Write-Warning "This was a DRY-RUN. No files were modified."
    Write-Warning "Use -Execute to actually remove BOMs."
} else {
    Write-Success "BOM removal complete!"
    Write-Info "Run 'git diff' to verify changes."
}

Write-Host ""
