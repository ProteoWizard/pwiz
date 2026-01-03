# Skyline Git Workflow & Branch Strategy

This document outlines the Git branch strategy and workflow for the Skyline project, including guidelines for LLM tools working on branches.

## Branch Strategy

### Master Branch
- **Purpose**: Stable releases for Skyline-daily builds
- **Protection**: Direct pushes require review
- **Merges**: Only from release branches or completed work branches
- **Naming**: `master`

### Release Branches
- **Purpose**: Release preparation and stabilization
- **Naming**: `Skyline/skyline_YY_N` (e.g., `Skyline/skyline_25_1`)
- **Lifecycle**: Created from master, merged back to master after release
- **Usage**: Bug fixes, release preparation, final testing

### Work Branches
- **Purpose**: Feature development, refactoring, bug fixes
- **Naming**: `Skyline/work/YYYYMMDD_description` (e.g., `Skyline/work/20251010_webclient_replacement`)
- **Lifecycle**: Created from master, merged to master when complete
- **Usage**: All development work, including LLM-assisted changes

### AI-Context Branch
- **Purpose**: Rapid iteration on AI tooling and documentation
- **Naming**: `ai-context`
- **Lifecycle**: Long-lived branch, merges to master periodically
- **Usage**: ai/ folder changes, MCP tools, skills, commands

## Backlog and TODO System

### Overview

**GitHub Issues** serves as the single backlog system. TODO files are created only when work actively starts, providing detailed engineering context during development.

```
GitHub Issue (backlog)
    |
    | /pw-startissue <number>
    v
ai/todos/active/TODO-*.md created, linked to issue
    |
    | work completes, PR merges
    v
ai/todos/completed/TODO-*.md (moved)
GitHub Issue closed with link to completed TODO
```

### Where Work Lives

| Stage | Location | Description |
|-------|----------|-------------|
| **Backlog** | GitHub Issues | All planned work items |
| **Active** | `ai/todos/active/` | Work in progress (committed to branch) |
| **Completed** | `ai/todos/completed/` | Recently completed (keep 1-3 months) |
| **Archived** | `ai/todos/archive/` | Old completed work (>3 months) |

### GitHub Issue Labels

#### Component Labels
| Label | Description | Branch Strategy |
|-------|-------------|-----------------|
| `skyline` | Skyline application issues | Create Skyline/work branch from master |
| `pwiz` | ProteoWizard/msconvert issues | Create Skyline/work branch from master |
| `ai-context` | AI tooling and context | Work directly on ai-context branch |

#### Workflow Labels
| Label | Description |
|-------|-------------|
| `todo` | Tracked via ai/todos system |
| `bug` | Something isn't working |
| `enhancement` | New feature or request |

### TODO File Naming Convention

**Format**: `TODO-YYYYMMDD_<branch_specifier>.md`

- **Branch specifier**: Lowercase words separated by underscores (matches branch name after date)
- **Date prefix**: Active branch creation date

**Examples**:
- `ai/todos/active/TODO-20251227_filestree_deadlock.md` - Active branch created 2025-12-27
- `ai/todos/completed/TODO-20251010_webclient_replacement.md` - Recently merged work

**Auxiliary files** (non-markdown files associated with a TODO) must:
1. Use the TODO filename as a prefix (e.g., `TODO-20251227_feature-coverage.txt`)
2. Move with their TODO when transitioning between directories
3. Be deleted or archived with their TODO

### TODO File Structure (Active)

