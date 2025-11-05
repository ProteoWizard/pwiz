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

## TODO File System

### Overview
The TODO file system enables seamless context management for LLM-assisted development, from initial planning through branch completion and archival.

### TODO Directory Structure

```
<root>/ai/
  todos/
    active/           # Currently being worked on (committed to branch)
    completed/        # Recently completed (keep 1-3 months for reference)
    backlog/          # Ready to start, fully planned
    archive/          # Old completed work (>3 months old or as needed)
```

### TODO File Naming Convention

**Format**: `TODO-[YYYYMMDD_]<branch_specifier>.md`

- **Branch specifier**: Lowercase words separated by underscores (matches branch name after date)
- **No date prefix**: Branch-ready, planning phase (lives in `ai/todos/backlog/`)
- **With date prefix**: Active branch, dated with branch creation date (lives in `ai/todos/active/`, committed to branch)

**Examples**:
- `ai/todos/backlog/TODO-utf8_no_bom.md` - Branch-ready, not yet created
- `ai/todos/active/TODO-20251015_utf8_no_bom.md` - Active branch created 2025-10-15
- `ai/todos/completed/TODO-20251010_webclient_replacement.md` - Recently merged work

### TODO File Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│ Phase 1: Planning (ai/todos/backlog/)                       │
├─────────────────────────────────────────────────────────────┤
│ File: TODO-<branch_specifier>.md                            │
│ Status: Branch-ready, contains scope and task breakdown     │
│ Location: ai/todos/backlog/, committed to master            │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼ Create branch
┌─────────────────────────────────────────────────────────────┐
│ Phase 2: Active Development (ai/todos/active/)              │
├─────────────────────────────────────────────────────────────┤
│ File: TODO-YYYYMMDD_<branch_specifier>.md                   │
│ Status: Renamed with date, moved to active/, on branch      │
│ Updates: Modified with every commit                         │
│ Purpose: Track progress, enable LLM context switching       │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼ Work complete, PR merged
┌─────────────────────────────────────────────────────────────┐
│ Phase 3: Completed (ai/todos/completed/)                    │
├─────────────────────────────────────────────────────────────┤
│ File: Moved to ai/todos/completed/ as final commit on branch│
│ Status: Includes completion summary, merged to master       │
│ Purpose: Documentation, reference for future work           │
│ Retention: Keep 1-3 months for reference                    │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼ After 1-3 months
┌─────────────────────────────────────────────────────────────┐
│ Phase 4: Archived (ai/todos/archive/)                       │
├─────────────────────────────────────────────────────────────┤
│ File: Moved to ai/todos/archive/ or deleted (in Git history)│
│ Status: Historical reference only                           │
│ Purpose: Reduce clutter, preserved in Git forever           │
└─────────────────────────────────────────────────────────────┘
```

### TODO File Structure (Backlog)

For files **in ai/todos/backlog/** (planning phase):

```markdown
# TODO-<branch_specifier>.md

## Branch Information (Future)
- **Branch**: Not yet created - will be `Skyline/work/YYYYMMDD_<branch_specifier>`
- **Objective**: Brief description

## Background
Context and rationale for the work

## Task Checklist
### Phase 1: [Name]
- [ ] Task 1
- [ ] Task 2

### Phase 2: [Name]
- [ ] Task 3
- [ ] Task 4

## Tools & Scripts
References to existing tools/scripts

## Risks & Considerations
Potential issues and mitigation

## Success Criteria
How to know work is complete

## Handoff Prompt for Branch Creation
Template for LLM to create branch and begin work
```

### TODO File Structure (Active)

For files **in ai/todos/active/** (active branch):

```markdown
# TODO-YYYYMMDD_<branch_specifier>.md

## Branch Information
- **Branch**: Skyline/work/YYYYMMDD_<branch_specifier>
- **Created**: YYYY-MM-DD
- **Objective**: Brief description

## Task Checklist
### ✅ Completed
- [x] Task 1
- [x] Task 2

### 🔄 In Progress
- [ ] Task 3

### 📋 Remaining
- [ ] Task 4
- [ ] Task 5

