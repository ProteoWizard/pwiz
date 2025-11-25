<#
.SYNOPSIS
    Synchronize ai-context branch with master (bidirectional).

.DESCRIPTION
    Manages the ai-context branch lifecycle:
    - Pull latest changes from master into ai-context (daily maintenance)
    - Merge ai-context back to master (batch update of ai/ documentation)
    - Provides dry-run mode to preview changes before executing

.PARAMETER Direction
    Sync direction:
    - 'FromMaster': Pull master changes into ai-context (daily maintenance)
    - 'ToMaster': Merge ai-context back to master (batch documentation update)

.PARAMETER DryRun
    Preview changes without executing merge/push operations.

.PARAMETER Push
    Automatically push after merge (default: prompt user).

.EXAMPLE
    .\ai\scripts\sync-ai-context.ps1 -Direction FromMaster
    Pull latest master changes into ai-context branch.

.EXAMPLE
    .\ai\scripts\sync-ai-context.ps1 -Direction ToMaster -DryRun
    Preview what would be merged to master without executing.

.EXAMPLE
    .\ai\scripts\sync-ai-context.ps1 -Direction ToMaster -Push
    Merge ai-context to master and push automatically.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('FromMaster', 'ToMaster')]
    [string]$Direction,

    [Parameter(Mandatory=$false)]
    [switch]$DryRun,

    [Parameter(Mandatory=$false)]
    [switch]$Push
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Write-Status {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Get-CurrentBranch {
    $branch = git rev-parse --abbrev-ref HEAD 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to determine current branch"
    }
    return $branch
}

function Assert-CleanWorkingTree {
    $status = git status --porcelain
    if ($status) {
        Write-Error-Custom "Working tree is not clean. Commit or stash changes first:"
        git status --short
        exit 1
    }
}

function Get-CommitsBetween {
    param(
        [string]$From,
        [string]$To
    )
    $commits = git log --oneline "$From..$To" 2>$null
    if ($LASTEXITCODE -ne 0) {
        return @()
    }
    return $commits
}

function Show-MergePreview {
    param(
        [string]$SourceBranch,
        [string]$TargetBranch
    )
    
    Write-Status "`nMerge Preview: $SourceBranch → $TargetBranch"
    Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
    
    $commits = Get-CommitsBetween "origin/$TargetBranch" "origin/$SourceBranch"
    if ($commits.Count -eq 0) {
        Write-Warning-Custom "No new commits to merge from $SourceBranch"
        return $false
    }
    
    Write-Host "`nCommits to be merged ($($commits.Count)):" -ForegroundColor Yellow
    $commits | ForEach-Object { Write-Host "  $_" }
    
    Write-Host "`nAffected files:" -ForegroundColor Yellow
    git diff --name-status "origin/$TargetBranch...origin/$SourceBranch" | ForEach-Object {
        Write-Host "  $_"
    }
    
    return $true
}

