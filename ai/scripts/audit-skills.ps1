<#
.SYNOPSIS
    Audits Claude Code skill sizes in the project.

.DESCRIPTION
    Scans .claude/skills for SKILL.md files and reports their character counts.
    Claude Code has a character limit on skills (reportedly 30,000 characters).
    This script helps monitor skill sizes to stay within limits.

.PARAMETER SkillsRoot
    Root directory of skills (default: .claude/skills relative to repo root)

.PARAMETER WarningThreshold
    Character count threshold for warnings (default: 20000)

.PARAMETER ErrorThreshold
    Character count threshold for errors (default: 30000)

.EXAMPLE
    .\audit-skills.ps1
    Audit all skills and show character counts

.EXAMPLE
    .\audit-skills.ps1 -WarningThreshold 15000
    Audit with a lower warning threshold
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$SkillsRoot = $null,
    [Parameter(Mandatory=$false)]
    [int]$WarningThreshold = 20000,
    [Parameter(Mandatory=$false)]
    [int]$ErrorThreshold = 30000
)

# Determine skills root directory
if ([string]::IsNullOrEmpty($SkillsRoot))
{
    $scriptPath = $PSScriptRoot
    if ([string]::IsNullOrEmpty($scriptPath))
    {
        $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    # From ai\scripts, go up to repo root, then into .claude\skills
    $repoRoot = Split-Path -Parent (Split-Path -Parent $scriptPath)
    $SkillsRoot = Join-Path $repoRoot ".claude\skills"
}

if (-not (Test-Path $SkillsRoot))
{
    Write-Error "Skills root directory not found: $SkillsRoot"
    exit 1
}

Write-Host "Auditing Claude Code skills in: $SkillsRoot" -ForegroundColor Cyan
Write-Host "Warning threshold: $WarningThreshold characters" -ForegroundColor Yellow
Write-Host "Error threshold: $ErrorThreshold characters" -ForegroundColor Red
Write-Host ""

$results = @()

# Find all SKILL.md files
$skillFiles = Get-ChildItem -Path $SkillsRoot -Filter "SKILL.md" -Recurse -File -ErrorAction SilentlyContinue

foreach ($file in $skillFiles)
{
    $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
    $charCount = if ($content) { $content.Length } else { 0 }
    $lineCount = if ($content) { ($content -split "`n").Count } else { 0 }

    # Extract skill name from parent directory
    $skillName = $file.Directory.Name

    # Determine status
    $status = "OK"
    if ($charCount -ge $ErrorThreshold)
    {
        $status = "ERROR"
    }
    elseif ($charCount -ge $WarningThreshold)
    {
        $status = "WARNING"
    }

    $results += [PSCustomObject]@{
        Name = $skillName
        Characters = $charCount
        Lines = $lineCount
        Status = $status
        Path = $file.FullName.Substring($SkillsRoot.Length + 1)
        PercentOfLimit = [math]::Round(($charCount / $ErrorThreshold) * 100, 1)
    }
}

# Sort by character count descending
$results = $results | Sort-Object -Property Characters -Descending

# Display results
Write-Host "Skill Character Counts (sorted by size, descending):" -ForegroundColor Cyan
Write-Host ("=" * 90)
Write-Host ("{0,-30} {1,12} {2,8} {3,10} {4,10}" -f "Skill Name", "Characters", "Lines", "% of Limit", "Status")
Write-Host ("-" * 90)

$totalChars = 0
$warningCount = 0
$errorCount = 0

foreach ($result in $results)
{
    $totalChars += $result.Characters

    $color = switch ($result.Status)
    {
        "ERROR" { $errorCount++; "Red" }
        "WARNING" { $warningCount++; "Yellow" }
        default { "Green" }
    }

    Write-Host ("{0,-30} {1,12:N0} {2,8} {3,9}% {4,10}" -f `
        $result.Name, `
        $result.Characters, `
        $result.Lines, `
        $result.PercentOfLimit, `
        $result.Status) -ForegroundColor $color
}

Write-Host ("=" * 90)
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host ("  Total skills: {0}" -f $results.Count)
Write-Host ("  Total characters: {0:N0}" -f $totalChars)
Write-Host ("  Average per skill: {0:N0}" -f ($totalChars / [Math]::Max(1, $results.Count)))

if ($errorCount -gt 0)
{
    Write-Host ("  Skills over limit ({0}+): {1}" -f $ErrorThreshold, $errorCount) -ForegroundColor Red
}
if ($warningCount -gt 0)
{
    Write-Host ("  Skills approaching limit ({0}+): {1}" -f $WarningThreshold, $warningCount) -ForegroundColor Yellow
}
if ($errorCount -eq 0 -and $warningCount -eq 0)
{
    Write-Host "  All skills within recommended limits" -ForegroundColor Green
}

Write-Host ""

# Return exit code based on errors
if ($errorCount -gt 0)
{
    exit 1
}
exit 0
