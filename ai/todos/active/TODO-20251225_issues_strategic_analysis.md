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
3. What workflow makes sense? Test before committing to implementation.

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

### Phase 1: Analysis & Reporting Enhancements ✅
- [x] Query skyline.ms issues (300 open)
- [x] Generate initial issues report
- [x] Enhance `save_issues_report()` with strategic dimensions:
  - [x] Summary by Area (component ownership)
  - [x] Summary by Milestone (target versions)
  - [x] Summary by Assignee (workload distribution)
  - [x] Age analysis (staleness of backlog)
- [x] Create strategy document in ai/docs
- [x] Query GitHub issues for comparison (87 open)
- [x] Document findings and recommendations

### Phase 2: Strategy Document ✅
- [x] Document current workflow (PRs + back-channel)
- [x] Document proposed workflow options (4 options)
- [x] Document migration considerations
- [x] Provide recommendations

### Phase 3: Experimental Workflow (Optional Next Step)
- [ ] Pick a backlog item for test case
- [ ] Create GitHub Issue from backlog item
- [ ] Work through full workflow cycle
- [ ] Evaluate: Was the overhead worth it?
- [ ] Decide on direction

## Technical Notes

### Issues Report Enhancements (Completed)

Enhanced `save_issues_report()` now includes:
- Summary by Type (Defect, Todo)
- Summary by Priority
- **Summary by Area** (component ownership)
- **Summary by Milestone** (target versions)
- **Summary by Assignee** (workload distribution)
- **Age analysis** (staleness buckets)

### Key Findings

**skyline.ms Issues (300 open):**
- Priority 3 dominates (250/300) - not used as differentiator
- 233/300 are 3+ years stale (78%)
- 228/300 have no milestone (not used for planning)
- Oldest: Issue #57 from 2014 (11 years old!)
- Assignee distribution: Brendan (90), Brian (85), Kaipo (35), Nick (34)
- Kaipo left January 2023 - 35 issues need reassignment

**GitHub Issues (87 open):**
- Entirely msconvert/ProteoWizard focused
- Community-reported (external users)
- Labels: bug, enhancement, tutorial, can't reproduce
- Issues span 2018-2025
- Never used for Skyline (could add `skyline` label)

### Experimental Workflow Rationale

Try GitHub Issues workflow before committing to LabKey write operations:
1. `gh` CLI already works well
2. Low risk - can revert to file-system-only
3. Provides real-world test of issues-driven workflow
4. Defers MCP write operations until we know they're needed

## Progress Log

### 2025-12-26 (Session 3)
- Reorganized MCP documentation into `ai/docs/mcp/` folder
  - Moved: mcp-development-guide.md → mcp/development-guide.md
  - Moved: exception-triage-system.md → mcp/exceptions.md
  - Moved: nightly-test-analysis.md → mcp/nightly-tests.md
  - Moved: issues-strategy.md → mcp/issues-strategy.md
  - Split: wiki-support-system.md → mcp/wiki.md + mcp/support.md
  - Created: mcp/README.md (index)
- Updated Generate-TOC.ps1 to include MCP section
- Updated skills to reference new file locations
- Executed /pw-upconfig to sync wiki with source files
  - Updated AIDevSetup wiki (new +claude credential pattern)
  - Confirmed NewMachineBootstrap attachment in sync
- Documented LabKey MARKDOWN HTML comment bug (tested 2025-12-26)
- GitHub labels analysis: can use multiple labels, identified need for skyline/pwiz/todo

### 2025-12-25 (Session 2)
- Queried GitHub issues (87 open, entirely msconvert focused)
- Documented Option 4: GitHub Consolidation (experimental)
- Added Experimental GitHub Workflow section to strategy doc
- Updated recommendations: Try GitHub workflow before LabKey write ops
- Completed Phase 1 and Phase 2 analysis
- Phase 3 (optional experimental workflow) ready for test case selection

### 2025-12-25 (Session 1)
- Created TODO file
- Discussed strategic options for issues integration
- Decided on Option A (ai/todos/completed stays, issues as workflow tracker)
- Decided against GitHub MCP server (gh CLI sufficient)
- Generated initial issues report (300 open: 147 Defects, 150 Todos)
- Enhanced save_issues_report() with Area, Milestone, Assignee, Age
- Created ai/docs/issues-strategy.md
