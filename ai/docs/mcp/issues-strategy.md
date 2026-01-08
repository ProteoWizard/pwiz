# Skyline Issues Strategy

Strategic analysis and recommendations for issue tracking in the Skyline project.

## Current State

### How Work Gets Done Today
1. **PRs are the unit of work** - Most work tracked through pull requests
2. **Back-channel coordination** - Email, G-chat, in-person for prioritization
3. **ai/todos system** - Working well for Claude-assisted sprints (backlog → active → completed)
4. **TeamCity integration** - Standard way to share test builds with interested parties

### Existing Issue Systems

| System | Total | Open | Used For | Status |
|--------|-------|------|----------|--------|
| skyline.ms LabKey | 1058 | 300 | Skyline (historical) | Underutilized, stale |
| GitHub ProteoWizard | 325 | 87 | ProteoWizard core | Active for msconvert |

### Skyline.ms Issues Breakdown (Dec 2025)

**By Type:**
- 147 Defects
- 150 Todos
- 3 Other (Documentation)

**By Priority:**
- Priority 1: 1 issue
- Priority 2: 24 issues
- Priority 3: 250 issues (83%)
- Priority 4: 25 issues

**Observations:**
- Priority 3 dominates - not used as a differentiator
- Oldest issue last modified in 2014 (11 years stale)
- 233/300 issues are 3+ years stale
- 228/300 have no milestone assigned

**Context (Dec 2025):**
- Current release: 26.1 (was 25.2)
- Non-stale milestones: 25.2, 26.1, 26.2
- Kaipo Tamura left team January 2023 (35 issues assigned)
- Any milestone before 25.2 is historical
- Blank milestone = fell below priority line

**Potential Bulk Cleanup:**
- Close issues with no milestone and >1 year stale as "Won't Fix"
- Reassign Kaipo's issues to unassigned
- Result: Clean list ready for ai/todos integration

## Strategic Options

### Option 1: Status Quo+
Keep current workflow, use ai/todos as primary, issues as optional idea capture.

**Pros:**
- No migration effort
- ai/todos working well
- Minimal disruption

**Cons:**
- Issues lists continue to decay
- Valuable historical ideas lost
- No unified backlog view

### Option 2: Issues-Driven Workflow
Issues become canonical backlog, ai/todos/backlog goes away.

**Pros:**
- Single source of truth for ideas
- Better visibility for team
- Standard issue tracking workflow

**Cons:**
- Requires issues list hygiene
- Migration/cleanup effort
- Human factors (see below)

### Option 3: Hybrid Approach
External/community issues on GitHub, internal planning via ai/todos.

**Pros:**
- GitHub for open source engagement
- ai/todos for internal sprint work
- No forced migration

**Cons:**
- Two systems to monitor
- Unclear boundaries

### Option 4: GitHub Consolidation (Preferred Direction)
Migrate to GitHub Issues for all ProteoWizard/Skyline tracking.

**Pros:**
- Industry standard for open source
- `gh` CLI already configured and working
- Community can contribute issues easily
- Consolidates two systems into one
- No MCP write operations needed
- Aligns with GitHub for version control
- Team members favor this direction
- Assignee list = org members (auto-maintained)

**Cons:**
- Requires migration of skyline.ms issues (or archival)
- Wiki page redirect needed
- Some historical context loss

**Prerequisites:**
- Matt Chambers to clean up stale org members (7 to remove)
- Create `skyline`, `pwiz`, `todo` labels
- Bulk-label existing issues as `pwiz`

## GitHub Issues Analysis (Dec 2025)

**Open Issues:** 87
**Labels in use:** bug, enhancement, tutorial, can't reproduce, good first issue, wontfix, help wanted

