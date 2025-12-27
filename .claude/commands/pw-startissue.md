---
description: Start work on a GitHub Issue
---

# Start Work on GitHub Issue

Begin work on a GitHub Issue, following the appropriate workflow based on issue labels.

## Arguments

$ARGUMENTS = GitHub Issue number (e.g., "3732")

## Workflow

### Step 1: Fetch Issue Details

```bash
gh issue view $ARGUMENTS
```

Review the issue scope and **check labels** to determine branching strategy.

### Step 2: Determine Branch Strategy

**Check for `ai-context` label:**

- **If `ai-context` label present** → Work directly on `ai-context` branch
  - These are AI tooling/documentation issues
  - No feature branch needed (single developer on ai-context currently)
  - Future: may use branches when multiple developers work on ai-context

- **If NO `ai-context` label** → Follow standard workflow (ai/WORKFLOW.md)
  - Create feature branch: `Skyline/work/YYYYMMDD_<description>`
  - Base branch is typically `master`
  - PR back to master when complete

### Step 3: Switch to Appropriate Branch

**For ai-context issues:**
```bash
git checkout ai-context
git pull origin ai-context
```

**For other issues:**
```bash
git checkout master
git pull origin master
git checkout -b Skyline/work/YYYYMMDD_<description>
```

### Step 4: Create TODO File

Create `ai/todos/active/TODO-YYYYMMDD_<issue_title_slug>.md`:

```markdown
# <Issue Title>

## Branch Information
- **Branch**: `ai-context` or `Skyline/work/YYYYMMDD_<description>`
- **Base**: `ai-context` or `master`
- **Created**: YYYY-MM-DD
- **GitHub Issue**: https://github.com/ProteoWizard/pwiz/issues/$ARGUMENTS

## Objective

<Copy from issue Summary section>

## Tasks

<Copy scope items as checkboxes>

## Progress Log

### YYYY-MM-DD - Session Start

Starting work on this issue...
```

### Step 5: Signal Ownership

**Git signal** - Commit and push TODO:
```bash
git add ai/todos/active/TODO-*.md
git commit -m "Start work on #$ARGUMENTS - <brief description>"
git push
```

**GitHub signal** - Comment on the issue:
```bash
gh issue comment $ARGUMENTS --body "Starting work.
- Branch: \`<branch-name>\`
- TODO: \`ai/todos/active/TODO-YYYYMMDD_<slug>.md\`"
```

### Step 6: Load Context

Based on issue labels, load appropriate skills:
- `ai-context` label → Load skyline-development skill
- `skyline` label → Load skyline-development skill
- `tutorial` label → Load tutorial-documentation skill

Check for related documentation referenced in issue.

### Step 7: Begin Work

With TODO created and ownership signaled, begin implementing the issue scope.

Reference the issue in commits: `See #$ARGUMENTS` or `Fixes #$ARGUMENTS`

## Completion

**For ai-context issues:**
1. Update TODO Progress Log
2. Move TODO to `ai/todos/completed/`
3. Commit and push to ai-context
4. Close issue: `gh issue close $ARGUMENTS --comment "Completed. See ai/todos/completed/TODO-*.md"`

**For other issues:**
1. Update TODO Progress Log
2. Move TODO to `ai/todos/completed/`
3. Create PR to master (follow ai/WORKFLOW.md)
4. Issue closed when PR merges (use `Fixes #$ARGUMENTS` in PR)

If work remains, create new GitHub Issues for remaining scope.

## Related

- ai/WORKFLOW.md - Standard branching and TODO workflow
- ai/docs/workflow-issues-guide.md - GitHub Issues integration details
