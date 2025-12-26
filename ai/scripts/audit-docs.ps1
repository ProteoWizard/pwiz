<#
.SYNOPSIS
    Audits Claude Code documentation and configuration files.

.DESCRIPTION
    Scans .claude/ and ai/ directories for documentation files and reports
    character counts (for skills/commands) and line counts (for docs).
    Helps monitor file sizes to stay within Claude Code limits.

.PARAMETER Section
    Which section to audit: all, skills, commands, ai, docs, mcp
    Default: all

.PARAMETER WarningThreshold
    Character count threshold for warnings on skills/commands (default: 20000)

.PARAMETER ErrorThreshold
    Character count threshold for errors on skills/commands (default: 30000)

.EXAMPLE
    .\audit-docs.ps1
    Full audit of all sections

.EXAMPLE
    .\audit-docs.ps1 -Section skills
    Audit only skills

.EXAMPLE
    .\audit-docs.ps1 -Section mcp
    Audit only MCP documentation
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("all", "skills", "commands", "ai", "docs", "mcp")]
    [string]$Section = "all",
    [Parameter(Mandatory=$false)]
    [int]$WarningThreshold = 20000,
    [Parameter(Mandatory=$false)]
    [int]$ErrorThreshold = 30000
)

# Determine repo root
$scriptPath = $PSScriptRoot
if ([string]::IsNullOrEmpty($scriptPath))
{
    $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptPath)

function Get-FileStats
{
    param(
        [string]$Path,
        [string]$Filter = "*.md",
        [switch]$Recurse
    )

    $files = if ($Recurse) {
        Get-ChildItem -Path $Path -Filter $Filter -Recurse -File -ErrorAction SilentlyContinue
    } else {
        Get-ChildItem -Path $Path -Filter $Filter -File -ErrorAction SilentlyContinue
    }

    $results = @()
    foreach ($file in $files)
    {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
        $charCount = if ($content) { $content.Length } else { 0 }
        $lineCount = if ($content) { ($content -split "`n").Count } else { 0 }

        $results += [PSCustomObject]@{
            Name = $file.Name
            RelativePath = $file.FullName.Substring($repoRoot.Length + 1)
            Characters = $charCount
            Lines = $lineCount
        }
    }
    return $results
}

function Show-CharacterReport
{
    param(
        [string]$Title,
        [array]$Results,
        [int]$WarnAt,
        [int]$ErrorAt
    )

    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
    Write-Host ("{0,-45} {1,12} {2,8} {3,10}" -f "File", "Characters", "Lines", "Status")
    Write-Host ("-" * 80)

    $sorted = $Results | Sort-Object -Property Characters -Descending
    $totalChars = 0
    $warnCount = 0
    $errCount = 0

    foreach ($item in $sorted)
    {
        $totalChars += $item.Characters
        $status = "OK"
        $color = "Green"

        if ($item.Characters -ge $ErrorAt)
        {
            $status = "ERROR"
            $color = "Red"
            $errCount++
        }
        elseif ($item.Characters -ge $WarnAt)
        {
            $status = "WARNING"
            $color = "Yellow"
            $warnCount++
        }

        $displayName = if ($item.Name.Length -gt 42) { $item.Name.Substring(0, 39) + "..." } else { $item.Name }
        Write-Host ("{0,-45} {1,12:N0} {2,8} {3,10}" -f $displayName, $item.Characters, $item.Lines, $status) -ForegroundColor $color
    }

    Write-Host ("-" * 80)
    Write-Host ("Total: {0} files, {1:N0} characters" -f $sorted.Count, $totalChars)
    if ($warnCount -gt 0) { Write-Host "  Warnings: $warnCount" -ForegroundColor Yellow }
    if ($errCount -gt 0) { Write-Host "  Errors: $errCount" -ForegroundColor Red }

    return @{ Total = $totalChars; Count = $sorted.Count; Warnings = $warnCount; Errors = $errCount }
}

function Show-LineReport
{
    param(
        [string]$Title,
        [array]$Results
    )

    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
    Write-Host ("{0,-55} {1,8} {2,12}" -f "File", "Lines", "Characters")
    Write-Host ("-" * 80)

    $sorted = $Results | Sort-Object -Property Lines -Descending
    $totalLines = 0
    $totalChars = 0

    foreach ($item in $sorted)
    {
        $totalLines += $item.Lines
        $totalChars += $item.Characters

        $displayName = if ($item.Name.Length -gt 52) { $item.Name.Substring(0, 49) + "..." } else { $item.Name }
        Write-Host ("{0,-55} {1,8} {2,12:N0}" -f $displayName, $item.Lines, $item.Characters)
    }

    Write-Host ("-" * 80)
    Write-Host ("Total: {0} files, {1:N0} lines, {2:N0} characters" -f $sorted.Count, $totalLines, $totalChars)

    return @{ TotalLines = $totalLines; TotalChars = $totalChars; Count = $sorted.Count }
}

