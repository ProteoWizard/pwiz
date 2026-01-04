<#
.SYNOPSIS
    Verify developer environment for LLM-assisted Skyline development

.DESCRIPTION
    Checks all prerequisites from ai/docs/developer-setup-guide.md and outputs
    a summary report. Use this to quickly validate your workstation setup.

.EXAMPLE
    .\Verify-Environment.ps1
    Run all environment checks and display status report

.NOTES
    Author: LLM-assisted development
    See: ai/docs/developer-setup-guide.md

    MAINTENANCE: Keep this script in sync with ai/docs/developer-setup-guide.md.
    When prerequisites change in the documentation, update the checks here.
#>

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Skyline Development Environment Check" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Track results for summary
$results = @()

function Add-Result {
    param(
        [string]$Component,
        [string]$Status,
        [string]$Details,
        [bool]$Success
    )
    $script:results += [PSCustomObject]@{
        Component = $Component
        Status = $Status
        Details = $Details
        Success = $Success
    }
}

function Test-Command {
    param([string]$Command)
    try {
        $null = & where.exe $Command 2>$null
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

# 1. PowerShell Version
Write-Host "Checking PowerShell..." -ForegroundColor Gray
$psVersion = $PSVersionTable.PSVersion
if ($psVersion.Major -ge 7) {
    Add-Result "PowerShell" "OK" "$($psVersion.Major).$($psVersion.Minor).$($psVersion.Patch)" $true
} else {
    Add-Result "PowerShell" "WARN" "$($psVersion.Major).$($psVersion.Minor) (recommend 7+)" $false
}

# 2. Console Encoding
Write-Host "Checking console encoding..." -ForegroundColor Gray
$encoding = [Console]::OutputEncoding
if ($encoding.CodePage -eq 65001) {
    Add-Result "Console Encoding" "OK" "UTF-8 (CP65001)" $true
} else {
    Add-Result "Console Encoding" "WARN" "$($encoding.EncodingName) (CP$($encoding.CodePage)) - recommend UTF-8" $false
}

# 3. Node.js
Write-Host "Checking Node.js..." -ForegroundColor Gray
if (Test-Command "node") {
    try {
        $nodeVersion = & node --version 2>$null
        if ($nodeVersion -match 'v?(\d+\.\d+\.\d+)') {
            $version = [version]$Matches[1]
            if ($version -ge [version]"18.0.0") {
                Add-Result "Node.js" "OK" $Matches[1] $true
            } else {
                Add-Result "Node.js" "WARN" "$($Matches[1]) (recommend 18+ LTS)" $false
            }
        } else {
            Add-Result "Node.js" "OK" $nodeVersion $true
        }
    } catch {
        Add-Result "Node.js" "ERROR" "node found but version check failed" $false
    }
} else {
    Add-Result "Node.js" "MISSING" "Run: winget install OpenJS.NodeJS.LTS" $false
}

# 4. npm
Write-Host "Checking npm..." -ForegroundColor Gray
if (Test-Command "npm") {
    try {
        $npmVersion = & npm --version 2>$null
        if ($npmVersion -match '(\d+\.\d+\.\d+)') {
            Add-Result "npm" "OK" $Matches[1] $true
        } else {
            Add-Result "npm" "OK" $npmVersion $true
        }
    } catch {
        Add-Result "npm" "ERROR" "npm found but version check failed" $false
    }
} else {
    Add-Result "npm" "MISSING" "Install Node.js: winget install OpenJS.NodeJS.LTS" $false
}

# 5. Claude Code CLI
Write-Host "Checking Claude Code CLI..." -ForegroundColor Gray
try {
    $claudeVersion = & claude --version 2>$null
    if ($claudeVersion -match '(\d+\.\d+\.\d+)') {
        $version = $Matches[1]
        # Check if update is available
        $updateCheck = & claude update 2>&1 | Out-String
        if ($updateCheck -match 'is up to date') {
            Add-Result "Claude Code CLI" "OK" "$version (latest)" $true
        } elseif ($updateCheck -match 'available|updating|updated') {
            Add-Result "Claude Code CLI" "WARN" "$version (update available - run: claude update)" $false
        } else {
            Add-Result "Claude Code CLI" "OK" $version $true
        }
    } else {
        Add-Result "Claude Code CLI" "OK" $claudeVersion $true
    }
} catch {
    Add-Result "Claude Code CLI" "MISSING" "Run: npm install -g @anthropic-ai/claude-code" $false
}

# 4. ReSharper CLI (jb inspectcode)
Write-Host "Checking ReSharper CLI tools..." -ForegroundColor Gray
if (Test-Command "jb") {
    try {
        $jbOutput = & jb inspectcode --version 2>&1 | Out-String
        if ($jbOutput -match 'Inspect Code (\d+\.\d+\.\d+)') {
            Add-Result "ReSharper CLI (jb)" "OK" $Matches[1] $true
        } elseif ($jbOutput -match 'Version:\s*(\d+\.\d+\.\d+)') {
            Add-Result "ReSharper CLI (jb)" "OK" $Matches[1] $true
        } else {
            Add-Result "ReSharper CLI (jb)" "OK" "installed" $true
        }
    } catch {
        Add-Result "ReSharper CLI (jb)" "ERROR" "jb found but inspectcode failed" $false
    }
} else {
    Add-Result "ReSharper CLI (jb)" "MISSING" "Run: dotnet tool install -g JetBrains.ReSharper.GlobalTools" $false
}

# 5. dotCover CLI
Write-Host "Checking dotCover CLI..." -ForegroundColor Gray
if (Test-Command "dotCover") {
    try {
        $dotCoverOutput = & dotCover --version 2>&1 | Out-String
        # Match "dotCover Console Runner X.Y.Z" pattern
        if ($dotCoverOutput -match 'dotCover.*?(\d{4}\.\d+\.\d+)') {
            $version = $Matches[1]
            # Check for known buggy versions
            if ($version -match '^2025\.3\.' -or [version]$version -gt [version]"2025.2.99") {
                Add-Result "dotCover CLI" "WARN" "$version (2025.3.0+ has JSON export bug, use 2025.1.7)" $false
            } else {
                Add-Result "dotCover CLI" "OK" $version $true
            }
        } else {
            Add-Result "dotCover CLI" "OK" "installed" $true
        }
    } catch {
        Add-Result "dotCover CLI" "ERROR" "dotCover found but version check failed" $false
    }
} else {
    Add-Result "dotCover CLI" "MISSING" "Run: dotnet tool install --global JetBrains.dotCover.CommandLineTools --version 2025.1.7" $false
}

# 6. GitHub CLI
Write-Host "Checking GitHub CLI..." -ForegroundColor Gray
if (Test-Command "gh") {
    try {
        $ghOutput = & gh --version 2>$null | Select-Object -First 1
        if ($ghOutput -match '(\d+\.\d+\.\d+)') {
            Add-Result "GitHub CLI (gh)" "OK" $Matches[1] $true
        } else {
            Add-Result "GitHub CLI (gh)" "OK" "installed" $true
        }
    } catch {
        Add-Result "GitHub CLI (gh)" "ERROR" "gh found but version check failed" $false
    }
} else {
    Add-Result "GitHub CLI (gh)" "MISSING" "Run: winget install GitHub.cli" $false
}

# 7. GitHub CLI Authentication
Write-Host "Checking GitHub CLI authentication..." -ForegroundColor Gray
if (Test-Command "gh") {
    try {
        $authStatus = & gh auth status 2>&1
        if ($LASTEXITCODE -eq 0) {
            Add-Result "GitHub CLI Auth" "OK" "authenticated" $true
        } else {
            Add-Result "GitHub CLI Auth" "MISSING" "Run: gh auth login (in interactive terminal)" $false
        }
    } catch {
        Add-Result "GitHub CLI Auth" "MISSING" "Run: gh auth login (in interactive terminal)" $false
    }
} else {
    Add-Result "GitHub CLI Auth" "SKIP" "gh not installed" $false
}

# 8. Python
Write-Host "Checking Python..." -ForegroundColor Gray
if (Test-Command "python") {
    try {
        $pythonVersion = & python --version 2>$null
        if ($pythonVersion -match '(\d+\.\d+\.\d+)') {
            $version = [version]$Matches[1]
            if ($version -ge [version]"3.10.0") {
                Add-Result "Python" "OK" $Matches[1] $true
            } else {
                Add-Result "Python" "WARN" "$($Matches[1]) (recommend 3.10+)" $false
            }
        } else {
            Add-Result "Python" "OK" $pythonVersion $true
        }
    } catch {
        Add-Result "Python" "ERROR" "python found but version check failed" $false
    }
} else {
    Add-Result "Python" "MISSING" "Install Python 3.10+" $false
}

# 9. Python MCP packages
Write-Host "Checking Python MCP packages..." -ForegroundColor Gray
$labkeyInstalled = $false
$mcpInstalled = $false
try {
    $pipShow = & pip show labkey mcp 2>&1
    if ($pipShow -match 'Name: labkey') { $labkeyInstalled = $true }
    if ($pipShow -match 'Name: mcp') { $mcpInstalled = $true }
} catch {}

if ($labkeyInstalled -and $mcpInstalled) {
    Add-Result "Python packages (labkey, mcp)" "OK" "installed" $true
} elseif ($labkeyInstalled -or $mcpInstalled) {
    $missing = @()
    if (-not $labkeyInstalled) { $missing += "labkey" }
    if (-not $mcpInstalled) { $missing += "mcp" }
    Add-Result "Python packages" "PARTIAL" "Missing: $($missing -join ', '). Run: pip install $($missing -join ' ')" $false
} else {
    Add-Result "Python packages (labkey, mcp)" "MISSING" "Run: pip install mcp labkey" $false
}

# 10. netrc file for LabKey
Write-Host "Checking netrc credentials..." -ForegroundColor Gray
$netrcPath = Join-Path $env:USERPROFILE ".netrc"
$netrcAltPath = Join-Path $env:USERPROFILE "_netrc"
if ((Test-Path $netrcPath) -or (Test-Path $netrcAltPath)) {
    $foundPath = if (Test-Path $netrcPath) { ".netrc" } else { "_netrc" }
    Add-Result "netrc credentials" "OK" "$foundPath exists" $true
} else {
    Add-Result "netrc credentials" "MISSING" "Create ~\.netrc with shared agent credentials (see developer-setup-guide.md)" $false
}

# 11. LabKey MCP Server registration
Write-Host "Checking LabKey MCP server..." -ForegroundColor Gray
try {
    $serverPath = Join-Path $repoRoot "ai\mcp\LabKeyMcp\server.py"
    $serverExists = Test-Path $serverPath
    $mcpList = & claude mcp list 2>&1

    # Parse the registered path from output like: "labkey: python C:/path/to/server.py - ✓ Connected"
    $registeredPath = $null
    if ($mcpList -match 'labkey:\s*python\s+([^\s]+server\.py)') {
        $registeredPath = $matches[1] -replace '/', '\'  # Normalize to backslashes
    }

    $isConnected = $mcpList -match 'labkey.*Connected'
    $isRegistered = $null -ne $registeredPath

    # Normalize expected path for comparison
    $expectedPathNormalized = $serverPath -replace '/', '\'
    $pathMatches = $isRegistered -and ($registeredPath -eq $expectedPathNormalized)

    if ($isConnected -and $pathMatches) {
        Add-Result "LabKey MCP Server" "OK" "registered and connected" $true
    } elseif ($isRegistered -and -not $pathMatches) {
        # Registered but pointing to wrong path
        Add-Result "LabKey MCP Server" "WARN" "registered at wrong path. Run: claude mcp remove labkey && claude mcp add labkey -- python $serverPath" $false
    } elseif ($isRegistered -and $pathMatches) {
        # Registered at correct path, file exists, but not connected (normal when not in active session)
        Add-Result "LabKey MCP Server" "WARN" "registered but not connected (normal outside active session)" $false
    } elseif ($serverExists) {
        # Server exists but not registered
        Add-Result "LabKey MCP Server" "MISSING" "Run: claude mcp add labkey -- python $serverPath" $false
    } else {
        # Neither registered nor server file found
        Add-Result "LabKey MCP Server" "ERROR" "server.py not found at $serverPath" $false
    }
} catch {
    Add-Result "LabKey MCP Server" "ERROR" "Could not check MCP status: $_" $false
}

# 12. Git autocrlf (check effective value from any scope: system, global, or local)
Write-Host "Checking Git configuration..." -ForegroundColor Gray
try {
    $autocrlf = & git config core.autocrlf 2>$null
    if ($autocrlf -eq "true") {
        # Show where it's set
        $origin = & git config --show-origin core.autocrlf 2>$null
        $scope = if ($origin -match 'file:.*gitconfig') { "(system)" }
                 elseif ($origin -match '\.gitconfig') { "(global)" }
                 else { "" }
        Add-Result "Git core.autocrlf" "OK" "true $scope".Trim() $true
    } elseif ($autocrlf) {
        Add-Result "Git core.autocrlf" "WARN" "$autocrlf (recommend: true)" $false
    } else {
        Add-Result "Git core.autocrlf" "MISSING" "Run: git config --global core.autocrlf true" $false
    }
} catch {
    Add-Result "Git core.autocrlf" "ERROR" "Could not check git config" $false
}

# 13. Git pull.rebase (check effective value from any scope)
try {
    $pullRebase = & git config pull.rebase 2>$null
    if ($pullRebase -eq "false") {
        Add-Result "Git pull.rebase" "OK" "false (merge)" $true
    } elseif ($pullRebase -eq "true") {
        Add-Result "Git pull.rebase" "INFO" "true (rebase) - consider 'false' for safer merges" $true
    } elseif ($pullRebase) {
        Add-Result "Git pull.rebase" "INFO" "$pullRebase" $true
    } else {
        # Not set means Git's default behavior (merge in most versions)
        Add-Result "Git pull.rebase" "INFO" "not set (defaults to merge)" $true
    }
} catch {
    Add-Result "Git pull.rebase" "ERROR" "Could not check git config" $false
}

# Print Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Environment Check Results" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$maxComponentLen = ($results | ForEach-Object { $_.Component.Length } | Measure-Object -Maximum).Maximum
$maxStatusLen = 7  # "MISSING" is longest

foreach ($result in $results) {
    $component = $result.Component.PadRight($maxComponentLen)
    $statusColor = switch ($result.Status) {
        "OK" { "Green" }
        "WARN" { "Yellow" }
        "PARTIAL" { "Yellow" }
        "INFO" { "Cyan" }
        "SKIP" { "Gray" }
        default { "Red" }
    }
    $statusText = "[$($result.Status)]".PadRight($maxStatusLen + 2)

    Write-Host "  $component " -NoNewline
    Write-Host $statusText -ForegroundColor $statusColor -NoNewline
    Write-Host " $($result.Details)"
}

# Summary counts
$okCount = ($results | Where-Object { $_.Status -eq "OK" }).Count
$warnCount = ($results | Where-Object { $_.Status -in @("WARN", "PARTIAL") }).Count
$missingCount = ($results | Where-Object { $_.Status -eq "MISSING" }).Count
$errorCount = ($results | Where-Object { $_.Status -eq "ERROR" }).Count

Write-Host "`n----------------------------------------" -ForegroundColor Gray
Write-Host "  OK: $okCount | Warnings: $warnCount | Missing: $missingCount | Errors: $errorCount" -ForegroundColor Gray

if ($missingCount -eq 0 -and $errorCount -eq 0 -and $warnCount -eq 0) {
    Write-Host "`n[OK] Environment is fully configured for LLM-assisted development" -ForegroundColor Green
} elseif ($missingCount -eq 0 -and $errorCount -eq 0) {
    Write-Host "`n[OK] Environment is ready (some optional items have warnings)" -ForegroundColor Yellow
} else {
    Write-Host "`n[ACTION REQUIRED] Some components need configuration" -ForegroundColor Red
    Write-Host "See: ai/docs/developer-setup-guide.md for installation instructions`n" -ForegroundColor Gray
}

exit ($missingCount + $errorCount)
