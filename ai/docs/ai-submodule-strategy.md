# AI Submodule Strategy

Strategy for managing all AI tooling as a Git submodule, replacing the ai-context branch workflow.

**Status**: Proposed (not yet implemented)

---

## Overview

All AI tooling lives in a separate repository (`ProteoWizard/pwiz-ai`) included in pwiz as a Git submodule at `ai/`. This provides:

- **Normal git workflow** - Standard pull/push, no rebase complexity
- **Single source of truth** - All pwiz clones share the same AI content
- **Unified location** - All AI tooling in one place
- **Clear pattern** - Future projects add `ai/scripts/NewProject/`

### What's Where

| Component | Location | Notes |
|-----------|----------|-------|
| `ai/` folder | `pwiz-ai` repo (submodule) | Everything: docs, scripts, commands, MCP |
| `.claude/` folder | Windows junction | Points to `ai/claude/` |

### Unified Structure

```
ai/                              <- Single submodule (pwiz-ai repo)
+-- claude/                      <- Commands/skills (was .claude/)
|   +-- commands/
|   +-- skills/
+-- scripts/                     <- All project build scripts
|   +-- Skyline/
|   |   +-- Build-Skyline.ps1
|   |   +-- Run-Tests.ps1
|   |   +-- helpers/
|   +-- AutoQC/
|   |   +-- Build-AutoQC.ps1
|   +-- SkylineBatch/
|       +-- Build-SkylineBatch.ps1
+-- docs/                        <- All documentation
+-- mcp/                         <- MCP server
+-- todos/                       <- Work tracking
+-- CLAUDE.md, MEMORY.md, etc.
```

**In pwiz repo (after conversion):**
```
.claude/  ->  Windows junction to ai/claude/
ai/       ->  Submodule mount point
```

---

## Benefits

| Benefit | Description |
|---------|-------------|
| **Single source** | "Where's AI stuff?" -> `ai/` |
| **Single sync** | One submodule update gets everything |
| **Clear pattern** | New project? Add `ai/scripts/NewProject/` |
| **Findability** | All build scripts in one tree |
| **Normal workflow** | Standard push/pull, no force-push or rebase |

---

## Daily Workflow

### Updating AI Content

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
1. Right-click the `ai/` folder -> Submodule Update
2. Check "Remote tracking branch" to get latest

---

## Windows Junction for .claude/

Claude Code requires `.claude/` at repo root. Solution: Windows junction (directory link) pointing to `ai/claude/`.

### Automatic Creation via Boost Build

The junction is created automatically during the first build. In `pwiz_tools/Skyline/Jamfile.jam`:

```jam
if ! --incremental in [ modules.peek : ARGV ]
{
   echo "Updating submodules for Hardklor etc..." ;
   SHELL "git submodule update --init --recursive" ;

   # Create .claude junction if it doesn't exist
   if ! [ path.exists $(PWIZ_ROOT_PATH)/.claude ]
   {
      echo "Creating .claude junction to ai/claude..." ;
      SHELL "mklink /J \"$(PWIZ_ROOT_PATH)/.claude\" \"$(PWIZ_ROOT_PATH)/ai/claude\"" ;
   }
}
```

**This integrates with the existing workflow:**
- Submodules already initialized by Boost Build (not clone)
- Junction created in same step
- No changes needed to clone instructions
- `new-machine-setup.md` workflow unchanged

### Junction Properties

- Works without admin rights
- Appears as normal folder to all tools
- Git sees it as a junction (not tracked)
- Created automatically by first build

---

## New Machine Setup

When cloning pwiz fresh, no special steps needed:

```bash
git clone git@github.com:ProteoWizard/pwiz.git
cd pwiz
bs.bat   # Boost Build initializes submodules AND creates junction
```

The build automatically:
1. Runs `git submodule update --init --recursive`
2. Creates `.claude` junction pointing to `ai/claude/`

---

## Working with Multiple Clones

All your pwiz clones share the same ai/ content:

```
C:\proj\
  pwiz\ai\          -> pwiz-ai repo
  scratch\ai\       -> pwiz-ai repo (same!)
  review\ai\        -> pwiz-ai repo (same!)
  skyline_26_1\ai\  -> pwiz-ai repo (same!)
```

Update ai/ in any clone, and it's available everywhere (after `git pull` in each ai/ submodule).

---

## Working with Historical Branches

**For older release branches that predate the submodule** (e.g., skyline_25_1):

Don't retrofit them. Instead, work from a modern checkout:

```
C:\proj\scratch\          <- Work here (has full ai/ + .claude/ context)
  +-- Claude Code can read/write files in:
      C:\proj\skyline_25_1\   <- Older branch without ai/
```

**Pattern:** "Work from context-rich, modify anywhere"

- Claude Code has full context from the modern checkout
- Can read/write files in any directory on the system
- The LLM understands modern conventions and applies them to older code

---

## Existing Submodules in pwiz

The project already uses 4 submodules - this is an **established pattern**:

