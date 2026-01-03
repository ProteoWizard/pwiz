---
description: Create GitHub Issue from TODO file or skyline.ms issue
---

# Create GitHub Issue

Create a GitHub Issue from a TODO file or skyline.ms issue.

## Arguments

$ARGUMENTS = One of:
- TODO file path: `ai/todos/backlog/TODO-old_work.md`
- skyline.ms issue: `skyline:NNN`

## From TODO File

```
/pw-issue ai/todos/backlog/TODO-old_work.md
```

### Workflow

1. **Read the TODO file**:
   - If file exists on current branch: `Read ai/todos/backlog/TODO-old_work.md`
   - If not found: `git show backlog-archive-YYYYMMDD:ai/todos/backlog/TODO-old_work.md`

2. **Extract content**:
   - Objective/Summary
   - Scope items (as checklist)
   - Any relevant technical notes

3. **Create GitHub Issue**:
   ```bash
   gh issue create \
     --title "<objective>" \
     --label "todo" \
     --body "## Summary
   <from TODO>

   ## Scope
   <checklist from TODO>

   ## Getting Started
   Use /pw-startissue <number> to begin work.

   ---
   Recovered from: $ARGUMENTS"
   ```

4. **Report**: Show issue URL

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

- Only create issues for work you're ready to start soon
- Add appropriate labels (ai-context, skyline, pwiz, etc.)
- The created issue becomes the backlog item - no TODO file until work starts
