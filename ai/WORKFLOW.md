# Skyline Git Workflow - Quick Reference

Essential workflows for LLM-assisted development. See [ai/docs/workflow-guide.md](docs/workflow-guide.md) for comprehensive details.

## Branch Strategy

- **master** - Stable releases, requires review
- **ai-context** - Rapid iteration on `ai/` documentation (merges to master periodically)
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
- `TODO-20251105_feature_name-auxiliary.txt` - Auxiliary files (logs, data, coverage reports)

**Auxiliary files** (non-markdown files associated with a TODO) must:
1. Use the TODO filename as a prefix (e.g., `TODO-20251105_feature-coverage.txt`)
2. Move with their TODO when transitioning between directories
3. Be deleted or archived with their TODO

### Lifecycle
1. **Backlog** - Planning on ai-context (`ai/todos/backlog/`)
2. **Active** - Development on feature branch (`ai/todos/active/`)
3. **Completed** - On feature branch, merged to master with code (`ai/todos/completed/`)
4. **Archive** - Cleanup after 3 months (`ai/todos/archive/`)

> **Note:** Backlog TODOs are committed to `ai-context` branch to avoid churning master. See [ai/docs/ai-context-branch-strategy.md](docs/ai-context-branch-strategy.md) for details.

### Header Standard (All TODO Files)

Each TODO (active or completed) MUST begin with the file name as a level-1 heading followed by a standardized Branch Information block:

```
# TODO-YYYYMMDD_feature_name.md

## Branch Information
- **Branch**: `Skyline/work/YYYYMMDD_feature_name`
- **Base**: `master` | `ai-context` | `Skyline/skyline_YY_N` (optional, defaults to master)
- **Created**: YYYY-MM-DD
- **Completed**: YYYY-MM-DD | (pending)
- **Status**: 🚧 In Progress | ✅ Completed
- **PR**: [#NNNN](https://github.com/ProteoWizard/pwiz/pull/NNNN) | (pending)
- **Objective**: Single concise sentence describing the end goal
```

**Base branch selection:**
- `master` (default) - Normal feature/fix work
- `ai-context` - Documentation-only work (ai/ files, MCP tools, skills)
- `Skyline/skyline_YY_N` - Release branch fixes

Rules:
- Before moving a TODO from `active/` to `completed/`, the **PR** field must be populated with the number (linked form preferred). If absent, automated tools / LLM must prompt the user: "Provide PR number before completion move.".
- Use backticks around the branch name.
- Keep the Objective to one line; expanded background goes in later sections.
- Do not retroactively edit completed TODOs except to apply this header standard at merge time.
- Status should reflect current state ("🚧 In Progress" until merged; then change to "✅ Completed").


## Key Workflows

### Workflow 1: Start Work from Backlog TODO

**Determine base branch:**
1. Check TODO file for `**Base**:` field (if present, use that)
2. Developer can override: "Let's work on TODO-feature.md and base it on ai-context"
3. Default to `master` if not specified

**Step 1: Create feature branch from appropriate base:**
```bash
# Use base from TODO file, developer override, or default to master
git checkout <base-branch>  # master, ai-context, or Skyline/skyline_YY_N
git pull origin <base-branch>
git checkout -b Skyline/work/20251105_feature_name
```

**Step 2: Move TODO on ai-context (claims the work):**
> **Note:** Backlog TODOs live on `ai-context` branch. Move there first, then cherry-pick.
```bash
git checkout ai-context
git pull origin ai-context
git mv ai/todos/backlog/TODO-feature_name.md ai/todos/active/TODO-20251105_feature_name.md
# Edit TODO: update Branch, Created, Status fields
git add ai/todos/active/TODO-20251105_feature_name.md
git commit -m "Start feature_name work - move TODO to active"
git push origin ai-context
```

**Step 3: Cherry-pick to feature branch:**
```bash
git checkout Skyline/work/20251105_feature_name
git cherry-pick <commit-hash-from-step-2>
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
2. Add PR reference to TODO (`**PR**: #1234` or `**PR**: [#1234](https://github.com/ProteoWizard/pwiz/pull/1234)`)
3. Mark all completed tasks as `[x]`
4. Commit TODO updates to branch

