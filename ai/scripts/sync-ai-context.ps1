<#
.SYNOPSIS
    Synchronize ai-context branch with master using rebase workflow.

.DESCRIPTION
    Manages the ai-context branch lifecycle with clean linear history:
    - FromMaster: Rebase ai-context onto master (daily maintenance)
    - ToMaster: Squash commits and prepare PR (weekly sync)

    This workflow ensures:
    - ai-context is always a linear history on top of master
    - PRs show a single squashed commit
    - After merge, both branches share the same commit (no "closing the loop" needed)

.PARAMETER Direction
    Sync direction:
    - 'FromMaster': Rebase ai-context onto master (daily maintenance)
    - 'ToMaster': Squash commits and prepare PR to master (weekly sync)

.PARAMETER DryRun
    Preview changes without executing rebase/push operations.

.PARAMETER Push
    Automatically push after rebase (default: prompt user).

.PARAMETER Message
    Custom commit message for the squashed commit (ToMaster only).
    If not provided, will prompt or use a default.

.EXAMPLE
    .\ai\scripts\sync-ai-context.ps1 -Direction FromMaster -Push
    Rebase ai-context onto latest master and push.

.EXAMPLE
    .\ai\scripts\sync-ai-context.ps1 -Direction ToMaster -DryRun
    Preview what would be squashed for the PR.

.EXAMPLE
    .\ai\scripts\sync-ai-context.ps1 -Direction ToMaster -Push -Message "Weekly sync: release docs"
    Squash commits with custom message and push.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('FromMaster', 'ToMaster')]
    [string]$Direction,

    [Parameter(Mandatory=$false)]
    [switch]$DryRun,

    [Parameter(Mandatory=$false)]
    [switch]$Push,

    [Parameter(Mandatory=$false)]
    [string]$Message
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

function Get-MergeBase {
    $mergeBase = git merge-base origin/master HEAD 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to find merge base with master"
    }
    return $mergeBase
}

function Get-CommitsSinceMergeBase {
    $mergeBase = Get-MergeBase
    $commits = git log --oneline "$mergeBase..HEAD" 2>$null
    if ($LASTEXITCODE -ne 0) {
        return @()
    }
    if ($commits) {
        return @($commits)
    }
    return @()
}

function Show-RebasePreview {
    param([string]$Direction)

    if ($Direction -eq 'FromMaster') {
        Write-Status "`nRebase Preview: ai-context onto master"
        Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray

        # Show commits on master that we'll rebase onto
        $behindCount = git rev-list --count HEAD..origin/master 2>$null
        if ($behindCount -eq 0) {
            Write-Success "ai-context is already up to date with master"
            return $false
        }

        Write-Host "`nmaster is $behindCount commit(s) ahead. Commits to rebase onto:" -ForegroundColor Yellow
        git log --oneline HEAD..origin/master | ForEach-Object { Write-Host "  $_" }

        # Show our commits that will be replayed
        $aheadCount = git rev-list --count origin/master..HEAD 2>$null
        if ($aheadCount -gt 0) {
            Write-Host "`nai-context commits to replay ($aheadCount):" -ForegroundColor Yellow
            git log --oneline origin/master..HEAD | ForEach-Object { Write-Host "  $_" }
        }

        return $true
    }
    else {
        Write-Status "`nSquash Preview: ai-context → master"
        Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray

        $commits = Get-CommitsSinceMergeBase
        if ($commits.Count -eq 0) {
            Write-Success "No commits to squash - ai-context matches master"
            return $false
        }

        Write-Host "`nCommits to squash into 1 ($($commits.Count)):" -ForegroundColor Yellow
        $commits | ForEach-Object { Write-Host "  $_" }

        Write-Host "`nFiles changed:" -ForegroundColor Yellow
        $mergeBase = Get-MergeBase
        git diff --name-status $mergeBase HEAD | ForEach-Object { Write-Host "  $_" }

        return $true
    }
}

