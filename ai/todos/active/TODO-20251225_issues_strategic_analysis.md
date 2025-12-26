# TODO-20251225_issues_strategic_analysis.md

## Branch Information
- **Branch**: `ai-context`
- **Base**: `ai-context`
- **Created**: 2025-12-25
- **Completed**: (pending)
- **Status**: 🚧 In Progress
- **PR**: N/A (documentation and analysis sprint)
- **Objective**: Strategic analysis of Skyline issues lists and planning for ai/todos integration

## Background

The Skyline project has two underutilized issue tracking systems:

1. **skyline.ms LabKey issues** (`/home/issues`)
   - 1058 total issues, 300 open (Dec 2025)
   - 147 Defects, 150 Todos, 3 Other
   - Used since Skyline inception, now mostly stale

2. **GitHub ProteoWizard issues** (github.com/ProteoWizard/pwiz/issues)
   - 325 total issues, 87 open
   - ProteoWizard core only (msconvert, data readers)
   - Never used for Skyline

### Current State
- Work tracked primarily through PRs
- Back-channel communication (email, G-chat, in-person) for prioritization
- ai/todos system working well for Claude-assisted sprints
- TeamCity integration provides standard way to share test builds

### Strategic Questions
1. Should we revive an issues list to replace ai/todos/backlog?
2. Which system: skyline.ms (historical) vs GitHub (open source standard)?
3. How to handle human factors (retiring developer's "ideas for future", ProteoWizard developer's concern about crowding)?

## Design Decisions

### ai/todos ↔ Issues Integration (Option A chosen)

**Option A: Keep ai/todos/completed, link from issue** ✅
- Issue is workflow tracker and pointer
- TODO.md is detailed engineering record with git history
- Issue lifecycle: Created → Active → Resolved (points to completed TODO)

**Option B: Upload TODO as attachment, eliminate ai/todos/completed** ❌
- Loses git history of TODO evolution
- Attachments harder to search
- Less elegant than version-controlled files

### GitHub Integration Approach

**Recommendation: Continue using `gh` CLI, not MCP server**
- `gh` already provides excellent structured output (`--json` flag)
- MCP server would duplicate functionality with maintenance burden
- Can create `/pw-ghissues` skill that wraps `gh` if needed

## Scope

### Phase 1: Analysis & Reporting Enhancements (Current)
- [x] Query skyline.ms issues (300 open)
- [x] Generate initial issues report
- [x] Enhance `save_issues_report()` with strategic dimensions:
  - [x] Summary by Area (component ownership)
  - [x] Summary by Milestone (target versions)
  - [x] Summary by Assignee (workload distribution)
  - [x] Age analysis (staleness of backlog)
- [x] Create strategy document in ai/docs
- [ ] Query GitHub issues for comparison
- [ ] Document findings and recommendations

### Phase 2: Strategy Document
- [ ] Document current workflow (PRs + back-channel)
- [ ] Document proposed workflow options
- [ ] Address human factors considerations
- [ ] Provide recommendations

## Technical Notes

### Issues Report Enhancements Needed

Current `save_issues_report()` has:
- Summary by Type (Defect, Todo)
- Summary by Priority
- Tables with ID, Title, Priority, Assigned, Modified

Missing strategic dimensions:
- **Area** - Which product areas have most issues?
- **Milestone** - Are milestones being used for planning?
- **Assignee distribution** - Who's carrying the backlog?
- **Age analysis** - How stale is the backlog?

### Initial Data Observations

From 300 open issues:
- Priority 3 dominates (250/300) - priority not used as differentiator
- Oldest modified: Issue #57 from 2014 (11 years old!)
- Heavy concentration of issues assigned to Brian Pratt
- Many issues last modified 2021-2022, spike in 2024-2025

### Migration Considerations

- Many old issues represent valuable ideas, not abandoned work
- Consider "Someday/Maybe" labels vs closing stale issues
- Clear labels/milestones to separate Skyline vs ProteoWizard if using GitHub
- No volunteer has stepped forward to own migration
- Any changes should be incremental, not disruptive

## Progress Log

### 2025-12-25
- Created TODO file
- Discussed strategic options for issues integration
- Decided on Option A (ai/todos/completed stays, issues as workflow tracker)
- Decided against GitHub MCP server (gh CLI sufficient)
- Generated initial issues report (300 open: 147 Defects, 150 Todos)
- Identified report enhancements needed (Area, Milestone, Assignee, Age)
- Noted initial data observations (Priority 3 dominance, 2014 staleness)
