# Validate UTF-8 BOM compliance against approved list
#
# Usage: .\validate-bom-compliance.ps1
# Returns: Exit code 0 if compliant, 1 if unexpected BOMs found
#
# This script is intended to be used in CI/commit hooks to prevent
# unwanted BOM introduction.

$ErrorActionPreference = "Stop"
$utf8Bom = @(0xEF, 0xBB, 0xBF)

# Approved files that are allowed to have BOM with explanations
$approvedBomFiles = @{
    # Visual Studio auto-generated COM type library files
    "pwiz_aux/msrc/utility/vendor_api/thermo/xrawfile2.tli" = "Visual Studio generated COM type library"
    "pwiz_tools/Skyline/Executables/BuildMethod/BuildLTQMethod/ltmethod.tlh" = "Visual Studio generated COM type library"
    "pwiz_tools/Skyline/Executables/BuildMethod/BuildLTQMethod/ltmethod.tli" = "Visual Studio generated COM type library"
    "pwiz_tools/Skyline/Executables/BuildMethod/BuildTSQEZMethod/tsqezmethod.tlh" = "Visual Studio generated COM type library"
    "pwiz_tools/Skyline/Executables/BuildMethod/BuildTSQEZMethod/tsqezmethod.tli" = "Visual Studio generated COM type library"

    # Agilent vendor data format test files (represent real instrument output)
    "pwiz/data/vendor_readers/Agilent/Reader_Agilent_Test.data/TOFsulfasMS4GHzDualMode+DADSpectra+UVSignal272-NoProfile.d/AcqData/DefaultMassCal.xml" = "Agilent vendor data format"
    "pwiz/data/vendor_readers/Agilent/Reader_Agilent_Test.data/TOFsulfasMS4GHzDualMode+DADSpectra+UVSignal272-NoProfile.d/AcqData/Devices.xml" = "Agilent vendor data format"
    "pwiz/data/vendor_readers/Agilent/Reader_Agilent_Test.data/TOFsulfasMS4GHzDualMode+DADSpectra+UVSignal272-NoProfile.d/AcqData/MSTS.xml" = "Agilent vendor data format"
    "pwiz/data/vendor_readers/Agilent/Reader_Agilent_Test.data/TOFsulfasMS4GHzDualMode+DADSpectra+UVSignal272-NoProfile.d/AcqData/acqmethod.xml" = "Agilent vendor data format"
    "pwiz_tools/Bumbershoot/bumberdash/Tests/Data/AgilentTest.d/AcqData/Devices.xml" = "Agilent vendor data format"
    "pwiz_tools/Bumbershoot/bumberdash/Tests/Data/AgilentTest.d/AcqData/acqmethod.xml" = "Agilent vendor data format"
}

Write-Host "Validating UTF-8 BOM compliance..." -ForegroundColor Cyan
Write-Host ""

# Get all Git-tracked files
$gitFiles = @(git ls-files)
$filesWithBom = @()

foreach ($file in $gitFiles) {
    # Convert forward slashes to backslashes for Windows
    $filePath = $file.Replace('/', '\')
    $fullPath = Join-Path (Get-Location) $filePath

    if (-not [System.IO.File]::Exists($fullPath)) {
        continue
    }

    try {
        # Read first 3 bytes
        $stream = [System.IO.File]::OpenRead($fullPath)
        $bytes = New-Object byte[] 3
        $bytesRead = $stream.Read($bytes, 0, 3)
        $stream.Close()

        if ($bytesRead -ge 3 -and
            $bytes[0] -eq $utf8Bom[0] -and
            $bytes[1] -eq $utf8Bom[1] -and
            $bytes[2] -eq $utf8Bom[2]) {
            $filesWithBom += $file
        }
    } catch {
        # Skip files that can't be read
    }
}

Write-Host "Found $($filesWithBom.Count) files with UTF-8 BOM" -ForegroundColor $(if ($filesWithBom.Count -eq 0) { "Green" } else { "Yellow" })
Write-Host ""

# Check for unexpected BOMs
$unexpectedBoms = @()
$approvedBoms = @()

foreach ($file in $filesWithBom) {
    $normalizedPath = $file.Replace('\', '/')
    if ($approvedBomFiles.ContainsKey($normalizedPath)) {
        $approvedBoms += @{
            File = $file
            Reason = $approvedBomFiles[$normalizedPath]
        }
    } else {
        $unexpectedBoms += $file
    }
}

# Report approved BOMs
if ($approvedBoms.Count -gt 0) {
    Write-Host "=== Approved BOMs ($($approvedBoms.Count)) ===" -ForegroundColor Green
    foreach ($entry in $approvedBoms) {
        Write-Host "  [OK] $($entry.File)" -ForegroundColor Green
        Write-Host "       Reason: $($entry.Reason)" -ForegroundColor Gray
    }
    Write-Host ""
}

# Report unexpected BOMs
if ($unexpectedBoms.Count -gt 0) {
    Write-Host "=== UNEXPECTED BOMs FOUND ($($unexpectedBoms.Count)) ===" -ForegroundColor Red
    Write-Host ""
    Write-Host "The following files have UTF-8 BOMs but are not on the approved list:" -ForegroundColor Red
    Write-Host ""
    foreach ($file in $unexpectedBoms) {
        Write-Host "  [ERROR] $file" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Action required:" -ForegroundColor Yellow
    Write-Host "  1. If these files should NOT have BOM, remove it using scripts/misc/remove-bom.ps1" -ForegroundColor Yellow
    Write-Host "  2. If these files MUST have BOM, add them to the approved list in this script" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "For more information, see todos/active/TODO-20251019_utf8_no_bom.md" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Success
Write-Host "=== VALIDATION PASSED ===" -ForegroundColor Green
Write-Host "All files with BOM are on the approved list." -ForegroundColor Green
Write-Host "Project is BOM-compliant!" -ForegroundColor Green
Write-Host ""
exit 0