# Header
Write-Host ""
Write-Host "Claude Code Documentation Audit" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot"
Write-Host "Section: $Section"
Write-Host ""

$summary = @{}

# Skills audit
if ($Section -eq "all" -or $Section -eq "skills")
{
    $skillsPath = Join-Path $repoRoot ".claude\skills"
    if (Test-Path $skillsPath)
    {
        $skillFiles = Get-ChildItem -Path $skillsPath -Filter "SKILL.md" -Recurse -File -ErrorAction SilentlyContinue
        $skillResults = @()
        foreach ($file in $skillFiles)
        {
            $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
            $charCount = if ($content) { $content.Length } else { 0 }
            $lineCount = if ($content) { ($content -split "`n").Count } else { 0 }
            $skillResults += [PSCustomObject]@{
                Name = $file.Directory.Name
                RelativePath = $file.FullName.Substring($repoRoot.Length + 1)
                Characters = $charCount
                Lines = $lineCount
            }
        }
        $summary["skills"] = Show-CharacterReport -Title "Skills (.claude/skills/*/SKILL.md)" -Results $skillResults -WarnAt $WarningThreshold -ErrorAt $ErrorThreshold
    }
}

# Commands audit
if ($Section -eq "all" -or $Section -eq "commands")
{
    $commandsPath = Join-Path $repoRoot ".claude\commands"
    if (Test-Path $commandsPath)
    {
        $commandResults = Get-FileStats -Path $commandsPath -Filter "*.md"
        $summary["commands"] = Show-CharacterReport -Title "Commands (.claude/commands/*.md)" -Results $commandResults -WarnAt $WarningThreshold -ErrorAt $ErrorThreshold
    }
}

# ai/*.md audit (top-level only)
if ($Section -eq "all" -or $Section -eq "ai")
{
    $aiPath = Join-Path $repoRoot "ai"
    if (Test-Path $aiPath)
    {
        $aiResults = Get-FileStats -Path $aiPath -Filter "*.md"
        $summary["ai"] = Show-LineReport -Title "AI Root (ai/*.md)" -Results $aiResults
    }
}

# ai/docs/*.md audit (top-level only)
if ($Section -eq "all" -or $Section -eq "docs")
{
    $docsPath = Join-Path $repoRoot "ai\docs"
    if (Test-Path $docsPath)
    {
        $docsResults = Get-FileStats -Path $docsPath -Filter "*.md"
        $summary["docs"] = Show-LineReport -Title "Documentation (ai/docs/*.md)" -Results $docsResults
    }
}

# ai/docs/mcp/*.md audit
if ($Section -eq "all" -or $Section -eq "mcp")
{
    $mcpPath = Join-Path $repoRoot "ai\docs\mcp"
    if (Test-Path $mcpPath)
    {
        $mcpResults = Get-FileStats -Path $mcpPath -Filter "*.md"
        $summary["mcp"] = Show-LineReport -Title "MCP Documentation (ai/docs/mcp/*.md)" -Results $mcpResults
    }
}

# Overall summary
if ($Section -eq "all")
{
    Write-Host ""
    Write-Host "=== SUMMARY ===" -ForegroundColor Cyan
    Write-Host ""

    $totalErrors = 0
    $totalWarnings = 0

    foreach ($key in $summary.Keys)
    {
        $s = $summary[$key]
        if ($s.Errors) { $totalErrors += $s.Errors }
        if ($s.Warnings) { $totalWarnings += $s.Warnings }
    }

    if ($totalErrors -eq 0 -and $totalWarnings -eq 0)
    {
        Write-Host "All documentation within recommended limits." -ForegroundColor Green
    }
    else
    {
        if ($totalWarnings -gt 0) { Write-Host "Total warnings: $totalWarnings" -ForegroundColor Yellow }
        if ($totalErrors -gt 0) { Write-Host "Total errors: $totalErrors" -ForegroundColor Red }
    }
}

Write-Host ""

# Exit code
$exitCode = 0
foreach ($key in $summary.Keys)
{
    if ($summary[$key].Errors -gt 0) { $exitCode = 1 }
}
exit $exitCode
