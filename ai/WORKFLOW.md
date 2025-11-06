# Skyline Git Workflow - Quick Reference

Essential workflows for LLM-assisted development. See [ai/docs/workflow-guide.md](docs/workflow-guide.md) for comprehensive details.

## Branch Strategy

- **master** - Stable releases, requires review
- **Skyline/skyline_YY_N** - Release branches
- **Skyline/work/YYYYMMDD_description** - Feature/fix branches (all development)

## TODO File System

### Directory Structure
```
ai/todos/
  active/      # Currently being worked on (on branch)
  backlog/     # Ready to start, fully planned
  completed/   # Recently completed (1-3 months)
  archive/     # Old work (>3 months)
```

### File Naming
- `TODO-feature_name.md` - Backlog (no date, in ai/todos/backlog/)
- `TODO-20251105_feature_name.md` - Active (dated, in ai/todos/active/)

### Lifecycle
1. **Backlog** - Planning on master (`ai/todos/backlog/`)
2. **Active** - Development on branch (`ai/todos/active/`)
3. **Completed** - Merged to master (`ai/todos/completed/`)
4. **Archive** - Cleanup after 3 months (`ai/todos/archive/`)

## Key Workflows

### Workflow 1: Start Work from Backlog TODO

**On master - claim the work:**
```bash
git checkout master
git pull origin master
git mv ai/todos/backlog/TODO-feature_name.md ai/todos/active/TODO-20251105_feature_name.md
git commit -m "Start feature_name work - move TODO to active"
git push origin master
```

**Create branch:**
```bash
git checkout -b Skyline/work/20251105_feature_name
```

**Update TODO header and commit:**
```bash
# Edit TODO: add Branch, Created, PR fields
git add ai/todos/active/TODO-20251105_feature_name.md
git commit -m "Update TODO with branch information"
git push -u origin Skyline/work/20251105_feature_name
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
2. Add PR reference to TODO (`**PR**: #1234`)
3. Mark all completed tasks as `[x]`
4. Commit TODO updates to branch

**After PR merge:**
```bash
# On branch
git mv ai/todos/active/TODO-YYYYMMDD_feature.md ai/todos/completed/
git commit -m "Move TODO to completed - PR #1234 merged"
git push
```

**On master after merge:**
```bash
git checkout master
git pull origin master
git branch -d Skyline/work/YYYYMMDD_feature  # Delete local branch
```

### Workflow 4: Create Backlog TODO During Active Work

When inspiration strikes during development:

**Option 1: Create on master (recommended):**
```bash
git stash
git checkout master
git pull origin master
# Create ai/todos/backlog/TODO-new_idea.md
git add ai/todos/backlog/TODO-new_idea.md
git commit -m "Add backlog TODO for new_idea planning"
git push origin master
git checkout Skyline/work/YYYYMMDD_current_feature
git stash pop
```

**Option 2: Cherry-pick from branch:**
```bash
# Create TODO on current branch
git add ai/todos/backlog/TODO-new_idea.md
git commit -m "Add backlog TODO for new_idea planning"
# Cherry-pick to master
git checkout master
git pull origin master
git cherry-pick <commit-hash>
git push origin master
git checkout Skyline/work/YYYYMMDD_current_feature
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

**For LLM-assisted IDEs that can execute PowerShell:**

```powershell
cd pwiz_tools\Skyline

# Build entire solution (default)
.\ai\Build-Skyline.ps1

# Pre-commit validation (recommended before committing)
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection

# Build and run all unit tests
.\ai\Build-Skyline.ps1 -RunTests

# Build specific project
.\ai\Build-Skyline.ps1 -Target Test
```

See [docs/build-and-test-guide.md](docs/build-and-test-guide.md) for complete command reference.

**Developers can continue using Visual Studio directly:**
- Build: Ctrl+Shift+B
- Run tests: Test Explorer
- Code inspection: ReSharper menu

### Context Switching
When switching LLM tools/sessions:
1. Update TODO with exact progress
2. Note key decisions and modified files
3. Provide handoff prompt in TODO

## Commit Messages

**Keep commit messages concise (<10 lines)** - digestible in TortoiseGit's multi-line textbox view.

**Pattern:**
```
Brief summary of change (imperative mood)

Optional 2-3 line explanation if needed.
Details belong in TODO file, not commit message.
```

**Examples:**
```bash
# Good
git commit -m "Balance documentation tone and restore TODO-20251105 goals

Moved BUILD-TEST.md to ai/docs/, created documentation-maintenance.md.
See TODO-20251105_improve_tool_support_for_ai_dev.md Phase 5 for details."

# Bad - too verbose
git commit -m "This commit addresses documentation violations...
[40 lines of detailed explanation]"
```

**Rationale:** TODO files tell the whole story. Commit messages just summarize.

## Critical Rules

See [ai/CRITICAL-RULES.md](CRITICAL-RULES.md) for full list. Key workflow rules:

- **Use git mv** - Always use `git mv` for moving TODO files (preserves history)
- **Update TODO every commit** - Track progress, decisions, files modified
- **Never modify completed TODOs** - They document merged PRs (historical record)
- **All TODOs must have PR reference** - Before moving to completed/
- **Commit messages <10 lines** - Details go in TODO files

## See Also

- [ai/docs/workflow-guide.md](docs/workflow-guide.md) - Comprehensive workflow guide with all templates and examples
- [ai/CRITICAL-RULES.md](CRITICAL-RULES.md) - All critical constraints
- [ai/MEMORY.md](MEMORY.md) - Project context and patterns
- [ai/STYLEGUIDE.md](STYLEGUIDE.md) - Coding conventions
