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
- Many issues from 2021-2022 with recent spike in 2024-2025

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

### Analysis Enhancements
Improve `save_issues_report()` to include:
- Summary by Area (component ownership)
- Summary by Milestone (target versions)
- Summary by Assignee (workload distribution)
- Age analysis (staleness of backlog)

### Strategic Questions to Answer
1. How many issues are "ancient" (>3 years untouched)?
2. Which developers carry most of the backlog?
3. Are milestones being used for planning?
4. Which product areas have the most open issues?

## Related Documentation

- [MCP Issues Integration TODO](../todos/completed/TODO-20251224_mcp_issues_integration.md)
- [MCP Issues Write Operations](../todos/backlog/TODO-mcp_issues_write_operations.md)
- [GitHub Issues Integration](../todos/backlog/TODO-github_issues_integration.md)