function Sync-FromMaster {
    Write-Status "`n═══════════════════════════════════════════════════════"
    Write-Status "  Sync ai-context FROM master (daily maintenance)"
    Write-Status "═══════════════════════════════════════════════════════`n"
    
    # Fetch latest
    Write-Status "Fetching latest from origin..."
    git fetch origin master ai-context
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    
    # Preview
    $hasChanges = Show-MergePreview "master" "ai-context"
    if (-not $hasChanges -and -not $DryRun) {
        Write-Success "ai-context is already up to date with master"
        return
    }
    
    if ($DryRun) {
        Write-Warning-Custom "`n[DRY RUN] Would merge master into ai-context (no changes made)"
        return
    }
    
    # Ensure on ai-context branch
    $currentBranch = Get-CurrentBranch
    if ($currentBranch -ne 'ai-context') {
        Write-Status "`nSwitching to ai-context branch..."
        git checkout ai-context
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    
    # Pull latest ai-context
    Write-Status "Pulling latest ai-context..."
    git pull origin ai-context
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    
    # Merge master
    Write-Status "`nMerging master into ai-context..."
    git merge origin/master --no-ff -m "Merge master into ai-context: sync with latest code changes"
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Custom "Merge conflict detected. Resolve conflicts manually, then run:"
        Write-Host "  git merge --continue" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Success "Merged master into ai-context"
    
    # Push
    if ($Push) {
        Write-Status "`nPushing ai-context..."
        git push origin ai-context
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Write-Success "Pushed ai-context to origin"
    } else {
        Write-Warning-Custom "`nMerge complete locally. Push with:"
        Write-Host "  git push origin ai-context" -ForegroundColor Yellow
    }
}

function Sync-ToMaster {
    Write-Status "`n═══════════════════════════════════════════════════════"
    Write-Status "  Sync ai-context TO master (batch documentation update)"
    Write-Status "═══════════════════════════════════════════════════════`n"
    
    # Fetch latest
    Write-Status "Fetching latest from origin..."
    git fetch origin master ai-context
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    
    # Preview
    $hasChanges = Show-MergePreview "ai-context" "master"
    if (-not $hasChanges -and -not $DryRun) {
        Write-Success "master already has all ai-context changes"
        return
    }
    
    if ($DryRun) {
        Write-Warning-Custom "`n[DRY RUN] Would merge ai-context into master (no changes made)"
        Write-Host "`nTo execute merge, create PR or run:" -ForegroundColor Yellow
        Write-Host "  .\\ai\\scripts\\sync-ai-context.ps1 -Direction ToMaster -Push" -ForegroundColor Yellow
        return
    }
    
    # Warn about direct master merge
    Write-Warning-Custom "`nYou are about to merge ai-context → master directly."
    Write-Host "Consider creating a PR instead for team visibility:" -ForegroundColor Yellow
    Write-Host "  1. Push ai-context: git push origin ai-context" -ForegroundColor Yellow
    Write-Host "  2. Open PR: ai-context → master on GitHub" -ForegroundColor Yellow
    Write-Host "  3. Merge via GitHub (requires approval if branch protection enabled)`n" -ForegroundColor Yellow
    
    $response = Read-Host "Continue with direct merge? (y/N)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-Warning-Custom "Merge cancelled"
        return
    }
    
    # Ensure on master branch
    $currentBranch = Get-CurrentBranch
    if ($currentBranch -ne 'master') {
        Write-Status "`nSwitching to master branch..."
        git checkout master
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    
    # Pull latest master
    Write-Status "Pulling latest master..."
    git pull origin master
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    
    # Merge ai-context
    Write-Status "`nMerging ai-context into master..."
    git merge origin/ai-context --no-ff -m "Merge ai-context: batch update ai/ documentation"
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Custom "Merge conflict detected. Resolve conflicts manually, then run:"
        Write-Host "  git merge --continue" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Success "Merged ai-context into master"
    
    # Push
    if ($Push) {
        Write-Status "`nPushing master..."
        git push origin master
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Write-Success "Pushed master to origin"
        
        # Sync ai-context back
        Write-Status "`nSyncing ai-context with updated master..."
        git checkout ai-context
        git merge master --no-ff -m "Merge master back into ai-context after batch merge"
        git push origin ai-context
        Write-Success "ai-context synchronized with master"
    } else {
        Write-Warning-Custom "`nMerge complete locally. Push with:"
        Write-Host "  git push origin master" -ForegroundColor Yellow
        Write-Host "`nThen sync ai-context:" -ForegroundColor Yellow
        Write-Host "  git checkout ai-context" -ForegroundColor Yellow
        Write-Host "  git merge master" -ForegroundColor Yellow
        Write-Host "  git push origin ai-context" -ForegroundColor Yellow
    }
}

# Main execution
try {
    Write-Host "`nai-context Branch Synchronization Tool" -ForegroundColor Magenta
    Write-Host "======================================`n" -ForegroundColor Magenta
    
    # Verify git repository
    $gitRoot = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Custom "Not in a git repository"
        exit 1
    }
    
    # Verify clean working tree (unless dry run)
    if (-not $DryRun) {
        Assert-CleanWorkingTree
    }
    
    # Execute sync
    switch ($Direction) {
        'FromMaster' { Sync-FromMaster }
        'ToMaster' { Sync-ToMaster }
    }
    
    Write-Host "`n" # Blank line for readability
}
catch {
    Write-Error-Custom "Error: $_"
    exit 1
}