**Character:**
- Entirely ProteoWizard/msconvert focused
- Community-reported issues (external users)
- Never used for Skyline (could add `skyline` label)
- Issues span 2018-2025 (oldest: #214 from Sept 2018)

**Sample Recent Issues:**
- #3703: Scan Summing filter losing MS2 info (Dec 2025)
- #3606: FragPipe/EasyPQP mismatch (Sept 2025)
- #3571: Waters Xevo DIA conversion (July 2025)

## Experimental GitHub Workflow

Before investing in LabKey write operations, test the workflow with GitHub Issues.

### Proposed Workflow
```
GitHub Issue (idea capture, internal or external)
    ↓
    "gh issue view N --json body" downloads to ai/todos/active
    ↓
ai/todos/active/TODO-YYYYMMDD_feature.md (working document)
    ↓
    (sprint work, git commits on feature branch)
    ↓
Completed TODO attached: "gh issue comment N --body-file TODO.md"
    ↓
Issue closed: "gh issue close N"
    ↓
TODO removed from local tree (lives as attachment on issue)
```

### What We'd Learn
1. Is the overhead of issue ↔ file sync worth it?
2. Do attachments provide enough record, or do we miss git history?
3. Does the workflow feel natural or forced?
4. Is GitHub Issues suitable for internal planning (not just community reports)?

### Low Risk
- If workflow doesn't work, continue with file-system-only
- No LabKey write operations needed for experiment
- skyline.ms issues can remain as historical archive

### Test Case
Pick one item from `ai/todos/backlog/`, convert to GitHub Issue, work it through the full cycle.

**Candidate:** Select a small, well-defined backlog item that can be completed in one sprint.

## Recommended Integration Model

If issues integration proceeds, use this model:

### Issue Lifecycle
```
Created (idea capture)
    ↓
Active (sprint started)
    → TODO.md downloaded to ai/todos/active
    → Issue updated with "In Progress" status
    ↓
Resolved
    → Points to ai/todos/completed/TODO-YYYYMMDD_feature.md
    → Issue closed with resolution
```

### Why Keep ai/todos/completed?
- **Git history** - Version-controlled evolution of the work
- **Searchability** - `grep` across all completed work
- **Offline access** - Full clone, no API calls needed
- **Detailed record** - Progress logs, technical notes, learnings

The issue becomes a **workflow tracker** and **pointer**, while the TODO.md remains the **detailed engineering record**.

## Migration Considerations

### Backlog Hygiene
- Many old issues represent valuable ideas, not abandoned work
- Consider "Someday/Maybe" labels vs closing stale issues
- Frame any migration as "fresh start" not "cleanup"

### System Boundaries
- Clear labels/milestones to separate Skyline vs ProteoWizard if using GitHub
- Respect existing ownership patterns
- Discuss expectations before any migration

### General Observations
- No volunteer has stepped forward to own a migration effort
- Any changes should be incremental, not disruptive

## GitHub Integration Approach

### Recommendation: Use `gh` CLI, Not MCP Server

The GitHub CLI already provides excellent access:
```bash
gh issue list --repo ProteoWizard/pwiz --json number,title,state,labels
gh issue view 123 --json number,title,body,comments
```

**Why not an MCP server?**
- `gh` is well-maintained by GitHub
- Already configured in development environment
- MCP server would duplicate functionality
- Additional maintenance burden for minimal benefit

**If structured reports needed:**
- Create `/pw-ghissues` skill that wraps `gh`
- Format output like LabKey reports (save to `ai/.tmp/`)

## Immediate Actions

### Analysis Enhancements ✅ (Completed Dec 2025)
`save_issues_report()` now includes:
- Summary by Area (component ownership)
- Summary by Milestone (target versions)
- Summary by Assignee (workload distribution)
- Age analysis (staleness of backlog)

### Strategic Questions Answered
1. **Ancient issues (>3yr)?** 233/300 (78%)
2. **Backlog distribution?** Brendan (90), Brian (85), Kaipo (35), Nick (34)
3. **Milestones in use?** 228/300 have no milestone (not used for planning)
4. **Product areas?** Most are "Skyline" or blank

### Recommended Next Steps
1. **Trial run** - Create one Skyline issue from backlog, work through cycle
2. **Coordinate with Matt Chambers** - Clean up stale org members
3. **Create labels** - `skyline`, `pwiz`, `todo`
4. **Bulk-label existing issues** - Add `pwiz` to all 87 open issues
5. **Update wiki** - Redirect skyline.ms/issues page to GitHub
6. **Archive skyline.ms issues** - Close as "Won't Fix" or leave as historical

### Migration Plan (When Ready)

**Phase 1: Setup**
```bash
gh label create "skyline" --description "Skyline application" --color "0066cc"
gh label create "pwiz" --description "ProteoWizard/msconvert" --color "cc6600"
gh label create "todo" --description "Task or feature request" --color "22cc66"
```

**Phase 2: Label existing issues**
```bash
# Add pwiz label to all open issues
gh issue list --state open --json number --limit 500 | \
  pwsh -Command '$input | ConvertFrom-Json | ForEach-Object {
    gh issue edit $_.number --add-label "pwiz"
  }'
```

**Phase 3: Redirect wiki page**
- Update skyline.ms issues wiki to point to GitHub
- Add instructions for filing Skyline issues (use `skyline` label)

**Phase 4: skyline.ms cleanup**
- Option A: Bulk close as "Won't Fix - Moved to GitHub"
- Option B: Leave as read-only historical archive

## Related Documentation

- [MCP Issues Integration TODO](../todos/completed/TODO-20251224_mcp_issues_integration.md)
- [MCP Issues Write Operations](../todos/backlog/TODO-mcp_issues_write_operations.md)
- [GitHub Issues Integration](../todos/backlog/TODO-github_issues_integration.md)
