# Convert only files currently added/modified in Git to CRLF and verify
#
# Usage: .\fix-crlf.ps1
# Scope: Only processes files in 'git status' (modified/added)
#
# This script was created during the webclient_replacement work (Oct 2025)
# when LLM tools (which prefer Linux-style LF) inadvertently changed
# line endings from Windows CRLF to LF-only, causing large Git diffs.
#
# The project standard is CRLF on Windows. Run this script before committing
# if you notice files with unwanted line ending changes.

# Get modified/added files (working tree)
$files = git status --porcelain | Where-Object { $_ -match '^( M|AM|A )' } |
         ForEach-Object { $_ -replace '^...','' }

if (-not $files) {
  Write-Host 'No modified/added files found.' -ForegroundColor Yellow
  exit 0
}

# Force CRLF
foreach ($f in $files) {
  if (Test-Path $f) {
    $absolutePath = (Resolve-Path -LiteralPath $f).Path
    $text  = [System.IO.File]::ReadAllText($absolutePath, [System.Text.UTF8Encoding]::new($false))
    $fixed = [regex]::Replace($text, "`r?`n", "`r`n")
    if ($fixed -ne $text) {
      [System.IO.File]::WriteAllText($absolutePath, $fixed, [System.Text.UTF8Encoding]::new($false))
      Write-Host "Converted: $f"
    }
  }
}

# Verify: report any files that still contain bare LFs
$bad = @()
foreach ($f in $files) {
  if (Test-Path $f) {
    $absolutePath = (Resolve-Path -LiteralPath $f).Path
    $s = [System.IO.File]::ReadAllText($absolutePath, [System.Text.UTF8Encoding]::new($false))
    if ($s -match '(?<!\r)\n') { $bad += $f }
  }
}

if ($bad.Count) {
  Write-Host "`nLF-only still present in:" -ForegroundColor Red
  $bad | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
  exit 1
} else {
  Write-Host "`nAll converted to CRLF." -ForegroundColor Green
}