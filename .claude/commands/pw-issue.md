---
description: Create GitHub Issue from TODO file or skyline.ms issue
---

# Create GitHub Issue

Migrate a TODO file or skyline.ms issue to GitHub Issues (the single backlog system).

**Goal**: Complete information transfer so the source file can be deleted. The GitHub Issue must be self-contained with ALL technical details preserved.

## Arguments

$ARGUMENTS = One of:
- TODO file path: `ai/todos/backlog/TODO-old_work.md`
- skyline.ms issue: `skyline:NNN`

## From TODO File

```
/pw-issue ai/todos/backlog/TODO-old_work.md
```

### Workflow

1. **Read the TODO file completely**:
   - If file exists on current branch: `Read ai/todos/backlog/TODO-old_work.md`
   - If not found: `git show backlog-archive-YYYYMMDD:ai/todos/backlog/TODO-old_work.md`

2. **Transfer ALL content** (the issue must be self-contained):
   - Objective/Summary
   - Scope items (as checklist)
   - **All technical details** - proposed solutions, code samples, root cause analysis
   - **All context** - affected files/screenshots, status notes, related work
   - Preserve code blocks with proper formatting

3. **Create GitHub Issue**:
   ```bash
   gh issue create \
     --title "<objective>" \
     --label "todo" \
     --body "## Summary
   <from TODO - include category, priority, origin>

   ## Scope
   <checklist from TODO>

   ## Technical Details
   <ALL technical content - code samples, proposed solutions, root cause analysis>
   <Use markdown headers to organize by task/topic>

   ## Getting Started
   Use /pw-startissue <number> to begin work.

   ---
   Migrated from: $ARGUMENTS"
   ```

4. **Report**: Show issue URL

5. **Verify completeness**: Ask user to confirm the issue contains all information needed to delete the TODO file

## From skyline.ms Issue

```
/pw-issue skyline:1234
```

### Workflow

1. **Fetch issue**: `mcp__labkey__get_issue_details(issue_id=1234)`

2. **Extract content**:
   - Title
   - Description
   - Priority, Area, Milestone (as labels if applicable)

3. **Create GitHub Issue**:
   ```bash
   gh issue create \
     --title "<title>" \
     --label "todo" \
     --body "## Summary
   <from skyline.ms>

   ## Getting Started
   Use /pw-startissue <number> to begin work.

   ---
   Transferred from: skyline.ms Issue #1234"
   ```

4. **Report**: Show issue URL

## Rules

- **Complete transfer**: The GitHub Issue must contain ALL information from the source. No summarizing or omitting technical details.
- **Self-contained**: After migration, the issue should be usable without referencing the original TODO file.
- **Deletable source**: The goal is that `ai/todos/backlog/` ceases to exist - all backlog items live in GitHub Issues.
- Add appropriate labels (ai-context, skyline, pwiz, etc.)
- The created issue becomes the backlog item - TODO files are only created when work actively starts via `/pw-startissue`
