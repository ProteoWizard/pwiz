# TODO-20260106_issues_transition.md

## Branch Information
- **Branch**: `ai-context`
- **Base**: `ai-context`
- **Created**: 2026-01-06
- **Completed**: (pending)
- **Status**: In Progress
- **PR**: N/A (ai-context documentation work)
- **Objective**: Complete transition from ai/todos/backlog to GitHub Issues as the sole backlog system

## Background

Strategic analysis (issues-strategy.md, Dec 2025) decided:
- GitHub Issues replaces ai/todos/backlog as the backlog system
- Use `gh` CLI for GitHub access (not MCP server)
- skyline.ms LabKey issues to be deprecated
- v2 documentation already prepared and ready to swap in

Commands `/pw-issue` and `/pw-startissue` are already implemented.

## Task Checklist

### Phase 1: Cleanup Obsolete Files
- [x] Delete TODO-mcp_issues_write_operations.md (obsolete - chose gh CLI over MCP)
- [x] Delete TODO-github_issues_integration.md (obsolete - integration done)
- [x] Delete TODO-issues_transition_completion.md (merged into this TODO)
- [x] Delete issues-strategy.md (decision made, outcome in v2 docs)

### Phase 2: Swap In v2 Documentation
- [x] Review WORKFLOW-v2.md and workflow-guide-v2.md one final time
- [x] Rename current docs to -v1.md (preserve for reference)
  - `ai/WORKFLOW.md` -> `ai/WORKFLOW-v1.md`
  - `ai/docs/workflow-guide.md` -> `ai/docs/workflow-guide-v1.md`
- [x] Rename v2 docs to production names
  - `ai/WORKFLOW-v2.md` -> `ai/WORKFLOW.md`
  - `ai/docs/workflow-guide-v2.md` -> `ai/docs/workflow-guide.md`
- [x] Delete workflow-issues-guide.md (merged into workflow-guide-v2.md)
- [x] Cross-references in completed TODOs are historical (no change needed)
- [x] Regenerate TOC.md

### Phase 3: Archive ai/todos/backlog
- [x] Reviewed remaining items - keeping 12 backlog TODOs (deleted TODO-compress_vendor_test_data.md)
- [ ] When activating old backlog TODO: create GitHub Issue first, then /pw-startissue
- [x] Deleted todos/archive/ folder (will use completed/YYYY/ for archiving)
- [x] Updated WORKFLOW.md and workflow-guide.md with refined completion workflow
- [ ] Create git tag after all changes committed

### Phase 4: Deprecate skyline.ms Issues
- [x] Bulk close all open issues on skyline.ms/home/issues
  - Added "GitHub Move" resolution value
  - Customized "Bulk issue resolver" wiki page script
  - Bulk closed ~300 open issues with resolution "GitHub Move"
  - Bulk closed ~49 resolved-but-not-closed issues
  - Blanked wiki script (history preserved for future reference)
- [x] Update skyline.ms wiki page for issues
  - Updated /home/issues default wiki page with redirect to GitHub Issues
  - Historical issues remain browsable below the notice

### Phase 5: GitHub Setup
- [x] Verify labels exist: `skyline`, `pwiz`, `todo`, `ai-context` (all present)
- [ ] Bulk-label existing GitHub issues as `pwiz` (optional - 87 open as of Dec 2025)

### Phase 6: Documentation Updates
- [ ] Update developer-setup-guide.md if needed
- [ ] Ensure /pw-issue and /pw-startissue commands are documented
- [ ] Update any remaining cross-references

## Progress Log

### 2026-01-06 - Session Start

Beginning transition sprint. Key decisions:
- issues-strategy.md served its purpose (decision-making) - deleted
- Three obsolete TODO files consolidated into this one
- Will complete full transition including LabKey bulk close

**Phase 1 completed:**
- Deleted TODO-mcp_issues_write_operations.md
- Deleted TODO-github_issues_integration.md
- Deleted TODO-issues_transition_completion.md
- Deleted issues-strategy.md

**Phase 2 completed:**
- Swapped in v2 documentation (WORKFLOW.md, workflow-guide.md)
- Deleted workflow-issues-guide.md (merged into v2)
- Preserved v1 docs as WORKFLOW-v1.md, workflow-guide-v1.md
- Regenerated TOC.md

**Phase 3 completed:**
- Deleted TODO-compress_vendor_test_data.md (low priority, marked reconsider)
- Keeping 12 remaining backlog TODOs (workflow: create GitHub Issue first when activating)
- Deleted todos/archive/ folder
- Updated completion workflow: post summary to GitHub Issue, keep detailed TODO in completed/
- Archiving strategy: move to completed/YYYY/ when folder exceeds ~25 files
- Added "Why Both Systems?" rationale to workflow-guide.md explaining ai/todos value for LLM-assisted development

**Phase 4 completed:**
- Added "GitHub Move" resolution value to LabKey
- Updated /home/issues wiki page with redirect notice to GitHub Issues
- Customized "Bulk issue resolver" wiki page script for GitHub migration
- Bulk closed ~300 open issues with resolution "GitHub Move"
- Bulk closed ~49 resolved-but-not-closed issues
- Blanked wiki script after use (history preserved in LabKey)

## Success Criteria

- [x] WORKFLOW.md and workflow-guide.md reflect GitHub Issues workflow (v2 swapped in)
- [ ] ai/todos/backlog/ - keeping for now, will clean up as items activate
- [x] skyline.ms issues bulk closed with "GitHub Move" resolution
- [x] skyline.ms wiki updated to point to GitHub
- [x] /pw-startissue is the documented way to begin work

## Notes

This is documentation/process work on ai-context branch - no code changes, no PR needed.
The v2 documentation is already complete; this sprint is about the swap-in and cleanup.
