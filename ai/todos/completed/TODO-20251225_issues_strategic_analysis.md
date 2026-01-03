# TODO-20251225_issues_strategic_analysis.md

## Branch Information
- **Branch**: `ai-context`
- **Base**: `ai-context`
- **Created**: 2025-12-25
- **Completed**: 2025-12-27
- **Status**: ✅ Complete
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

### Phase 3: Experimental Workflow ✅
- [x] Pick backlog items for test cases (#3732, #3733, #3738)
- [x] Create GitHub Issues and work through full cycle
- [x] Evaluate: Worth it - GitHub Issues approach validated
- [x] Decide on direction: GitHub Issues replaces ai/todos/backlog

### Phase 4: Documentation & Tooling ✅
- [x] Create /pw-startissue command (zero-prompt issue startup)
- [x] Create /pw-issue command (transfer from backlog archive or skyline.ms)
- [x] Create /pw-auditdocs command (documentation audit)
- [x] Draft WORKFLOW-v2.md (swap-in ready)
- [x] Draft workflow-guide-v2.md (swap-in ready)
- [x] Create follow-up TODO for transition completion

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

### 2025-12-27 (Session 5) - Documentation & Completion
- Created WORKFLOW-v2.md and workflow-guide-v2.md (swap-in ready)
  - Merged workflow-guide.md + workflow-issues-guide.md
  - GitHub Issues as sole backlog (no ai/todos/backlog)
  - /pw-startissue as primary workflow entry point
  - Label-based branch strategy (ai-context label → ai-context branch)
  - Ownership signaling requirement (push TODO + comment on issue)
- Discussed MCP server requirements simplification:
  - gh.exe is only required tool for all developers
  - LabKeyMcp/Gmail MCP are optional power-user tools
  - +claude accounts only needed for wiki editing (not critical)
- Created follow-up TODO for transition completion
- Marked this strategic analysis TODO complete

### 2025-12-26 (Session 4) - Workflow Testing & Tooling
- Tested GitHub Issues workflow with Issue #3733 (Gmail MCP) - SUCCESSFUL
  - Issue created → TODO created on start → work completed → TODO moved to completed → Issue closed
  - Identified gap: need ownership signaling (push TODO + comment on issue)
  - Updated workflow-issues-guide.md with ownership signaling requirement
- Created /pw-startissue command for zero-prompt issue startup
  - Takes issue number, checks labels, determines branch strategy
  - `ai-context` label → work on ai-context branch directly
  - Other labels → create Skyline/work branch from master
- Created GitHub labels: `ai-context`, `todo`
- Created /pw-auditdocs command for comprehensive documentation audit
  - Sections: skills, commands, ai, docs, mcp
  - Replaced single-purpose audit-skills.ps1
- Eliminated ai/todos/backlog - GitHub Issues is now the only backlog
- Updated Issue #3732 (scheduled daily analysis) with Gmail completion status
- Ready for next test: /pw-startissue 3732

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

## Completion Summary

**What Was Decided:**
- GitHub Issues replaces ai/todos/backlog as sole backlog system
- ai/todos/active and ai/todos/completed remain for engineering context
- /pw-startissue command for zero-prompt workflow startup
- Label-based branch strategy: `ai-context` label → ai-context branch, others → master
- Ownership signaling required: push TODO to ai-context + comment on issue
- gh.exe is only required tool; MCP servers are optional power-user tools

**Deliverables Created:**
- `.claude/commands/pw-startissue.md` - Zero-prompt issue startup
- `.claude/commands/pw-issue.md` - Transfer from archive or skyline.ms
- `.claude/commands/pw-auditdocs.md` - Documentation audit command
- `ai/scripts/audit-docs.ps1` - Comprehensive audit script
- `ai/WORKFLOW-v2.md` - Quick reference (swap-in ready)
- `ai/docs/workflow-guide-v2.md` - Comprehensive guide (swap-in ready)
- `ai/docs/workflow-issues-guide.md` - GitHub Issues workflow documentation
- GitHub labels: `ai-context`, `todo`

**Follow-up Work:**
- See `ai/todos/backlog/TODO-issues_transition_completion.md`
