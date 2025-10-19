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
<root>/
  todos/
    active/           # Currently being worked on (committed to branch)
    completed/        # Recently completed (keep 1-3 months for reference)
    backlog/          # Ready to start, fully planned
    archive/          # Old completed work (>3 months old or as needed)
```

### TODO File Naming Convention

**Format**: `TODO-[YYYYMMDD_]<branch_specifier>.md`

- **Branch specifier**: Lowercase words separated by underscores (matches branch name after date)
- **No date prefix**: Branch-ready, planning phase (lives in `todos/backlog/`)
- **With date prefix**: Active branch, dated with branch creation date (lives in `todos/active/`, committed to branch)

**Examples**:
- `todos/backlog/TODO-utf8_no_bom.md` - Branch-ready, not yet created
- `todos/active/TODO-20251015_utf8_no_bom.md` - Active branch created 2025-10-15
- `todos/completed/TODO-20251010_webclient_replacement.md` - Recently merged work

### TODO File Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│ Phase 1: Planning (todos/backlog/)                          │
├─────────────────────────────────────────────────────────────┤
│ File: TODO-<branch_specifier>.md                            │
│ Status: Branch-ready, contains scope and task breakdown     │
│ Location: todos/backlog/, committed to master               │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼ Create branch
┌─────────────────────────────────────────────────────────────┐
│ Phase 2: Active Development (todos/active/)                 │
├─────────────────────────────────────────────────────────────┤
│ File: TODO-YYYYMMDD_<branch_specifier>.md                   │
│ Status: Renamed with date, moved to active/, on branch      │
│ Updates: Modified with every commit                         │
│ Purpose: Track progress, enable LLM context switching       │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼ Work complete, PR merged
┌─────────────────────────────────────────────────────────────┐
│ Phase 3: Completed (todos/completed/)                       │
├─────────────────────────────────────────────────────────────┤
│ File: Moved to todos/completed/ as final commit on branch   │
│ Status: Includes completion summary, merged to master       │
│ Purpose: Documentation, reference for future work           │
│ Retention: Keep 1-3 months for reference                    │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼ After 1-3 months
┌─────────────────────────────────────────────────────────────┐
│ Phase 4: Archived (todos/archive/)                          │
├─────────────────────────────────────────────────────────────┤
│ File: Moved to todos/archive/ or deleted (in Git history)   │
│ Status: Historical reference only                           │
│ Purpose: Reduce clutter, preserved in Git forever           │
└─────────────────────────────────────────────────────────────┘
```

### TODO File Structure (Backlog)

For files **in todos/backlog/** (planning phase):

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

For files **in todos/active/** (active branch):

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

For files **in todos/completed/** (after merge):

Add this **final section** before moving to completed/:

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
3. **Check project conventions** - Read `MEMORY.md` for essential context
4. **Understand the scope** - Confirm what remains to be done

### During Development
1. **Update TODO with every commit** - Track progress and decisions
2. **Follow coding standards** - Use `STYLEGUIDE.md` for style guidance
3. **Write appropriate tests** - Ensure code quality and regression prevention
4. **Use DRY principles** - Avoid duplication in long-lived codebase
5. **Handle exceptions properly** - Use established patterns for error handling

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

The TODO file in todos/active/ contains full context. Please read it first, then continue with [specific next task].
```

## Branch Lifecycle Workflows

### Workflow 1: Creating Branch from Backlog TODO

When you have a branch-ready TODO file (e.g., `todos/backlog/TODO-utf8_no_bom.md`):

**Step 1: Create branch**
```bash
git checkout master
git pull origin master
git checkout -b Skyline/work/20251015_utf8_no_bom  # Use today's date
```

**Step 2: Move and rename TODO file with date**
```bash
# On Windows (PowerShell)
Move-Item todos\backlog\TODO-utf8_no_bom.md todos\active\TODO-20251015_utf8_no_bom.md

# On Linux/Mac
mv todos/backlog/TODO-utf8_no_bom.md todos/active/TODO-20251015_utf8_no_bom.md
```

**Step 3: Update TODO file header**
- Change "Branch Information (Future)" to "Branch Information"
- Fill in actual branch name and creation date
- Update any "will be" to actual values

**Step 4: Commit TODO to branch**
```bash
git add todos/
git commit -m "Start utf8_no_bom work - move TODO to active"
git push -u origin Skyline/work/20251015_utf8_no_bom
```

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
# Create todos/active/TODO-20251015_new_feature.md with structure from template above
```

**Step 3: Commit TODO to branch**
```bash
git add todos/active/TODO-20251015_new_feature.md
git commit -m "Initial TODO for new_feature"
git push -u origin Skyline/work/20251015_new_feature
```

### Workflow 3: Daily Development

```bash
# Make code changes
# Update todos/active/TODO-YYYYMMDD_description.md (mark tasks complete, update context)
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
git add todos/active/TODO-YYYYMMDD_description.md
git commit -m "Add completion summary to TODO"
git push
```

**Step 2: Move TODO to completed/**
```bash
# On Windows (PowerShell)
Move-Item todos\active\TODO-YYYYMMDD_description.md todos\completed\TODO-YYYYMMDD_description.md

# On Linux/Mac
mv todos/active/TODO-YYYYMMDD_description.md todos/completed/TODO-YYYYMMDD_description.md

git add todos/
git commit -m "Move TODO to completed - ready for merge"
git push
```

**Step 3: Create pull request**
- Document all changes and testing in PR description
- Reference any related issues
- Link to the completed TODO file for full context

**Step 4: After merge to master**
```bash
git checkout master
git pull origin master
git branch -d Skyline/work/YYYYMMDD_description  # Delete local branch
```

**Step 5: Periodic cleanup (monthly or as needed)**
```bash
# Move old completed TODOs (>3 months) to archive or delete
git mv todos/completed/TODO-20240715_old_work.md todos/archive/
# Or simply delete if no longer needed (preserved in Git history)
git rm todos/completed/TODO-20240715_old_work.md
```

### Workflow 5: Creating Backlog TODO (Planning)

When you want to plan future work without creating a branch:

**Step 1: Create TODO in backlog/**
```bash
# Create todos/backlog/TODO-new_feature.md with planning structure
```

**Step 2: Commit to master**
```bash
git checkout master
git add todos/backlog/TODO-new_feature.md
git commit -m "Add backlog TODO for new_feature planning"
git push origin master
```

This makes the planned work visible to the team and LLM tools.

## Best Practices

### TODO File Management
- **Backlog TODOs**: Commit to master for visibility, cull items >6 months old
- **Active TODOs**: Update with every commit, keep context fresh
- **Completed TODOs**: Retain 1-3 months for reference and knowledge transfer
- **Archive**: Move or delete old completed work, Git history preserves everything

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