## Context for Next Session
Current state, key decisions, important files

## Handoff Prompt for New LLM Session
Template for seamless context transfer

## Notes for Future Sessions
Patterns established, key decisions made
```

### TODO File Structure (Completed)

For files **in ai/todos/completed/** (after merge):

Add this **final section** before moving to ai/todos/completed/:

```markdown
## ✅ Completion Summary

**Branch**: `Skyline/work/YYYYMMDD_<branch_specifier>`  
**PR**: #XXXX  
**Merged**: YYYY-MM-DD  
**TeamCity Results**: [Summary of test results]  

**What Was Actually Done**:
- (Brief summary of actual changes, may differ from plan)

**Unexpected Findings**:
- (Bugs found, scope changes, lessons learned)

**Follow-up Work Created**:
- (Links to new TODO files spawned from this work)

**Key Files Modified**:
- (List of primary files changed)
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
4. **For larger changes: Ask the developer to run ReSharper inspection** (ReSharper > Inspect > Code Issues in Solution)

#### Why This Matters
- **Large project**: Full Skyline build can take 5-10 minutes even on fast machines
- **Complex dependencies**: C++ libraries, vendor SDKs, .NET Framework, test data files
- **Visual Studio integration**: Better error messages, IntelliSense, debugger
- **Resource intensive**: Builds/tests consume significant CPU/memory/disk
- **Agent limitations**: Command-line builds have limited context and diagnostics
- **ReSharper analysis**: Project-wide static analysis catches naming violations, code smells, and style issues

#### What AI Agents Should Do
✅ **DO**: Generate code, write tests, update files, suggest changes
✅ **DO**: Explain what needs to be tested and why
✅ **DO**: Update TODO file with what was changed
✅ **DO**: Point to specific files/lines for developer review
✅ **DO**: Wait for developer confirmation before proposing commits for larger changes

❌ **DON'T**: Run msbuild, invoke test runners, attempt full builds
❌ **DON'T**: Wait for long-running build processes
❌ **DON'T**: Parse incomplete build output (truncated after 100 lines)
❌ **DON'T**: Commit or propose commits before developer has reviewed, tested, and run ReSharper inspection

#### Example Developer Handoff
After making changes, tell the developer:

```
I've made the following changes:
1. Added NetworkRequestException to HttpClientWithProgress.cs (lines 468-498)
2. Updated MapHttpException to throw NetworkRequestException (line 419)
3. Refactored SkypSupport.GetErrorStatusCode() to use exception properties (lines 207-226)

Please review before committing:
1. Review the changes in Visual Studio
2. Build the Skyline solution (Ctrl+Shift+B)
3. Fix any compilation errors that appear
4. Run TestFunctional.SkypTest in Test Explorer
5. Run ReSharper > Inspect > Code Issues in Solution
6. Address any new warnings in the modified files
7. Let me know the results so I can help fix any issues

The changes should eliminate message parsing and use structured exception properties instead.
Once everything passes, I can help prepare the commit message.
```

This approach leverages the developer's IDE tools while keeping the AI focused on code generation and design.

### Context Switching
When switching between LLM tools or sessions:

1. **Document current state** - Update TODO file with exact progress
2. **Note key decisions** - Record architectural choices and rationale
3. **List modified files** - Help next session understand scope
4. **Provide handoff prompt** - Include context for seamless transition

### Example Handoff Prompt Template
```
I'm working on [branch_name] implementing [objective]. 

Current status: [what's been completed]
Next steps: [what remains to be done]
Key files: [list of important files]
Decisions made: [architectural choices]

The TODO file in ai/todos/active/ contains full context. Please read it first, then continue with [specific next task].
```

## Branch Lifecycle Workflows

**IMPORTANT**: All TODO files in `ai/todos/` are Git-tracked. Always use Git commands (`git mv`, `git add`, `git commit`) when creating, moving, or modifying TODO files. Using regular file system operations (PowerShell `Move-Item`, bash `mv`, etc.) will not properly track changes in Git history.

### Workflow 1: Creating Branch from Backlog TODO

