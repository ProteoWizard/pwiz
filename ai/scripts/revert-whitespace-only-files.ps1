<#
.SYNOPSIS
    Reverts files that have only whitespace changes (no actual content differences).

.DESCRIPTION
    After running ResourcesOrganizer tools, some RESX files may have whitespace-only
    changes (e.g., tab-to-space conversion in XML comments). This script identifies
    and reverts those files to keep the diff clean.

.PARAMETER WhatIf
    Show which files would be reverted without actually reverting them.

.EXAMPLE
    .\revert-whitespace-only-files.ps1
    Reverts all files with whitespace-only changes.

.EXAMPLE
    .\revert-whitespace-only-files.ps1 -WhatIf
    Lists files that would be reverted without making changes.
#>

param(
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# Get list of modified files
$modifiedFiles = git diff --name-only

if (-not $modifiedFiles) {
    Write-Host "No modified files found."
    exit 0
}

$revertedCount = 0
$checkedCount = 0

foreach ($file in $modifiedFiles) {
    $checkedCount++

    # Check if diff is empty when ignoring whitespace
    $diffOutput = git diff --ignore-all-space -- $file 2>$null

    if ([string]::IsNullOrWhiteSpace($diffOutput)) {
        if ($WhatIf) {
            Write-Host "Would revert (whitespace-only): $file"
        } else {
            git checkout HEAD -- $file
            Write-Host "Reverted (whitespace-only): $file"
        }
        $revertedCount++
    }
}

Write-Host ""
if ($WhatIf) {
    Write-Host "Summary: $revertedCount of $checkedCount files would be reverted."
} else {
    Write-Host "Summary: Reverted $revertedCount of $checkedCount files with whitespace-only changes."
}
