<#
.SYNOPSIS
    Generates ai/TOC.md - a Table of Contents for all AI documentation.

.DESCRIPTION
    Scans ai/*.md, ai/docs/*.md, .claude/skills/*/SKILL.md, and .claude/commands/*.md
    to generate a comprehensive TOC with descriptions and size metrics.

    Preserves existing descriptions from previous TOC.md runs.
    Extracts descriptions from frontmatter for skills and commands.
    Marks new files as "**NEW** - needs description" for Claude Code to fill in.

.EXAMPLE
    pwsh -File ./ai/scripts/Generate-TOC.ps1

.EXAMPLE
    pwsh -File ./ai/scripts/Generate-TOC.ps1 -Verbose
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Find repository root
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    Write-Error "Not in a git repository"
    exit 1
}

$tocPath = Join-Path $repoRoot "ai/TOC.md"

# Parse existing TOC.md to preserve descriptions
function Get-ExistingDescriptions {
    param([string]$TocPath)

    $descriptions = @{}

    if (-not (Test-Path $TocPath)) {
        Write-Verbose "No existing TOC.md found"
        return $descriptions
    }

    $content = Get-Content $TocPath -Raw

    # Match table rows: | [displayName](linkPath) | description | count |
    # Use the link path as key to avoid collisions between files with same name
    $pattern = '\|\s*\[([^\]]+)\]\(([^)]+)\)\s*\|\s*([^|]+)\s*\|\s*[\d,]+\s*\|'
    $matches = [regex]::Matches($content, $pattern)

    foreach ($match in $matches) {
        $displayName = $match.Groups[1].Value.Trim()
        $linkPath = $match.Groups[2].Value.Trim()
        $description = $match.Groups[3].Value.Trim()

        # Skip NEW markers - we want to regenerate those
        if ($description -notmatch '^\*\*NEW\*\*') {
            # Use link path as key for uniqueness (avoids collisions with same-named files)
            $descriptions[$linkPath] = $description
            Write-Verbose "Preserved description for: $linkPath"
        }
    }

    Write-Verbose "Loaded $($descriptions.Count) existing descriptions"
    return $descriptions
}

# Extract description from YAML frontmatter
function Get-FrontmatterDescription {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) { return $null }

    $content = Get-Content $FilePath -Raw

    # Match YAML frontmatter: ---\n...\n---
    if ($content -match '(?s)^---\s*\n(.+?)\n---') {
        $yaml = $Matches[1]
        if ($yaml -match 'description:\s*(.+)') {
            return $Matches[1].Trim().Trim('"').Trim("'")
        }
    }

    return $null
}

# Get line count for a file
function Get-LineCount {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) { return 0 }
    return (Get-Content $FilePath | Measure-Object -Line).Lines
}

# Get character count for a file
function Get-CharCount {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) { return 0 }
    return (Get-Content $FilePath -Raw).Length
}

# Format number with commas
function Format-Number {
    param([int]$Number)
    return $Number.ToString("N0")
}

# Generate table rows for a set of files
function New-TableRows {
    param(
        [string[]]$Files,
        [string]$BaseDir,
        [string]$LinkPrefix,
        [hashtable]$ExistingDescriptions,
        [switch]$UseCharCount,
        [switch]$ExtractFromFrontmatter,
        [switch]$UseParentFolderName
    )

    $rows = @()

    foreach ($file in $Files | Sort-Object) {
        $filename = Split-Path $file -Leaf

        # For skills, use the parent folder name as the display name
        if ($UseParentFolderName) {
            $displayName = Split-Path (Split-Path $file -Parent) -Leaf
        } else {
            $displayName = $filename
        }

        # Calculate relative path from repo root
        $relativePath = $file.Substring($repoRoot.Length).TrimStart('\', '/').Replace('\', '/')

        # Build the link path relative to ai/TOC.md
        if ($relativePath.StartsWith('ai/')) {
            $linkPath = $relativePath.Substring(3)  # Remove 'ai/' prefix
        } elseif ($relativePath.StartsWith('.claude/')) {
            $linkPath = "../$relativePath"
        } else {
            $linkPath = $relativePath
        }

        # Get description
        $description = $null

        if ($ExtractFromFrontmatter) {
            $description = Get-FrontmatterDescription -FilePath $file
        }

        # Look up existing description by link path (unique key avoids same-name collisions)
        if (-not $description -and $ExistingDescriptions.ContainsKey($linkPath)) {
            $description = $ExistingDescriptions[$linkPath]
        }

        if (-not $description) {
            $description = "**NEW** - needs description"
        }

        # Get size metric
        if ($UseCharCount) {
            $size = Format-Number (Get-CharCount -FilePath $file)
        } else {
            $size = Format-Number (Get-LineCount -FilePath $file)
        }

        $rows += "| [$displayName]($linkPath) | $description | $size |"
    }

    return $rows
}

Write-Host "Generating ai/TOC.md..."

# Load existing descriptions
$existingDescriptions = Get-ExistingDescriptions -TocPath $tocPath

# Scan all documentation locations
$coreFiles = Get-ChildItem -Path (Join-Path $repoRoot "ai") -Filter "*.md" -File |
    Where-Object { $_.Name -ne "TOC.md" } |
    Select-Object -ExpandProperty FullName

$guideFiles = Get-ChildItem -Path (Join-Path $repoRoot "ai/docs") -Filter "*.md" -File -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty FullName

$mcpFiles = Get-ChildItem -Path (Join-Path $repoRoot "ai/docs/mcp") -Filter "*.md" -File -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty FullName

$skillFiles = Get-ChildItem -Path (Join-Path $repoRoot ".claude/skills") -Directory -ErrorAction SilentlyContinue |
    ForEach-Object { Join-Path $_.FullName "SKILL.md" } |
    Where-Object { Test-Path $_ }

$commandFiles = Get-ChildItem -Path (Join-Path $repoRoot ".claude/commands") -Filter "*.md" -File -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty FullName

# Generate TOC content
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm"

$tocContent = @"
# AI Documentation Table of Contents

> Auto-generated by ``ai/scripts/Generate-TOC.ps1`` on $timestamp
>
> Descriptions marked **NEW** need to be filled in by reviewing the file.
> Existing descriptions are preserved across regenerations.

## Summary

| Category | Count | Location |
|----------|-------|----------|
| Core Documents | $($coreFiles.Count) | ``ai/*.md`` |
| Guides | $($guideFiles.Count) | ``ai/docs/*.md`` |
| MCP Data Sources | $($mcpFiles.Count) | ``ai/docs/mcp/*.md`` |
| Skills | $($skillFiles.Count) | ``.claude/skills/*/SKILL.md`` |
| Commands | $($commandFiles.Count) | ``.claude/commands/*.md`` |

---

## Core Documents (ai/*.md)

Essential project documentation - read these first.

| File | Description | Lines |
|------|-------------|------:|
"@

$coreRows = New-TableRows -Files $coreFiles -BaseDir "ai" -LinkPrefix "" -ExistingDescriptions $existingDescriptions
$tocContent += "`n" + ($coreRows -join "`n")

$tocContent += @"


---

## Guides (ai/docs/*.md)

In-depth documentation on specific topics.

| File | Description | Lines |
|------|-------------|------:|
"@

$guideRows = New-TableRows -Files $guideFiles -BaseDir "ai/docs" -LinkPrefix "docs/" -ExistingDescriptions $existingDescriptions
$tocContent += "`n" + ($guideRows -join "`n")

$tocContent += @"


---

## MCP Data Sources (ai/docs/mcp/*.md)

Documentation for external data access via MCP servers and CLI tools.

| File | Description | Lines |
|------|-------------|------:|
"@

$mcpRows = New-TableRows -Files $mcpFiles -BaseDir "ai/docs/mcp" -LinkPrefix "docs/mcp/" -ExistingDescriptions $existingDescriptions
$tocContent += "`n" + ($mcpRows -join "`n")

$tocContent += @"


---

## Skills (.claude/skills/)

Auto-activated contexts for specific work areas. Character count shown (skill space is limited).

| Skill | Description | Chars |
|-------|-------------|------:|
"@

$skillRows = New-TableRows -Files $skillFiles -BaseDir ".claude/skills" -LinkPrefix "../.claude/skills/" -ExistingDescriptions $existingDescriptions -UseCharCount -ExtractFromFrontmatter -UseParentFolderName
$tocContent += "`n" + ($skillRows -join "`n")

$tocContent += @"


---

## Commands (.claude/commands/)

Slash commands for specific workflows. Invoke with `/<command-name>`.

| Command | Description | Chars |
|---------|-------------|------:|
"@

$commandRows = New-TableRows -Files $commandFiles -BaseDir ".claude/commands" -LinkPrefix "../.claude/commands/" -ExistingDescriptions $existingDescriptions -UseCharCount -ExtractFromFrontmatter
$tocContent += "`n" + ($commandRows -join "`n")

$tocContent += @"


---

## Maintenance

To regenerate this TOC:

```powershell
pwsh -File ./ai/scripts/Generate-TOC.ps1
```

This is automatically run during `/pw-aicontextsync`.

### When descriptions need updating

- **NEW files**: Review the file and add a one-line description
- **Renamed files**: Appear as NEW (old entry auto-removed)
- **Content changes**: Update description if purpose changed significantly
"@

# Write the TOC
$tocContent | Set-Content -Path $tocPath -Encoding UTF8

Write-Host "Generated: $tocPath"
Write-Host "  Core documents: $($coreFiles.Count)"
Write-Host "  Guides: $($guideFiles.Count)"
Write-Host "  MCP data sources: $($mcpFiles.Count)"
Write-Host "  Skills: $($skillFiles.Count)"
Write-Host "  Commands: $($commandFiles.Count)"

# Report any NEW items
$newCount = ($tocContent | Select-String -Pattern '\*\*NEW\*\*' -AllMatches).Matches.Count
if ($newCount -gt 0) {
    Write-Host ""
    Write-Host "NOTE: $newCount items marked as **NEW** need descriptions" -ForegroundColor Yellow
}