**PR URL format:** `https://github.com/ProteoWizard/pwiz/pull/{PR_NUMBER}`

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

### Workflow 3a: Bug Fix for Completed Work

When fixing bugs in recently completed features (still in `ai/todos/completed/`):

**Create bug-fix branch:**
```bash
git checkout master
git pull origin master
git checkout -b Skyline/work/YYYYMMDD_original-feature-name-fix
```

**Branch naming pattern:** Use original feature name + `-fix` suffix with today's date

**Update the original TODO (don't create new one):**
```bash
# Add "Bug Fixes" section at end of ai/todos/completed/TODO-YYYYMMDD_original.md
# Document: issue found, root cause, fix applied, testing notes
git add ai/todos/completed/TODO-YYYYMMDD_original.md
git commit -m "Document bug fix for original feature"
```

**After PR merge:**
- Original TODO stays in `completed/` with bug fix documented
- Delete bug-fix branch as usual
- Bug fix becomes part of original feature's history

**Use this workflow when:**
- Bug is discovered shortly after feature completion
- Fix is small and clearly related to original feature
- Original TODO is still in `completed/` (not yet archived)

### Workflow 4: Create Backlog TODO During Active Work

When inspiration strikes during development:

**Create on ai-context branch:**
```bash
git stash
git checkout ai-context
git pull origin ai-context
# Create ai/todos/backlog/TODO-new_idea.md
git add ai/todos/backlog/TODO-new_idea.md
git commit -m "Add backlog TODO for new_idea planning"
git push origin ai-context
git checkout Skyline/work/YYYYMMDD_current_feature
git stash pop
```

> **Why ai-context?** Backlog TODOs are committed to `ai-context` to avoid frequent master churn. See [ai/docs/ai-context-branch-strategy.md](docs/ai-context-branch-strategy.md).

## LLM Tool Guidelines

### GitHub CLI (gh) for PR Workflows

The GitHub CLI (`gh`) enables LLM agents to review PRs, fetch issues, and check CI status directly. If `gh` commands fail with "command not found" or similar:

> **Do not silently try another approach.** Instead, inform the user:
> "It looks like GitHub CLI isn't set up yet. Would you like help installing it, or should I try another approach?"

See [ai/docs/developer-setup-guide.md](docs/developer-setup-guide.md#github-cli-gh) for installation and authentication steps. Note that `gh auth login` requires an interactive terminal outside the IDE.

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

> ⚠️ **Always build before running tests.** Skyline executables load the last compiled binaries, so running tests without rebuilding will exercise stale code.

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

**Keep commit messages concise (≤10 lines)** - digestible in TortoiseGit's multi-line textbox view.

**Pattern:**
```
Brief summary of change (report mood - what was done)

Optional 2-3 line explanation if needed.
Details belong in TODO file, not commit message.

See ai/todos/active/TODO-YYYYMMDD_feature.md for complete details.

Co-Authored-By: Claude <noreply@anthropic.com>
```

**Rules:**
- **Maximum 10 lines total** including blank lines and attribution
- **Always include `Co-Authored-By` line** when an LLM agent contributed to the commit
- **Reference TODO file** for detailed context and decisions
- **Report mood** - "Added feature" not "Add feature" (like research paper methods section)

**Examples:**
```bash
# Good
git commit -m "Balanced documentation tone and restored TODO-20251105 goals

Moved BUILD-TEST.md to ai/docs/, created documentation-maintenance.md.
See TODO-20251105_improve_tool_support_for_ai_dev.md Phase 5 for details.

Co-Authored-By: Claude <noreply@anthropic.com>"

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
- **Commit messages ≤10 lines** - Single line for AI attribution, reference TODO for details

## See Also

- [ai/docs/workflow-guide.md](docs/workflow-guide.md) - Comprehensive workflow guide with all templates and examples
- [ai/CRITICAL-RULES.md](CRITICAL-RULES.md) - All critical constraints
- [ai/MEMORY.md](MEMORY.md) - Project context and patterns
- [ai/STYLEGUIDE.md](STYLEGUIDE.md) - Coding conventions
