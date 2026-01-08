# TODO Files Directory

This directory contains task planning and tracking files for the Skyline project.

## Directory Structure

```
todos/
  active/           # Currently being worked on (committed to branch)
  completed/        # Recently completed (keep 1-3 months for reference)
  backlog/          # Ready to start, fully planned
    <github-username>/  # Developer-specific TODOs (optional)
  archive/          # Old completed work (>3 months old or as needed)
```

### Developer-Specific Folders

The `backlog/` directory may contain subdirectories named after GitHub usernames (e.g., `brendanx67/`). These folders contain TODOs that are assigned to or being worked on by specific developers.

- **Root `backlog/` folder**: TODOs that can be claimed by any developer
- **Developer-specific folders** (e.g., `backlog/brendanx67/`): TODOs assigned to or being worked on by a specific developer

When creating a TODO you plan to work on yourself, place it in your developer-specific folder. When a TODO is ready to be claimed by anyone, place it in the root backlog folder.

## File Lifecycle

### 1. Planning (backlog/)
- Create `TODO-<branch_specifier>.md` without date prefix
- Commit to master for team visibility
- Contains: scope, tasks, risks, success criteria

### 2. Active Development (active/)
- When creating branch, move from `backlog/` to `active/`
- Rename to include date: `TODO-YYYYMMDD_<branch_specifier>.md`
- Update with every commit on the branch
- Tracks progress, decisions, context

### 3. Completion (completed/)
- Before merging PR, add completion summary to TODO
- Move from `active/` to `completed/` as final commit on branch
- Merge branch to master (TODO goes with it)
- Serves as documentation of what was done

### 4. Archive (archive/)
- After 1-3 months, move from `completed/` to `archive/` (or delete)
- Git history preserves everything forever
- Reduces clutter in active directories

## File Naming Convention

- **Backlog**: `TODO-<branch_specifier>.md` (no date)
- **Active**: `TODO-YYYYMMDD_<branch_specifier>.md` (with creation date)
- **Completed**: `TODO-YYYYMMDD_<branch_specifier>.md` (keeps date)
- **Branch specifier**: Lowercase, underscores (e.g., `webclient_replacement`, `utf8_no_bom`)

## Current Status

### Active
- `TODO-20251010_webclient_replacement.md` - Core Skyline.exe WebClient migration (PR #3636, pending merge)

### Backlog
- `TODO-skyp_webclient_replacement.md` - Last WebClient usage in core Skyline.exe
- `TODO-panorama_webclient_replacement.md` - PanoramaClient migration
- `TODO-tools_webclient_replacement.md` - Auxiliary tools migration
- `TODO-utf8_no_bom.md` - UTF-8 BOM standardization
- `TODO-remove_async_and_await.md` - Remove async/await keywords

### Completed
- (None yet - first TODO will move here after PR #3636 merges)

## Quick Reference

### Starting Work
```bash
# Create branch from master
git checkout -b Skyline/work/20251019_<specifier>

# Move and rename TODO
mv todos/backlog/TODO-<specifier>.md todos/active/TODO-20251019_<specifier>.md

# Update TODO header with actual branch info, commit
git add todos/
git commit -m "Start <specifier> work"
git push -u origin Skyline/work/20251019_<specifier>
```

### Completing Work
```bash
# Add completion summary to TODO
# (edit todos/active/TODO-YYYYMMDD_<specifier>.md)

# Move to completed
mv todos/active/TODO-YYYYMMDD_<specifier>.md todos/completed/

# Commit and push
git add todos/
git commit -m "Complete <specifier> work - ready for merge"
git push

# Create PR, merge to master
```

### Cleanup (Periodic)
```bash
# On master, after TODO is >3 months old
git mv todos/completed/TODO-20240715_old.md todos/archive/
# Or just delete (Git history preserves it)
git rm todos/completed/TODO-20240715_old.md

git commit -m "Archive old TODO files"
git push origin master
```

## LLM Tool Integration

This TODO system is designed for seamless context switching between LLM tools:

1. **Starting session**: LLM reads TODO from `active/` to understand current state
2. **During work**: LLM updates TODO with every commit (progress, decisions)
3. **Context switching**: "Handoff Prompt" in TODO enables smooth transitions
4. **Completion**: LLM adds completion summary before final merge

See `WORKFLOW.md` in project root for detailed LLM workflow guidelines.

## Benefits

- **Documentation**: Completed TODOs document what was done and why
- **Discoverability**: Future developers can see "how did we approach X?"
- **Context**: LLM tools can resume work seamlessly across sessions
- **Visibility**: Team knows what's planned, active, and completed
- **Git history**: Everything preserved forever, even if deleted from workspace
- **Clean workspace**: Stale items don't clutter active directories