When you have a branch-ready TODO file (e.g., `ai/todos/backlog/TODO-utf8_no_bom.md`):

**Step 1: Move TODO to active on master (claims the work)**
```bash
git checkout master
git pull origin master
# IMPORTANT: Use git mv to preserve Git history when moving tracked files
git mv ai/todos/backlog/TODO-utf8_no_bom.md ai/todos/active/TODO-20251015_utf8_no_bom.md
```

**Step 2: Commit and push to master (makes work visible to team)**
```bash
git commit -m "Start utf8_no_bom work - move TODO to active"
git push origin master
```

**Step 3: Create feature branch**
```bash
git checkout -b Skyline/work/20251015_utf8_no_bom  # Use today's date matching TODO
```

**Step 4: Update TODO file header**
- Change "Branch Information (Future)" to "Branch Information"
- Fill in actual branch name and creation date
- Update any "will be" to actual values

**Step 5: Commit TODO update to branch**
```bash
git add ai/todos/active/TODO-20251015_utf8_no_bom.md
git commit -m "Update TODO with branch information"
git push -u origin Skyline/work/20251015_utf8_no_bom
```

**Why move TODO to master first?**
- Makes it immediately visible on master that this work is being claimed
- Prevents duplicate work by other developers
- Atomic operation: file move is committed before code changes begin
- Git history properly tracks the TODO lifecycle

### Workflow 2: Creating Branch and TODO Together

When starting fresh without a pre-existing TODO:

**Step 1: Create branch**
```bash
git checkout master
git pull origin master
git checkout -b Skyline/work/20251015_new_feature
```

