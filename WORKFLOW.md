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
The TODO file system enables seamless context management for LLM-assisted development, from initial planning through branch completion.

### TODO File Naming Convention

**Format**: `TODO-[YYYYMMDD_]<branch_specifier>.md`

- **Branch specifier**: Lowercase words separated by underscores (matches branch name after date)
- **No date prefix**: Branch-ready, planning phase, not yet committed to Git
- **With date prefix**: Active branch, dated with branch creation date, committed to branch

**Examples**:
- `TODO-utf8_no_bom.md` - Branch-ready, not yet created
- `TODO-20251015_utf8_no_bom.md` - Active branch created 2025-10-15
- `TODO-webclient_replacement.md` - Branch-ready backlog item
- `TODO-20251010_webclient_replacement.md` - Active branch created 2025-10-10

### TODO File Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│ Phase 1: Planning (Not in Git)                             │
├─────────────────────────────────────────────────────────────┤
│ File: TODO-<branch_specifier>.md                            │
│ Status: Branch-ready, contains scope and task breakdown     │
│ Location: Local workspace, not committed                    │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼ Create branch
┌─────────────────────────────────────────────────────────────┐
│ Phase 2: Active Development (In Git)                        │
├─────────────────────────────────────────────────────────────┤
│ File: TODO-YYYYMMDD_<branch_specifier>.md                   │
│ Status: Renamed with date, committed to work branch         │
│ Updates: Modified with every commit                         │
│ Purpose: Track progress, enable LLM context switching       │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼ Work complete
┌─────────────────────────────────────────────────────────────┐
│ Phase 3: Pre-Merge Cleanup                                  │
├─────────────────────────────────────────────────────────────┤
│ File: Deleted from branch                                   │
│ Status: Removed before merging to master                    │
│ Purpose: Clean merge, no TODO files in master               │
└─────────────────────────────────────────────────────────────┘
```

### TODO File Structure (Branch-Ready)

For files **without date prefix** (planning phase):

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

### TODO File Structure (Active Branch)

For files **with date prefix** (active branch):

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

The TODO file contains full context. Please read it first, then continue with [specific next task].
```

## TODO File Planning Phase

### Creating Branch-Ready TODO Files

Before creating a branch, you can prepare a TODO file to plan the work:

**File naming**: `TODO-<branch_specifier>.md` (no date prefix)

**Purpose**:
- Scope the work before committing to a branch
- Document background and rationale
- Break down tasks into phases
- Identify risks and success criteria
- Enable LLM-assisted branch creation

**Location**: Local workspace, not committed to Git

**Example**: `TODO-utf8_no_bom.md` contains full planning for future UTF-8 standardization work

### LLM-Assisted Branch Creation

When ready to start work on a planned TODO:

**Handoff prompt to LLM**:
```
I want to start work on [description] from TODO-<branch_specifier>.md.

Please:
1. Create branch: Skyline/work/YYYYMMDD_<branch_specifier> (use today's date)
2. Rename TODO file to include the date
3. Update the TODO file header with actual branch information
4. Commit the TODO file to the new branch
5. Begin Phase 1 of the work

The TODO file contains full context. Let's get started.
```

The LLM will execute the workflow and begin implementing the first tasks.

## Branch Lifecycle Workflows

### Workflow 1: Creating Branch from Existing TODO File

When you have a branch-ready TODO file (e.g., `TODO-utf8_no_bom.md`):

**Step 1: Create branch**
```bash
git checkout master
git pull origin master
git checkout -b Skyline/work/20251015_utf8_no_bom  # Use today's date
```

**Step 2: Rename TODO file with date**
```bash
# On Windows (PowerShell)
Rename-Item TODO-utf8_no_bom.md TODO-20251015_utf8_no_bom.md

# On Linux/Mac
mv TODO-utf8_no_bom.md TODO-20251015_utf8_no_bom.md
```

**Step 3: Update TODO file header**
- Change "Branch Information (Future)" to "Branch Information"
- Fill in actual branch name and creation date
- Update any "will be" to actual values

**Step 4: Commit TODO to branch**
```bash
git add TODO-20251015_utf8_no_bom.md
git commit -m "Initial TODO for utf8_no_bom"
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

**Step 2: Create TODO file**
```bash
# Create TODO-20251015_new_feature.md with structure from template above
```

**Step 3: Commit TODO to branch**
```bash
git add TODO-20251015_new_feature.md
git commit -m "Initial TODO for new_feature"
git push -u origin Skyline/work/20251015_new_feature
```

### Workflow 3: Daily Development

```bash
# Make code changes
# Update TODO-YYYYMMDD_description.md (mark tasks complete, update context)
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

**Step 1: Final cleanup**
```bash
# Run final tests
# Verify all tasks in TODO are complete
# Review code for quality
```

**Step 2: Remove TODO file**
```bash
git rm TODO-YYYYMMDD_description.md
git commit -m "Remove TODO file before merge"
git push
```

**Step 3: Create pull request**
- Document all changes and testing in PR description
- Reference any related issues

**Step 4: After merge**
```bash
git checkout master
git pull origin master
git branch -d Skyline/work/YYYYMMDD_description  # Delete local branch
```

### Merging to Master
1. **Create pull request** - Document all changes and testing
2. **Code review** - Ensure quality and standards compliance
3. **Merge** - Squash commits for clean history
4. **Delete branch** - Clean up after successful merge

## Best Practices

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
