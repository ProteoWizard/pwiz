# GitHub Issues Workflow Guide

This document describes the experimental workflow for using GitHub Issues as the backlog system, with ai/todos for active work only.

## Overview

**GitHub Issues** = Backlog and tracking (replaces ai/todos/backlog)
**ai/todos/active/** = Detailed engineering context for work in progress
**ai/todos/completed/** = Historical record of completed work

This eliminates the ai/todos/backlog folder. Work items live in GitHub Issues until actively started.

## Labels

### Component Labels
| Label | Description | Color |
|-------|-------------|-------|
| `skyline` | Skyline application issues | (TBD) |
| `pwiz` | ProteoWizard/msconvert issues | (TBD) |
| `ai-context` | AI tooling and context branch | #1D76DB |

### Workflow Labels
| Label | Description | Color |
|-------|-------------|-------|
| `todo` | Tracked via ai/todos system | #0E8A16 |
| `bug` | Something isn't working | #d73a4a |
| `enhancement` | New feature or request | #a2eeef |

## Workflow

### Creating a Backlog Item

Create a GitHub Issue only (no TODO file yet):

```bash
gh issue create \
  --title "Brief description" \
  --label "ai-context,todo,enhancement" \
  --body "## Summary
Brief description...

## Scope
- [ ] Task 1
- [ ] Task 2

## Getting Started
When starting this work:
1. Create ai/todos/active/TODO-YYYYMMDD_feature_name.md
2. Link this issue in the TODO file"
```

### Starting Work on an Issue

When beginning work (new session):

1. **Create TODO file** in `ai/todos/active/`:
   ```
   ai/todos/active/TODO-YYYYMMDD_feature_name.md
   ```

2. **Link issue in TODO file**:
   ```markdown
   - **GitHub Issue**: https://github.com/ProteoWizard/pwiz/issues/NNNN
   ```

3. **Signal ownership immediately** (two places):

   **Git signal** - Push TODO to ai-context:
   ```bash
   git checkout ai-context
   git add ai/todos/active/TODO-YYYYMMDD_feature_name.md
   git commit -m "Start work on #NNNN - feature name"
   git push origin ai-context
   git checkout Skyline/work/YYYYMMDD_feature_name  # or stay on ai-context
   ```

   **GitHub signal** - Comment on the issue:
   ```
   Starting work.
   - Branch: `Skyline/work/YYYYMMDD_feature_name` (or `ai-context`)
   - TODO: `ai/todos/active/TODO-YYYYMMDD_feature_name.md`
   ```

4. **Work in appropriate branch** (feature branch or ai-context)

5. **Log progress** in TODO's Progress Log section

6. **Reference issue** in commits: `See #NNNN` or `Fixes #NNNN`

### Completing an Issue

1. **Move TODO** to `ai/todos/completed/`
2. **Close GitHub Issue** with reference to completed TODO:
   ```bash
   gh issue close NNNN --comment "Completed. See ai/todos/completed/TODO-YYYYMMDD_feature_name.md"
   ```

## Issue Templates

### Feature/Enhancement
```markdown
## Summary
Brief description of what this adds or improves.

## Motivation
Why is this needed? What problem does it solve?

## Scope
- Bullet points of what's included
- What's explicitly out of scope

## TODO File
See `ai/todos/active/TODO-YYYYMMDD_feature_name.md`
```

### Bug Report
```markdown
## Summary
Brief description of the bug.

## Steps to Reproduce
1. Step one
2. Step two
3. Expected vs actual behavior

## Environment
- Skyline version:
- OS:
- Test name (if applicable):

## TODO File
See `ai/todos/active/TODO-YYYYMMDD_fix_description.md`
```

## Querying Issues

### List open issues by label
```bash
gh issue list --label "ai-context"
gh issue list --label "todo"
gh issue list --label "ai-context,todo"
```

### View issue details
```bash
gh issue view 3732
```

### Search issues
```bash
gh issue list --search "scheduled daily"
```

## Current Active Issues

| Issue | Title | Labels |
|-------|-------|--------|
| #3732 | Scheduled Claude Code daily analysis | ai-context, todo, enhancement |
| #3733 | Gmail MCP integration for report delivery | ai-context, todo, enhancement |

## Design Decisions

### Why Both Systems?

**GitHub Issues (backlog + tracking):**
- Public visibility (external contributors)
- Integration with PRs and commits
- Labels and milestones
- Notifications and subscriptions
- Lightweight - just enough to describe and track work

**ai/todos/active (work in progress):**
- Detailed engineering context
- Git-tracked history of decisions
- Structured progress logs
- Session handoff documentation
- Created only when work actually starts

### The Lifecycle

```
GitHub Issue (backlog)
    ↓ work starts
ai/todos/active/TODO-*.md created, linked to issue
    ↓ work completes
ai/todos/completed/TODO-*.md (moved)
GitHub Issue closed with link to completed TODO
```

### No More ai/todos/backlog

Previously, backlog items lived in `ai/todos/backlog/`. This created duplication if also tracked in GitHub Issues. The new workflow:
- GitHub Issues = the only backlog
- TODO files created on-demand when work starts
- Eliminates sync problems between two backlog systems

## Future Enhancements

- `/pw-issue create` command to automate issue+TODO creation
- `/pw-issue sync` to update issue from TODO changes
- Issue templates in `.github/ISSUE_TEMPLATE/`