function Sync-FromMaster {
    Write-Status "`n═══════════════════════════════════════════════════════"
    Write-Status "  Rebase ai-context onto master (daily maintenance)"
    Write-Status "═══════════════════════════════════════════════════════`n"

    # Fetch latest
    Write-Status "Fetching latest from origin..."
    git fetch origin master ai-context
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Ensure on ai-context branch
    $currentBranch = Get-CurrentBranch
    if ($currentBranch -ne 'ai-context') {
        Write-Status "Switching to ai-context branch..."
        git checkout ai-context
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    # Pull latest ai-context (fast-forward only since we use rebase)
    git pull --ff-only origin ai-context 2>$null
    # Ignore errors - might be ahead of origin

    # Preview
    $hasChanges = Show-RebasePreview 'FromMaster'
    if (-not $hasChanges) {
        return
    }

    if ($DryRun) {
        Write-Warning-Custom "`n[DRY RUN] Would rebase ai-context onto master (no changes made)"
        return
    }

    # Rebase onto master
    Write-Status "`nRebasing ai-context onto master..."
    git rebase origin/master
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Custom "Rebase conflict detected. Resolve conflicts, then run:"
        Write-Host "  git rebase --continue" -ForegroundColor Yellow
        Write-Host "  git push --force-with-lease origin ai-context" -ForegroundColor Yellow
        exit 1
    }

    Write-Success "Rebased ai-context onto master"

    # Push (force required after rebase)
    if ($Push) {
        Write-Status "`nPushing ai-context (force-with-lease)..."
        git push --force-with-lease origin ai-context
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Write-Success "Pushed ai-context to origin"
    } else {
        Write-Warning-Custom "`nRebase complete locally. Push with:"
        Write-Host "  git push --force-with-lease origin ai-context" -ForegroundColor Yellow
    }
}

function Sync-ToMaster {
    Write-Status "`n═══════════════════════════════════════════════════════"
    Write-Status "  Squash and prepare PR: ai-context → master"
    Write-Status "═══════════════════════════════════════════════════════`n"

    # Fetch latest
    Write-Status "Fetching latest from origin..."
    git fetch origin master ai-context
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Ensure on ai-context branch
    $currentBranch = Get-CurrentBranch
    if ($currentBranch -ne 'ai-context') {
        Write-Status "Switching to ai-context branch..."
        git checkout ai-context
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    # First, ensure we're rebased onto latest master
    $behindCount = git rev-list --count HEAD..origin/master 2>$null
    if ($behindCount -gt 0) {
        Write-Status "ai-context is behind master by $behindCount commits. Rebasing first..."
        if (-not $DryRun) {
            git rebase origin/master
            if ($LASTEXITCODE -ne 0) {
                Write-Error-Custom "Rebase conflict. Resolve conflicts first, then re-run this script."
                exit 1
            }
        }
    }

    # Preview
    $hasChanges = Show-RebasePreview 'ToMaster'
    if (-not $hasChanges) {
        return
    }

    $commits = Get-CommitsSinceMergeBase

    if ($DryRun) {
        Write-Warning-Custom "`n[DRY RUN] Would squash $($commits.Count) commits into 1 (no changes made)"
        Write-Host "`nTo execute, run:" -ForegroundColor Yellow
        Write-Host "  pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction ToMaster -Push" -ForegroundColor Yellow
        return
    }

    # Squash commits
    if ($commits.Count -gt 1) {
        $mergeBase = Get-MergeBase

        # Determine commit message
        $commitMessage = $Message
        if (-not $commitMessage) {
            $dateStr = (Get-Date).ToString('MMM d, yyyy')
            $commitMessage = "Weekly ai-context sync ($dateStr)"
        }

        Write-Status "`nSquashing $($commits.Count) commits into 1..."
        git reset --soft $mergeBase
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        git commit -m $commitMessage
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Write-Success "Squashed into: $commitMessage"
    } else {
        Write-Status "Only 1 commit - no squash needed"
    }

    # Push
    if ($Push) {
        Write-Status "`nPushing ai-context (force-with-lease)..."
        git push --force-with-lease origin ai-context
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Write-Success "Pushed ai-context to origin"

        Write-Host "`n" -NoNewline
        Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
        Write-Host "  Next steps:" -ForegroundColor Green
        Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
        Write-Host ""
        Write-Host "  1. Create PR on GitHub: ai-context → master" -ForegroundColor Yellow
        Write-Host "     gh pr create --base master --head ai-context --title `"$commitMessage`"" -ForegroundColor White
        Write-Host ""
        Write-Host "  2. Merge using 'Rebase and merge' (NOT squash)" -ForegroundColor Yellow
        Write-Host "     This puts the same commit on master, keeping branches in sync." -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "  3. After merge, pull to sync local branches:" -ForegroundColor Yellow
        Write-Host "     git checkout ai-context && git pull origin ai-context" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Warning-Custom "`nSquash complete locally. Push with:"
        Write-Host "  git push --force-with-lease origin ai-context" -ForegroundColor Yellow
    }
}

# Main execution
try {
    Write-Host "`nai-context Branch Synchronization Tool (Rebase Workflow)" -ForegroundColor Magenta
    Write-Host "=========================================================`n" -ForegroundColor Magenta

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
