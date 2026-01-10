# AI Submodule Strategy

Strategy for managing the `ai/` folder as a Git submodule, replacing the ai-context branch workflow.

**Status**: Proposed (not yet implemented)

---

## Overview

The `ai/` folder lives in a separate repository (`ProteoWizard/pwiz-ai`) and is included in pwiz as a Git submodule. This provides:

- **Normal git workflow** - Standard pull/push, no rebase complexity
- **Single source of truth** - All pwiz clones share the same ai/ content
- **Clean separation** - Documentation PRs vs code PRs
- **No sync overhead** - No weekly syncs, no cherry-picks needed

### What's Where

| Component | Location | Notes |
|-----------|----------|-------|
| `ai/` folder | `pwiz-ai` repo (submodule) | All documentation, todos, MCP configs |
| `.claude/` folder | `pwiz` repo | Commands/skills (concise references to ai/) |
| Root `CLAUDE.md` | `pwiz` repo | Tiny pointer to ai/CLAUDE.md |

---

## Daily Workflow

### Updating ai/ Documentation

```bash
# Enter the submodule
cd ai

# Make sure you're on main and up to date
git checkout main
git pull origin main

# Make changes
# ... edit files ...

# Commit and push
git add .
git commit -m "Update documentation for X"
git push origin main

# Return to pwiz root
cd ..
```

### Updating Your Clone's ai/ Submodule

```bash
# From pwiz root - get latest ai/ content
git submodule update --remote ai
```

**In TortoiseGit:**
1. Right-click the `ai/` folder → Submodule Update
2. Check "Remote tracking branch" to get latest

### Pinning ai/ Version in pwiz (Optional)

If you want pwiz to track a specific ai/ commit:

```bash
git add ai
git commit -m "Update ai/ submodule to latest"
git push origin master
```

**Note**: We recommend NOT pinning - let each clone update ai/ independently since documentation applies broadly across Skyline versions.

---

## Working with Multiple Clones

One major benefit: all your pwiz clones can share the same ai/ content.

```
C:\proj\
  pwiz\ai\          → pwiz-ai repo
  scratch\ai\       → pwiz-ai repo (same!)
  review\ai\        → pwiz-ai repo (same!)
  skyline_26_1\ai\  → pwiz-ai repo (same!)
```

Update ai/ in any clone, and it's available everywhere (after `git pull` in each ai/ submodule).

---

## Working with Historical Branches

**For older release branches that predate the submodule** (e.g., skyline_25_1):

Don't retrofit them. Instead, work from a modern checkout:

```
C:\proj\scratch\          ← Work here (has full ai/ + .claude/ context)
  └── Claude Code can read/write files in:
      C:\proj\skyline_25_1\   ← Older branch without ai/
```

**Pattern:** "Work from context-rich, modify anywhere"

- Claude Code has full context from the modern checkout
- Can read/write files in any directory on the system
- The LLM understands modern conventions and applies them to older code

---

## New Machine Setup

When cloning pwiz fresh:

```bash
# Clone with submodules
git clone --recurse-submodules https://github.com/ProteoWizard/pwiz.git

# Or if already cloned without submodules:
git submodule update --init
```

---

## Scheduled Tasks

For machines running scheduled Claude Code tasks (e.g., /pw-daily):

```bash
# Update ai/ submodule before running
git submodule update --remote ai
```

This can be added to the scheduled task script.

---

## Troubleshooting

### "ai/ folder is empty"

Submodule not initialized:
```bash
git submodule update --init
```

### "Detached HEAD in ai/"

Normal for submodules. To make changes:
```bash
cd ai
git checkout main
# Now you can commit
```

### "Changes in ai/ not showing in pwiz status"

Submodule changes are tracked separately. To see ai/ changes:
```bash
cd ai
git status
```

### "Want to discard ai/ changes"

```bash
cd ai
git checkout .
git clean -fd
```

---

## Comparison: Before and After

### Before (ai-context branch)

```bash
# Update documentation
git checkout ai-context
git commit
git push --force-with-lease  # Force push required!

# Weekly sync
/pw-aicontextsync  # Complex rebase + squash

# Other machines after sync
git fetch origin ai-context
git reset --hard origin/ai-context  # Can't use pull!
```

### After (submodule)

```bash
# Update documentation
cd ai
git commit
git push  # Normal push!

# Other machines
cd ai
git pull  # Normal pull works!
```

---

## Transition Plan

### Phase 1: Create pwiz-ai Repository

- [ ] Create `ProteoWizard/pwiz-ai` repo on GitHub
- [ ] Copy ai/ content (flat structure - ai/ contents become repo root)
- [ ] Initial commit and push

### Phase 2: Convert pwiz to Use Submodule

- [ ] Final ai-context sync to master
- [ ] Remove ai/ from pwiz: `git rm -r ai/`
- [ ] Add submodule: `git submodule add https://github.com/ProteoWizard/pwiz-ai.git ai`
- [ ] Push to master

### Phase 3: Update Documentation

- [ ] Update new-machine-setup.md with `--recurse-submodules`
- [ ] Update CLAUDE.md to note submodule
- [ ] Archive ai-context-branch-strategy.md
- [ ] Update this document status to "Implemented"

### Phase 4: Retire ai-context Branch

- [ ] Delete remote branch: `git push origin --delete ai-context`
- [ ] Remove /pw-aicontextsync, /pw-aicontextupdate commands
- [ ] Clean up sync scripts

### Rollback Plan

If needed, revert to regular directory:

```bash
git submodule deinit ai
git rm ai
rm -rf .git/modules/ai
# Copy content back from pwiz-ai
cp -r /path/to/pwiz-ai/* ai/
git add ai/
git commit -m "Restore ai/ as regular directory"
```

---

## Design Decisions

### Why ai/ Only (Not .claude/)

- `ai/` has high change velocity (todos, docs, memory)
- `.claude/` has low change velocity (commands reference ai/docs/)
- Normal PR workflow works fine for rare .claude/ changes

### Why Not Pin Versions

- Documentation applies broadly across Skyline versions
- Each clone can update ai/ independently
- Reduces coordination overhead

### Why Fresh History (Not Extracted)

- Simpler migration
- Old history remains accessible in pwiz repo
- Clean start for new workflow

---

## Historical Note

Prior to this submodule approach, ai/ was managed via the ai-context branch with a rebase-based workflow. That approach required:
- Force-push after every change
- Weekly sync with squash and rebase
- "Never pull, always reset" rule for all machines

The submodule approach eliminates this complexity. See archived `ai-context-branch-strategy.md` for the previous workflow.

---

## References

- [Git Submodules Documentation](https://git-scm.com/book/en/v2/Git-Tools-Submodules)
- [documentation-maintenance.md](documentation-maintenance.md) - "Reference, don't embed" principle
