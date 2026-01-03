# Skyline Git Workflow - Quick Reference

Essential workflows for LLM-assisted development. See [ai/docs/workflow-guide.md](docs/workflow-guide.md) for comprehensive details.

## Branch Strategy

- **master** - Stable releases, requires review
- **ai-context** - Rapid iteration on `ai/` documentation (merges to master periodically)
- **Skyline/skyline_YY_N** - Release branches
- **Skyline/work/YYYYMMDD_description** - Feature/fix branches (all development)

## Backlog and TODO System

### Where Work Lives

| Stage | Location | Description |
|-------|----------|-------------|
| **Backlog** | GitHub Issues | All planned work, labeled and tracked |
| **Active** | `ai/todos/active/` | Engineering context for work in progress |
| **Completed** | `ai/todos/completed/` | Historical record (1-3 months) |
| **Archived** | `ai/todos/archive/` | Old work (>3 months) |

### Key Labels

| Label | Branch Strategy | Description |
|-------|-----------------|-------------|
| `ai-context` | Work on ai-context branch | AI tooling, documentation, MCP |
| `skyline` | Create Skyline/work branch from master | Application changes |
| `pwiz` | Create Skyline/work branch from master | ProteoWizard/msconvert |
| `todo` | N/A | Tracked via ai/todos system |

### TODO File Naming
- `TODO-20251227_feature_name.md` - Active (dated, in ai/todos/active/)
- `TODO-20251227_feature_name-auxiliary.txt` - Auxiliary files (logs, data, coverage)

**Auxiliary files** must use the TODO filename as a prefix and move with their TODO.

### Header Standard (All TODO Files)

```markdown
# TODO-YYYYMMDD_feature_name.md

## Branch Information
- **Branch**: `Skyline/work/YYYYMMDD_feature_name` | `ai-context`
- **Base**: `master` | `ai-context` | `Skyline/skyline_YY_N`
- **Created**: YYYY-MM-DD
- **Status**: In Progress | Completed
- **GitHub Issue**: [#NNNN](https://github.com/ProteoWizard/pwiz/issues/NNNN)
- **PR**: [#NNNN](https://github.com/ProteoWizard/pwiz/pull/NNNN) | (pending)
```

## Key Workflows

### Workflow 1: Start Work from GitHub Issue (/pw-startissue)

Use `/pw-startissue <number>` for zero-prompt startup. The command checks labels to determine branch strategy.

**If `ai-context` label present** - Work on ai-context branch:
```bash
git checkout ai-context
git pull origin ai-context
# Create TODO and work directly on ai-context
```

**If NO `ai-context` label** - Create feature branch from master:
```bash
git checkout master
git pull origin master
git checkout -b Skyline/work/YYYYMMDD_feature_name
```

**Both cases require ownership signaling:**

1. **Git signal** - Push TODO to ai-context:
```bash
git checkout ai-context
git add ai/todos/active/TODO-YYYYMMDD_feature_name.md
git commit -m "Start work on #NNNN - feature name"
git push origin ai-context
```

2. **GitHub signal** - Comment on the issue:
```
Starting work.
- Branch: `Skyline/work/YYYYMMDD_feature_name` (or `ai-context`)
- TODO: `ai/todos/active/TODO-YYYYMMDD_feature_name.md`
```

### Workflow 2: Daily Development

```bash
# Make changes
# Update ai/todos/active/TODO-*.md (mark tasks complete, add context)
git add .
git commit -m "Descriptive message"
git push
```

**Update TODO with every commit** - track completed tasks, decisions, files changed.

### Workflow 3: Complete Work and Merge

**Before PR approval:**
1. Add completion summary to TODO
2. Add PR reference: `**PR**: [#1234](https://github.com/ProteoWizard/pwiz/pull/1234)`
3. Mark all completed tasks as `[x]`
4. Update Status to `Completed`
5. Move TODO to completed:
```bash
git mv ai/todos/active/TODO-YYYYMMDD_feature.md ai/todos/completed/
git commit -m "Move TODO to completed - ready for merge"
git push
```

