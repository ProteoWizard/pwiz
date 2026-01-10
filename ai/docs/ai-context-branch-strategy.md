# AI Context Branch Strategy

## Quick Reference for LLM Agents

When a user asks to "sync ai-context" or "merge ai-context to master", use the automation script:

```powershell
# IMPORTANT: All .ps1 scripts require PowerShell 7+ (pwsh.exe)

# Sync FROM master (rebase ai-context onto master):
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -Push

# Sync TO master (squash commits and prepare PR):
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction ToMaster -Push
# Then create PR: gh pr create --base master --head ai-context --title "Weekly ai-context sync"
# Merge using "Rebase and merge" (NOT squash!)
```

---

## Problem Statement

Frequent commits to `ai/todos/` on master are causing friction for team members:
- PRs show "out-of-date with base branch" frequently
- Developers must wait for quiet periods to merge
- TeamCity builds trigger unnecessarily for documentation-only changes

## Key Insight

The `ai/` folder serves a different purpose than code:
- **Code**: Requires full CI validation, careful merge timing
- **ai/todos/**: LLM context restoration, rapid iteration, low risk

These have different change velocities and risk profiles. They shouldn't impose the same merge friction.

## Solution: Rebase-Based ai-context Branch

### Overview

1. `ai-context` branch holds rapid ai/ documentation iteration
2. Always kept as a **linear history on top of master** (via rebase, not merge)
3. Weekly sync squashes commits into one and merges to master
4. After merge, both branches share the same commit—no "sync loop" needed

### Branch Model

```
master:      M1---M2---M3---S  (S = squashed ai-context sync)
                           ↑
ai-context:               S  (same commit after merge!)
```

Key difference from merge-based workflow:
- **Rebase** keeps ai-context linear on top of master
- **Squash before PR** means PR shows 1 commit
- **"Rebase and merge"** on GitHub puts the same commit on master
- Both branches end up at the same commit—naturally in sync!

### Benefits

- PRs show exactly 1 commit (clean, easy to review)
- No "closing the loop" needed after merge
- No divergent history or add/add conflicts
- Clean linear history on ai-context

---

## CRITICAL: Never Use Regular Git Pull

**The ai-context branch uses rebase, which rewrites history.** After any sync (weekly or daily rebase), commit hashes change. A regular `git pull` will try to merge divergent histories and create a mess.

**On any computer with a local ai-context branch:**

```bash
# WRONG - Creates merge commits and pollutes history
git pull origin ai-context

# CORRECT - Reset to remote after syncs
git fetch origin ai-context
git reset --hard origin/ai-context
```

**In TortoiseGit:**
1. Fetch (not Pull)
2. Switch/Checkout → Select `ai-context` → Check **"Overwrite working tree changes (force)"** → OK

This is expected after every weekly sync or when another developer rebases. See Troubleshooting for scenarios with uncommitted changes.

---

## Daily Workflow

### Working on ai-context

```powershell
# Ensure you're on ai-context branch
git checkout ai-context

# Make changes to ai/ files
# Edit files...

# Commit changes
git add ai/
git commit -m "Add backlog TODO for [feature]"
git push --force-with-lease origin ai-context
```

**Note:** Use `--force-with-lease` because rebasing rewrites history.

### Sync FROM master (daily maintenance)

Keep ai-context rebased onto latest master:

```powershell
# Using automation script (recommended)
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -Push

# Or manually:
git checkout ai-context
git fetch origin master
git rebase origin/master
git push --force-with-lease origin ai-context
```

**When to run:**
- At start of work session
- After your code PR merges to master
- Before starting the weekly sync

---

## Weekly Sync: ai-context → master

### Step 1: Squash and Push

```powershell
# Preview what will be squashed
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction ToMaster -DryRun

# Squash commits and push
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction ToMaster -Push -Message "Weekly sync: description here"
```

This:
1. Rebases ai-context onto latest master (if needed)
2. Squashes all ai-context commits into 1
3. Force-pushes ai-context

### Step 2: Create and Merge PR

```powershell
# Create PR
gh pr create --base master --head ai-context --title "Weekly ai-context sync (Jan 4, 2026)"
```

**IMPORTANT: Use "Rebase and merge" on GitHub** (not "Squash and merge")

Since we already squashed, "Rebase and merge" puts the exact same commit on master. Both branches now share the same HEAD commit.

### Step 3: Sync Local (optional)

```powershell
# Pull the merged state
git checkout ai-context
git pull origin ai-context
```

---

## Automation Script Reference

Located at `ai/scripts/sync-ai-context.ps1`

> **Important:** Requires PowerShell 7+ (`pwsh.exe`).

### Commands

```powershell
# Preview rebase FROM master
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -DryRun

# Rebase FROM master and push
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -Push

# Preview squash TO master
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction ToMaster -DryRun

# Squash and push (prepares PR)
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction ToMaster -Push

# With custom commit message
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction ToMaster -Push -Message "Weekly sync: new features"
```

### Features

- **Dry run mode**: Preview changes before executing
- **Automatic rebase**: FromMaster rebases ai-context onto master
- **Automatic squash**: ToMaster squashes all commits into one
- **Force-push safety**: Uses `--force-with-lease` to prevent overwrites
- **Clear next steps**: Shows exact commands for PR creation

---

## Conflict Resolution

### Rebase Conflicts (FromMaster)

If conflicts occur during rebase:

```powershell
# Script will stop on conflict
# Resolve conflicts in affected files
git add <resolved-files>
git rebase --continue

# If needed, abort and retry
git rebase --abort

# After resolution, push
git push --force-with-lease origin ai-context
```

### Prevention

Conflicts are rare with this workflow because:
- ai-context is always linear on top of master
- Squashing before merge eliminates complex history

---

## Multi-Developer Workflow

When multiple developers work on ai-context, coordination is needed because rebasing rewrites history.

### Best Practices

1. **Communicate before weekly sync** - Let others know before running `/pw-aicontextsync`
2. **Push frequently** - Small, frequent pushes reduce conflict scope
3. **Fetch before committing** - Check if someone else pushed first
4. **Reset after others rebase** - Don't pull, reset (see CRITICAL section above)

### When Your Push Is Rejected

If someone else pushed while you were working:

```bash
# Fetch their changes
git fetch origin ai-context

# Rebase YOUR commits onto their changes
git rebase origin/ai-context

# Push with force-with-lease (safe force push)
git push --force-with-lease origin ai-context
```

This preserves both sets of commits in linear history

---

## Syncing TODO Changes from Feature Branches

When a feature branch updates a TODO file:

### Option 1: Cherry-pick to ai-context

```bash
# After committing TODO move on feature branch
git checkout ai-context
git fetch origin ai-context
git rebase origin/ai-context  # Ensure up to date
git cherry-pick <commit-hash>
git push --force-with-lease origin ai-context
git checkout <feature-branch>
```

### Option 2: Let it merge naturally

Since feature branches merge to master, and ai-context rebases onto master, the TODO changes will appear in ai-context after the next FromMaster sync.

---

## Policy Guidelines

### What goes on ai-context

- New TODO files in `ai/todos/backlog/`
- Updates to `ai/todos/active/` or `ai/todos/completed/`
- MEMORY.md, WORKFLOW.md, CRITICAL-RULES.md refinements
- MCP server updates in `ai/mcp/`
- Any file under `ai/` that doesn't affect build/test

### What goes through normal PRs

- Code changes (always)
- Changes to `ai/` that accompany code changes (same PR)
- CRITICAL-RULES.md updates that affect team workflow (needs review)

---

## Troubleshooting

### "Working tree is not clean"

Commit or stash changes before running sync:
```powershell
git stash push -u -m "WIP: ai/ changes"
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -Push
git stash pop
```

### "Rebase conflict detected"

Resolve conflicts manually:
```powershell
# Edit conflicting files
git add <resolved-files>
git rebase --continue
git push --force-with-lease origin ai-context
```

### "rejected - failed to push"

Someone else pushed to ai-context. Fetch and rebase:
```powershell
git fetch origin ai-context
git rebase origin/ai-context
git push --force-with-lease origin ai-context
```

### "Branch is ahead of origin" after someone else rebased

After another checkout runs `/pw-aicontextupdate` (or the sync script), your local history diverges from the force-pushed remote. Git shows you're "ahead by N commits" even though the content is identical.

**Why this happens:** Rebasing rewrites commit hashes. Your local branch has the old hashes; the remote has new ones. A regular `git pull` creates merge commits trying to reconcile them.

**Solution:** Reset to the remote:
```bash
git fetch origin ai-context
git reset --hard origin/ai-context
```

This discards your local commit graph and uses the remote's rebased history. No content is lost since the files are identical.

**If you have uncommitted changes**, stash first:
```bash
git stash push -m "WIP changes"
git fetch origin ai-context
git reset --hard origin/ai-context
git stash pop
```

### "I made local commits, then someone synced"

If you committed locally but didn't push, and then someone else rebased/synced ai-context:

```bash
# First, save your commit hashes (copy these somewhere)
git log --oneline -5

# Reset to the new remote history
git fetch origin ai-context
git reset --hard origin/ai-context

# Cherry-pick your commits back (use the hashes you saved)
git cherry-pick <your-commit-hash-1>
git cherry-pick <your-commit-hash-2>

# Push your work
git push --force-with-lease origin ai-context
```

**Alternative if commits are simple:** Just redo your changes manually after reset. Often faster than cherry-picking if changes were small.

---

## Historical Note

Prior to January 2026, this workflow used merge commits instead of rebase. This caused PRs to show hundreds of commits (the full history since branch creation) even though the actual diff was small. The rebase-based workflow was adopted to ensure PRs show only the actual new changes.

---

## Related

- **TeamCity**: Build triggers disabled for changes under `ai/` (separate configuration)
- **Issue tracker**: Remains the source of truth for project planning; ai/todos is LLM context