For files **in ai/todos/active/** (work in progress):

```markdown
# TODO-YYYYMMDD_<branch_specifier>.md

## Branch Information
- **Branch**: `Skyline/work/YYYYMMDD_<branch_specifier>` | `ai-context`
- **Base**: `master` | `ai-context` | `Skyline/skyline_YY_N`
- **Created**: YYYY-MM-DD
- **Status**: In Progress | Completed
- **GitHub Issue**: [#NNNN](https://github.com/ProteoWizard/pwiz/issues/NNNN)
- **PR**: [#NNNN](https://github.com/ProteoWizard/pwiz/pull/NNNN) | (pending)

## Objective

Single concise sentence describing the end goal.

## Task Checklist
### Completed
- [x] Task 1
- [x] Task 2

### In Progress
- [ ] Task 3

### Remaining
- [ ] Task 4
- [ ] Task 5

## Key Files

- `path/to/file1.cs` - Description
- `path/to/file2.cs` - Description

## Progress Log

### YYYY-MM-DD - Session N
- What was done
- Decisions made
- Next steps

## Context for Next Session
Current state, key decisions, important files
```

### TODO File Structure (Completed)

Add this **final section** before moving to ai/todos/completed/:

```markdown
## Completion Summary

**Branch**: `Skyline/work/YYYYMMDD_<branch_specifier>`
**PR**: [#XXXX](https://github.com/ProteoWizard/pwiz/pull/XXXX)
**Merged**: YYYY-MM-DD

**What Was Actually Done**:
- Brief summary of actual changes

**Follow-up Work Created**:
- Links to new issues spawned from this work

**Key Files Modified**:
- List of primary files changed
```

## LLM Tool Guidelines

### Starting a New Session
1. **Read the TODO file** - Understand current branch context and objectives
2. **Review recent commits** - Understand what has been done
3. **Check project conventions** - Read `ai/MEMORY.md` for essential context
4. **Understand the scope** - Confirm what remains to be done

### During Development
1. **Update TODO with every commit** - Track progress and decisions
2. **Follow coding standards** - Use `ai/STYLEGUIDE.md` for style guidance
3. **Write appropriate tests** - Ensure code quality and regression prevention
4. **Use DRY principles** - Avoid duplication in long-lived codebase
5. **Handle exceptions properly** - Use established patterns for error handling

### Build, Test, and Inspection Workflow

**IMPORTANT**: AI agents (Claude Code, Cursor, GitHub Copilot, ChatGPT, etc.) should **NOT** attempt to build or run tests for this large project. Instead, follow this workflow:

#### After Making Code Changes
1. **Ask the developer to inspect the changes** in Visual Studio 2022
2. **Ask the developer to build the solution** (Ctrl+Shift+B or F6)
3. **Ask the developer to run relevant tests** in Test Explorer
4. **For larger changes: Ask the developer to run ReSharper inspection**

#### What AI Agents Should Do
- **DO**: Generate code, write tests, update files, suggest changes
- **DO**: Explain what needs to be tested and why
- **DO**: Update TODO file with what was changed
- **DO**: Point to specific files/lines for developer review
- **DON'T**: Run msbuild, invoke test runners, attempt full builds
- **DON'T**: Commit before developer has reviewed and tested

### Context Switching
When switching between LLM tools or sessions:
1. **Document current state** - Update TODO file with exact progress
2. **Note key decisions** - Record architectural choices and rationale
3. **List modified files** - Help next session understand scope
4. **Provide handoff prompt** - Include context for seamless transition

## Branch Lifecycle Workflows

**IMPORTANT**: All TODO files in `ai/todos/` are Git-tracked. Always use Git commands (`git mv`, `git add`, `git commit`) when creating, moving, or modifying TODO files.

### Workflow 1: Starting Work from GitHub Issue (/pw-startissue)

Use `/pw-startissue <number>` for zero-prompt startup. The command reads the issue labels and determines the appropriate branch strategy.

#### Step 1: Fetch Issue and Check Labels

```bash
gh issue view <number> --json labels,title,body
```

#### Step 2: Determine Branch Strategy

**If `ai-context` label present** - Work directly on ai-context:
```bash
git checkout ai-context
git pull origin ai-context
```

**If NO `ai-context` label** - Create feature branch from master:
```bash
git checkout master
git pull origin master
git submodule update --init --recursive
git checkout -b Skyline/work/YYYYMMDD_feature_name
```

#### Step 3: Create TODO File

Create `ai/todos/active/TODO-YYYYMMDD_feature_name.md` with:
- Branch Information populated
- GitHub Issue linked
- Scope from issue transferred to task checklist
- Progress Log section started

#### Step 4: Signal Ownership (CRITICAL)

**Git signal** - Push TODO to ai-context (even if working on feature branch):
```bash
git checkout ai-context
git pull origin ai-context
git add ai/todos/active/TODO-YYYYMMDD_feature_name.md
git commit -m "Start work on #NNNN - feature name"
git push origin ai-context
```

If working on feature branch, cherry-pick:
```bash
git checkout Skyline/work/YYYYMMDD_feature_name
git cherry-pick <commit-hash>
git push -u origin Skyline/work/YYYYMMDD_feature_name
```

**GitHub signal** - Comment on the issue:
```bash
gh issue comment NNNN --body "Starting work.
- Branch: \`Skyline/work/YYYYMMDD_feature_name\` (or \`ai-context\`)
- TODO: \`ai/todos/active/TODO-YYYYMMDD_feature_name.md\`"
```

### Workflow 2: Daily Development

```bash
# Make code changes
# Update ai/todos/active/TODO-YYYYMMDD_description.md (mark tasks complete, update context)
git add .
git commit -m "Descriptive message of changes"
git push
```

**IMPORTANT**: Update the TODO file with **every commit** to track:
- Which tasks were completed
- Key decisions made
- Files modified
- Any blockers or issues encountered

### Workflow 3: Early Pull Request for TeamCity Validation

For multi-phase branches, create a PR after the first testable unit of work:

**When to create early PR:**
- Branch has multiple phases or takes >1 week
- First phase is complete and testable
- Want TeamCity validation before proceeding

**Early PR Message Template:**
```markdown
## Summary
[1-2 sentences: what is being done and why]

**Phase N (this commit):** [What this phase accomplishes]
**Next:** [What Phase N+1 will do]

## Status
- Phase N complete - all tests passing
- Awaiting TeamCity validation
- Phase N+1 in progress

## Testing
[Brief bullet points of test coverage]

See `ai/todos/active/TODO-YYYYMMDD_description.md` for complete details.
```

### Workflow 4: Completing Work and Merging

#### Step 1: Final Updates to TODO
```bash
# Add completion summary to TODO file
# Document what was actually done, bugs found, follow-up work
git add ai/todos/active/TODO-YYYYMMDD_description.md
git commit -m "Add completion summary to TODO"
git push
```

#### Step 2: Create Pull Request
- Document all changes and testing in PR description
- Reference the GitHub Issue: `Fixes #NNNN` or `Closes #NNNN`
- Link to the active TODO file for full context

#### Step 3: Add PR Reference to TODO

```markdown
- **PR**: [#1234](https://github.com/ProteoWizard/pwiz/pull/1234)
```

#### Step 4: Prepare for Merge (after approval)

**Update TODO checkboxes:**
- Mark all completed tasks as `[x]`
- Remove or mark incomplete any tasks not done

**Move TODO to completed:**
```bash
git mv ai/todos/active/TODO-YYYYMMDD_description.md ai/todos/completed/
git commit -m "Move TODO to completed - PR #1234 merged"
git push
```

#### Step 5: Sync to ai-context (CRITICAL before merging)

```bash
git checkout ai-context
git pull origin ai-context
git cherry-pick <commit-hash-of-TODO-move>
git push origin ai-context
git checkout Skyline/work/YYYYMMDD_feature
```

#### Step 6: After Merge to Master

```bash
git checkout master
git pull origin master
git branch -d Skyline/work/YYYYMMDD_description  # Delete local branch
```

#### Step 7: Close GitHub Issue

```bash
gh issue close NNNN --comment "Completed. See ai/todos/completed/TODO-YYYYMMDD_feature.md and PR #1234."
```

### Workflow 5: Bug Fix for Completed Work

When fixing bugs in recently completed features (still in `ai/todos/completed/`):

**Create bug-fix branch:**
```bash
git checkout master
git pull origin master
git checkout -b Skyline/work/YYYYMMDD_original-feature-name-fix
```

**Branch naming pattern:** Use original feature name + `-fix` suffix with today's date

**Update the original TODO** (don't create new one):
```markdown
## Bug Fixes

### YYYY-MM-DD - Fix Description
- **Issue**: What was wrong
- **Root Cause**: Why it happened
- **Fix**: What was changed
- **PR**: #NNNN
```

**Use this workflow when:**
- Bug is discovered shortly after feature completion
- Fix is small and clearly related to original feature
- Original TODO is still in `completed/` (not yet archived)

### Workflow 6: Create GitHub Issue for Future Work

When inspiration strikes during development:

```bash
gh issue create \
  --title "Brief description" \
  --label "ai-context,todo,enhancement" \
  --body "## Summary
Brief description of the work.

## Scope
- [ ] Task 1
- [ ] Task 2

## Getting Started
Use /pw-startissue <number> to begin work."
```

### Workflow 7: Branching from a Feature Branch (Pre-Merge Dependency)

When you want to start new work that depends on changes in a feature branch not yet merged to master:

**Step 1: Create new branch from the feature branch**
```bash
git checkout Skyline/work/20251122_parent_feature
git pull origin Skyline/work/20251122_parent_feature
git checkout -b Skyline/work/20251221_new_feature
```

**Step 2: Update TODO header with temporary base notation**
```markdown
- **Base**: `Skyline/work/20251122_parent_feature` (will rebase to master after parent merges)
```

**Step 3: After parent branch merges, rebase onto master**
```bash
git fetch origin master
git rebase origin/master
git push --force-with-lease
```

**Caution:**
- Only use when confident the parent will merge before your PR
- Document the temporary base clearly in the TODO

## Best Practices

### TODO File Management
- **Active TODOs**: Update with every commit, keep context fresh
- **Completed TODOs**: Must have PR reference, retain 1-3 months
- **Archive**: Move or delete old completed work, Git history preserves everything

**CRITICAL - Completed TODOs are Historical Records**:
- NEVER modify TODO files in `todos/completed/` (they document merged PRs)
- All completed TODOs MUST have a PR reference before moving to completed/
- If doing follow-up work, create a new TODO that references the completed one

### Git Submodule Management
```bash
# One-time configuration (recommended)
git config submodule.recurse true

# Or manual after every pull/checkout
git submodule update --init --recursive
```

### Commit Messages

**Keep messages concise (10 lines max)**

```
<Title in past tense>

* bullet point 1
* bullet point 2

See ai/todos/active/TODO-YYYYMMDD_feature.md

Co-Authored-By: Claude <noreply@anthropic.com>
```

**Rules:**
- Past tense title - "Added feature" not "Add feature"
- 1-5 bullet points
- Reference TODO file
- Include Co-Authored-By when LLM contributed
- No emojis or markdown links

### Testing
- All new code must have appropriate tests
- Run existing tests to ensure no regressions
- Use translation-proof assertions
- Follow DRY principles in test code

### Code Quality
- Follow established coding standards
- Use consistent naming conventions
- Avoid code duplication (DRY principle)
- Handle exceptions appropriately
- Maintain backward compatibility

## Emergency Procedures

### Hotfixes
For critical production issues:
1. Create branch from master: `Skyline/hotfix/YYYYMMDD_description`
2. Make minimal fix
3. Test thoroughly
4. Merge directly to master
5. Cherry-pick to release branches if needed

### Rollbacks
If issues are discovered after merge:
1. Create rollback branch: `Skyline/rollback/YYYYMMDD_description`
2. Revert problematic commits
3. Test rollback thoroughly
4. Merge to master
5. Document lessons learned

## Querying Issues

```bash
# List open issues by label
gh issue list --label "ai-context"
gh issue list --label "todo"
gh issue list --label "ai-context,todo"

# View issue details
gh issue view 3732

# Search issues
gh issue list --search "scheduled daily"
```

## Integration with CI/CD

### Automated Testing
- All branches run automated tests on push
- Master branch runs full test suite
- Work branches run relevant test subsets
- Failed tests block merge to master

### Build Verification
- Automated builds verify compilation
- Performance regression detection
- Memory leak detection
- Cross-platform compatibility testing

This workflow ensures consistent development practices, enables effective collaboration between human developers and AI tools, and maintains code quality in the long-lived Skyline project.