**Before merging to master (CRITICAL):**
> Sync TODO state to ai-context. See [ai-context-branch-strategy.md](docs/ai-context-branch-strategy.md).

```bash
git checkout ai-context
git pull origin ai-context
git cherry-pick <commit-hash-of-TODO-move>
git push origin ai-context
git checkout Skyline/work/YYYYMMDD_feature
```

**After PR merge:**
```bash
git checkout master
git pull origin master
git branch -d Skyline/work/YYYYMMDD_feature  # Delete local branch
```

**Close the GitHub Issue:**
```bash
gh issue close NNNN --comment "Completed. See ai/todos/completed/TODO-YYYYMMDD_feature.md"
```

### Workflow 3a: Bug Fix for Completed Work

When fixing bugs in recently completed features (still in `ai/todos/completed/`):

```bash
git checkout master
git pull origin master
git checkout -b Skyline/work/YYYYMMDD_original-feature-name-fix
```

**Update the original TODO** (don't create new one) - add "Bug Fixes" section.

### Workflow 4: Create GitHub Issue for Future Work

When inspiration strikes during development:

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
Use /pw-startissue <number> to begin work."
```

## LLM Tool Guidelines

### Starting a Session
1. Read `ai/todos/active/TODO-YYYYMMDD_feature.md` - understand current state
2. Review recent commits - see what's been done
3. Check `ai/MEMORY.md` and `ai/CRITICAL-RULES.md` - essential constraints
4. Confirm remaining tasks

### During Development
1. Update TODO with every commit - track progress
2. Follow `ai/STYLEGUIDE.md` and `ai/CRITICAL-RULES.md`
3. Use DRY principles - avoid duplication
4. Handle exceptions per established patterns

### Build and Test Automation (Optional)

> Always build before running tests.

```powershell
cd pwiz_tools\Skyline

# Build entire solution (default)
.\ai\Build-Skyline.ps1

# Pre-commit validation
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection

# Build and run all unit tests
.\ai\Build-Skyline.ps1 -RunTests
```

See [docs/build-and-test-guide.md](docs/build-and-test-guide.md) for complete reference.

### Context Switching
When switching LLM tools/sessions:
1. Update TODO with exact progress
2. Note key decisions and modified files
3. Provide handoff prompt in TODO

## Commit Messages

**Keep commit messages concise (10 lines max)**

**Required Format:**
```
<Title in past tense>

* bullet point 1
* bullet point 2

See ai/todos/active/TODO-YYYYMMDD_feature.md

Co-Authored-By: Claude <noreply@anthropic.com>
```

**Rules:**
- Past tense title - "Added feature" not "Add feature"
- 1-5 bullet points, each starting with `* `
- TODO reference - always include `See ai/todos/active/TODO-...`
- Co-Authored-By - always include when LLM contributed
- No emojis or markdown links

See [ai/docs/version-control-guide.md](docs/version-control-guide.md) for complete details.

## Critical Rules

See [ai/CRITICAL-RULES.md](CRITICAL-RULES.md) for full list. Key workflow rules:

- **Use git mv** - Always use `git mv` for moving TODO files (preserves history)
- **Update TODO every commit** - Track progress, decisions, files modified
- **Never modify completed TODOs** - They document merged PRs (historical record)
- **All TODOs must have PR reference** - Before moving to completed/
- **Signal ownership** - Push TODO to ai-context + comment on GitHub Issue
- **Commit messages 10 lines max** - Reference TODO for details

## See Also

- [ai/docs/workflow-guide.md](docs/workflow-guide.md) - Comprehensive workflow guide
- [ai/CRITICAL-RULES.md](CRITICAL-RULES.md) - All critical constraints
- [ai/MEMORY.md](MEMORY.md) - Project context and patterns
- [ai/STYLEGUIDE.md](STYLEGUIDE.md) - Coding conventions