**Step 2: Create TODO file in active/**
```bash
# Create ai/todos/active/TODO-20251015_new_feature.md with structure from template above
```

**Step 3: Commit TODO to branch**
```bash
git add ai/todos/active/TODO-20251015_new_feature.md
git commit -m "Initial TODO for new_feature"
git push -u origin Skyline/work/20251015_new_feature
```

### Workflow 3: Daily Development

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

### Workflow 4: Completing Work and Merging

**Step 1: Final updates to TODO**
```bash
# Add completion summary to TODO file
# Document what was actually done, bugs found, follow-up work
git add ai/todos/active/TODO-YYYYMMDD_description.md
git commit -m "Add completion summary to TODO"
git push
```

**Step 2: Create pull request**
- Document all changes and testing in PR description
- Reference any related issues
- Link to the active TODO file for full context

**Step 3: Add PR reference to TODO file**

**IMPORTANT**: TODO files must reference their PR before moving to completed/. This creates a permanent link between the work and its review/discussion.

```markdown
## Pull Request

**PR**: #1234
**Merged**: 2025-10-22
**Status**: Merged to master
```

Add this section to the TODO file in ai/todos/active/ and commit:
```bash
git add ai/todos/active/TODO-YYYYMMDD_description.md
git commit -m "Add PR #1234 reference to TODO"
git push
```

**Step 4: Prepare for merge (after PR is approved, before merging)**

**Update TODO checkboxes:**
- Mark all completed tasks as `[x]` in the Task Checklist and Success Criteria sections
- This creates an accurate historical record of what was accomplished
- Remove or mark as incomplete any tasks that were planned but not done

**Generate merge summary for GitHub:**
- Create a brief summary (single screen of text) for the merge commit message
- Include: Summary, Key Changes, Benefits, Testing status
- Reference the TODO file for complete details: `See todos/completed/TODO-YYYYMMDD_description.md for complete details.`
- Keep it concise - the TODO file contains the full context

**Example merge summary template:**
```markdown
## PR #XXXX: [Brief title]

### Summary
[1-2 sentences describing what was done and why]

### Key Changes
- [Major change 1]
- [Major change 2]
- [Major change 3]

### Benefits
- Users: [User-facing improvements]
- Developers: [Developer experience improvements]
- Code Quality: [Quality improvements]

### Testing
✅ [Test results summary]

See `todos/completed/TODO-YYYYMMDD_description.md` for complete details.

Co-Authored-By: Claude <noreply@anthropic.com>
```

**Step 5: Move TODO to completed/ with Git (after PR is merged)**
```bash
# IMPORTANT: Use git mv to preserve Git history when moving tracked files
# NEVER move to completed/ without a PR reference in the TODO file
git mv ai/todos/active/TODO-YYYYMMDD_description.md ai/todos/completed/TODO-YYYYMMDD_description.md
git commit -m "Move TODO to completed - PR #1234 merged"
git push
```

**Step 6: After merge to master**
```bash
git checkout master
git pull origin master
git branch -d Skyline/work/YYYYMMDD_description  # Delete local branch
```

**Step 7: Periodic cleanup (monthly or as needed)**
```bash
# Move old completed TODOs (>3 months) to archive or delete
git mv ai/todos/completed/TODO-20240715_old_work.md ai/todos/archive/
# Or simply delete if no longer needed (preserved in Git history)
git rm ai/todos/completed/TODO-20240715_old_work.md
```

### Workflow 5: Creating Backlog TODO (Planning)

#### 5a. Planning Future Work on Master

When you want to plan future work without creating a branch:

**Step 1: Create TODO in backlog/**
```bash
# Create ai/todos/backlog/TODO-new_feature.md with planning structure
```

**Step 2: Commit to master**
```bash
git checkout master
git add ai/todos/backlog/TODO-new_feature.md
git commit -m "Add backlog TODO for new_feature planning"
git push origin master
```

This makes the planned work visible to the team and LLM tools.

#### 5b. Creating Backlog TODO During Active Branch Work

When inspiration strikes during development on a feature branch, you can document the idea immediately and make it available to the team without waiting for your PR to merge.

**Option 1: Create TODO directly on master (recommended)**

```bash
# Save current work on feature branch
git stash

# Switch to master and create backlog TODO
git checkout master
git pull origin master

# Create ai/todos/backlog/TODO-new_idea.md with planning structure
# ... edit the file ...

git add ai/todos/backlog/TODO-new_idea.md
git commit -m "Add backlog TODO for new_idea planning"
git push origin master

# Return to feature branch and resume work
git checkout Skyline/work/YYYYMMDD_current_feature
git stash pop
```

**Option 2: Cherry-pick from feature branch to master**

```bash
# Create TODO on your current feature branch
# Create ai/todos/backlog/TODO-new_idea.md with planning structure
# ... edit the file ...

git add ai/todos/backlog/TODO-new_idea.md
git commit -m "Add backlog TODO for new_idea planning"

# Note the commit hash
git log -1 --oneline

# Switch to master and cherry-pick just that commit
git checkout master
git pull origin master
git cherry-pick <commit-hash>
git push origin master

# Return to feature branch
git checkout Skyline/work/YYYYMMDD_current_feature
```

**Why this matters:**
- New TODO ideas become available to the team immediately
- Other developers can start work on the idea without waiting for your PR merge
- Separates TODO lifecycle from feature branch lifecycle
- When your feature branch eventually merges, Git handles the duplicate commit gracefully

## Best Practices

### TODO File Management
- **Backlog TODOs**: Commit to master for visibility, cull items >6 months old
- **Active TODOs**: Update with every commit, keep context fresh
- **Completed TODOs**: Must have PR reference, retain 1-3 months for reference and knowledge transfer
- **Archive**: Move or delete old completed work, Git history preserves everything

**CRITICAL - Completed TODOs are Historical Records**:
- ❌ **NEVER modify TODO files in todos/completed/** (they document merged PRs)
- ✅ All completed TODOs **MUST** have a PR reference before moving to completed/
- ✅ They serve as a permanent record of decisions, context, and implementation details
- ✅ If doing follow-up work, create a new TODO that references the completed one

### Commit Messages
- Use clear, descriptive messages
- Reference issue numbers when applicable
- Keep commits focused and atomic
- Update TODO file with each commit

### Testing
- All new code must have appropriate tests
- Run existing tests to ensure no regressions
- Use translation-proof assertions
- Follow DRY principles in test code

### Documentation
- Update relevant documentation
- Add XML documentation for public APIs
- Keep comments focused and meaningful
- Update README.md when adding new features

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