| Submodule | Path |
|-----------|------|
| BullseyeSharp | `pwiz_tools/Skyline/Executables/BullseyeSharp` |
| DocumentConverter | `pwiz_tools/Skyline/Executables/DevTools/DocumentConverter` |
| Hardklor | `pwiz_tools/Skyline/Executables/Hardklor/Hardklor` |
| MSToolkit | `pwiz_tools/Skyline/Executables/Hardklor/MSToolkit` |

**Implications:**
- CI/build already handles submodules
- Team has submodule experience
- Adding `ai/` follows existing pattern
- Difference: `ai/` won't be pinned (docs apply broadly), others are pinned (reproducible builds)

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

1. Create `ProteoWizard/pwiz-ai` repo on GitHub
2. Copy content with new structure:
   ```
   ai/docs/          -> docs/
   ai/mcp/           -> mcp/
   ai/scripts/       -> scripts/        (existing utility scripts)
   ai/todos/         -> todos/
   ai/*.md           -> *.md            (root docs)
   .claude/          -> claude/         (commands/skills)
   pwiz_tools/Skyline/ai/              -> scripts/Skyline/
   pwiz_tools/.../AutoQC/ai/           -> scripts/AutoQC/
   pwiz_tools/.../SkylineBatch/ai/     -> scripts/SkylineBatch/
   ```
3. Initial commit and push

### Phase 2: Update Script Paths

Scripts must update their relative path calculations:
- `Build-Skyline.ps1`: Navigate from `ai/scripts/Skyline/` to `pwiz_tools/Skyline/`
- Similar updates for AutoQC, SkylineBatch
- Update all documentation references to new paths

### Phase 3: Convert pwiz to Use Submodule

1. Final ai-context sync to master
2. Remove old locations:
   ```bash
   git rm -r ai/
   git rm -r .claude/
   git rm -r pwiz_tools/Skyline/ai/
   git rm -r pwiz_tools/Skyline/Executables/AutoQC/ai/
   git rm -r pwiz_tools/Skyline/Executables/SkylineBatch/ai/
   ```
3. Add submodule:
   ```bash
   git submodule add https://github.com/ProteoWizard/pwiz-ai.git ai
   ```
4. Update `pwiz_tools/Skyline/Jamfile.jam` to create junction (see above)
5. Add `.claude` to `.gitignore` (junction shouldn't be tracked)
6. Push to master

### Phase 4: Update Documentation

- `new-machine-setup.md`: Minimal changes (workflow unchanged, build creates junction)
- `CLAUDE.md`: Update script path examples
- Archive `ai-context-branch-strategy.md`
- Update this document status to "Implemented"

### Phase 5: Retire ai-context Branch

- Delete remote branch: `git push origin --delete ai-context`
- Remove `/pw-aicontextsync`, `/pw-aicontextupdate` commands

---

## Critical Files to Update

| File | Change Needed |
|------|---------------|
| `pwiz_tools/Skyline/Jamfile.jam` | Add junction creation after submodule init |
| `Build-Skyline.ps1` | Update `$skylineRoot` path calculation |
| `Build-AutoQC.ps1` | Update `$autoQCRoot` path calculation |
| `Build-SkylineBatch.ps1` | Update root path calculation |
| `ai/CLAUDE.md` | Update script path examples |
| `ai/docs/build-and-test-guide.md` | Update script path references |
| `.gitignore` | Add `.claude` (junction) |

---

## Rollback Plan

If issues arise, revert to regular directory:

```bash
git submodule deinit ai
git rm ai
rm -rf .git/modules/ai
rmdir .claude  # Remove junction
# Restore from pwiz-ai repo content
git add ai/ .claude/
git commit -m "Restore ai/ as regular directory"
```

---

## Troubleshooting

### "ai/ folder is empty"

Submodule not initialized. Run the build (`bs.bat`) or manually:
```bash
git submodule update --init
```

### ".claude/ folder missing after clone"

Run the build (`bs.bat`) which creates the junction, or manually:
```cmd
mklink /J .claude ai\claude
```

### "Detached HEAD in ai/"

Normal for submodules. To make changes:
```bash
cd ai
git checkout main
# Now you can commit
```

### "Changes in ai/ not showing in pwiz status"

Submodule changes are tracked separately:
```bash
cd ai
git status
```

---

## Design Decisions

### Why Unified (All AI Content Together)

- **Single location**: "Where's AI stuff?" -> `ai/`
- **Single sync**: One update gets everything
- **Clear pattern**: Future projects add `ai/scripts/NewProject/`
- **Build scripts self-navigate**: Location is organizational, not functional

### Why Not Pin Versions

- Documentation applies broadly across Skyline versions
- Each clone can update ai/ independently
- Reduces coordination overhead

### Why Junction for .claude/

- Claude Code requires `.claude/` at repo root
- Junction is transparent to all tools
- No admin rights required
- Created automatically by Boost Build

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
- GitHub Issue #3786 - Original submodule proposal discussion
