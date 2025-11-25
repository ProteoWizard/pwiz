# AI Context Branch Strategy

## Problem Statement

Frequent commits to `ai/todos/` on master are causing friction for team members:
- PRs show "out-of-date with base branch" frequently
- Developers must wait for quiet periods to merge
- TeamCity builds trigger unnecessarily for documentation-only changes
- Current workaround (override merge rules) works but feels ad-hoc

## Key Insight

The `ai/` folder serves a different purpose than code:
- **Code**: Requires full CI validation, careful merge timing
- **ai/todos/**: LLM context restoration, rapid iteration, low risk

These have different change velocities and risk profiles. They shouldn't impose the same merge friction.

## Recommended Solution: Dedicated ai-context Branch

### Overview

1. Create a long-lived `ai-context` branch for rapid ai/ iteration
2. Commit ai/ changes there without blocking team PRs
3. Batch-merge to master periodically (daily or when stabilized)
4. Feature branches continue branching from master (stable)

### Benefits

- Rapid iteration on ai/ documentation without blocking colleagues
- Git version control retained (history, diffs, blame)
- No submodule complexity
- Clear separation of concerns
- Feature branches see stable ai/ context from master

### Branch Workflow

```
master (stable)
  │
  ├── feature/my-feature (branches from master)
  │
  └── ai-context (long-lived, rapid ai/ changes)
        │
        └── periodically merged back to master
```

## Implementation Steps

### 1. Create the ai-context branch

```bash
git checkout master
git pull origin master
git checkout -b ai-context
git push -u origin ai-context
```

### 2. Daily Workflow: Working on ai-context

**Adding/modifying ai/ files:**
```powershell
# Ensure you're on ai-context branch
git checkout ai-context
git pull origin ai-context

# Make changes to ai/ files (MEMORY.md, backlog TODOs, etc.)
# Edit files...

# Commit changes
git add ai/
git commit -m "Add backlog TODO for [feature] and update MEMORY.md"
git push origin ai-context
```

### 3. Daily Maintenance: Sync ai-context FROM master

**Keep ai-context up-to-date with master code changes (run daily or before starting work):**

```powershell
# Using automation script (recommended)
.\ai\scripts\sync-ai-context.ps1 -Direction FromMaster -Push

# Or manually:
git checkout ai-context
git pull origin ai-context
git fetch origin master
git merge origin/master --no-ff -m "Merge master into ai-context: sync with latest code changes"
git push origin ai-context
```

**What this does:**
- Pulls latest code changes from master into ai-context
- Keeps ai-context's view of codebase current
- Prevents ai-context from diverging too far from master

**When to run:**
- At start of work session
- After major master merges (e.g., your PR was just merged)
- If you notice ai-context is behind master

### 4. Batch Update: Sync ai-context TO master

**Merge accumulated ai/ documentation back to master (weekly or when stabilized):**

```powershell
# Preview changes first (dry run)
.\ai\scripts\sync-ai-context.ps1 -Direction ToMaster -DryRun

# Option A: Create PR (recommended for visibility)
# 1. Push ai-context if needed
git push origin ai-context
# 2. Create PR: ai-context → master on GitHub
# 3. Merge via GitHub after review/approval

# Option B: Direct merge (faster, less ceremony)
.\ai\scripts\sync-ai-context.ps1 -Direction ToMaster -Push

# Or manually:
git checkout master
git pull origin master
git merge origin/ai-context --no-ff -m "Merge ai-context: batch update ai/ documentation"
git push origin master

# Sync ai-context back to master
git checkout ai-context
git merge master
git push origin ai-context
```

**When to merge to master:**
- Weekly batch (e.g., Friday afternoon)
- Before starting a feature that needs latest ai/ context
- When backlog TODOs are ready for team visibility
- After major documentation updates

**Do NOT merge to master:**
- For every tiny commit (defeats the purpose)
- During active PR merging periods (let team members finish first)
- If ai-context has experimental/incomplete documentation

## Policy Guidelines

### What goes on ai-context (bypass full CI)
- New TODO files in `ai/todos/backlog/`
- Updates to `ai/todos/work/` session notes
- MEMORY.md, WORKFLOW.md refinements
- Any file under `ai/` that doesn't affect build/test

### What still goes through normal PRs
- Code changes (always)
- Changes to `ai/` that accompany code changes (same PR)
- CRITICAL-RULES.md updates that affect team workflow (needs review)

### Merge frequency to master
- Daily batch merge if changes accumulated
- Immediate merge if a feature branch needs updated context
- No merge needed if ai-context is just holding backlog items

## Automation: sync-ai-context.ps1 Script

Located at `ai/scripts/sync-ai-context.ps1`, this script automates bidirectional synchronization.

### Command Reference

```powershell
# Preview what would be merged FROM master into ai-context
.\ai\scripts\sync-ai-context.ps1 -Direction FromMaster -DryRun

# Sync FROM master (daily maintenance)
.\ai\scripts\sync-ai-context.ps1 -Direction FromMaster -Push

# Preview what would be merged TO master from ai-context
.\ai\scripts\sync-ai-context.ps1 -Direction ToMaster -DryRun

# Sync TO master (batch update)
.\ai\scripts\sync-ai-context.ps1 -Direction ToMaster -Push
```

### Features

- **Dry run mode**: Preview changes before executing
- **Safety checks**: Verifies clean working tree, fetches latest
- **Conflict detection**: Guides you through resolution if needed
- **Bidirectional sync**: Handles both FROM master and TO master
- **Automatic push**: Optional `-Push` flag for unattended operation

### Typical Usage

**Morning routine (sync FROM master):**
```powershell
.\ai\scripts\sync-ai-context.ps1 -Direction FromMaster -Push
```

**Weekly batch merge (sync TO master via PR):**
```powershell
# 1. Preview changes
.\ai\scripts\sync-ai-context.ps1 -Direction ToMaster -DryRun

# 2. Push ai-context
git push origin ai-context

# 3. Create PR on GitHub: ai-context → master
# 4. Merge via GitHub after approval

# 5. Sync ai-context back
.\ai\scripts\sync-ai-context.ps1 -Direction FromMaster -Push
```

## Team Communication

Suggested message to team:

> To reduce master churn from ai/ documentation updates, we're adopting a dedicated `ai-context` branch for rapid iteration. 
>
> - ai/-only changes will accumulate on `ai-context`
> - Batch merges to master will happen weekly (or when stabilized)
> - Your feature branches won't see constant "out-of-date" warnings
> - The issue tracker remains our project management tool; ai/todos is LLM context
>
> **If you need latest ai/ context on your feature branch:**
> - Option 1: Wait for next batch merge (usually weekly)
> - Option 2: Cherry-pick from ai-context: `git cherry-pick <commit-hash>`
> - Option 3: Merge ai-context directly: `git merge origin/ai-context` (creates merge commit)
>
> **Automation:** Use `ai/scripts/sync-ai-context.ps1` to manage branch synchronization.

## Conflict Resolution

If merge conflicts occur during synchronization:

### Conflicts when syncing FROM master (code conflicts)

Rare, but can happen if both master and ai-context modified the same ai/ file:

```powershell
# Script will stop on conflict
# Resolve conflicts manually in affected files
git add <resolved-files>
git merge --continue
git push origin ai-context
```

### Conflicts when syncing TO master (documentation conflicts)

Very rare (ai/ files rarely touched on master), but resolve similarly:

```powershell
# Resolve conflicts (prefer ai-context version for ai/ files)
git add <resolved-files>
git merge --continue
git push origin master

# Sync ai-context back
git checkout ai-context
git merge master
git push origin ai-context
```

## Troubleshooting

### "Working tree is not clean"

Commit or stash changes before running sync:
```powershell
git stash push -u -m "WIP: ai/ changes"
.\ai\scripts\sync-ai-context.ps1 -Direction FromMaster -Push
git stash pop
```

### "No new commits to merge"

Branches are already in sync. No action needed.

### "Merge conflict detected"

Follow conflict resolution guidance above. The script will guide you.

### "Not in a git repository"

Run script from repository root or any subdirectory within the pwiz repository.

## Future Enhancements

Potential automation improvements:
- GitHub Actions for scheduled ai-context → master PRs (weekly)
- Branch protection rules lighter for ai-context → master merges
- Personal ai-context branches if team adopts ai/ workflows broadly
- Slack/Teams notification when ai-context merges to master

## Related

- TeamCity: Also disabling build triggers for changes under `ai/` (separate configuration change)
- Issue tracker: Remains the source of truth for project planning; ai/todos is implementation context for LLM sessions
