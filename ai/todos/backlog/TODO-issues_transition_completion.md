# TODO-issues_transition_completion.md

## Branch Information (Future)
- **Branch**: Not yet created - will be `ai-context` (documentation work)
- **Objective**: Complete transition from ai/todos/backlog to GitHub Issues workflow

## Background

Strategic analysis sprint (TODO-20251225_issues_strategic_analysis.md) decided:
- GitHub Issues replaces ai/todos/backlog as sole backlog system
- Created v2 documentation ready to swap in
- Need to complete transition before team-wide adoption

## Prerequisites

Before starting this work:
1. Team briefing on new workflow (or async communication)
2. Confirm v2 documentation is ready for production use

## Task Checklist

### Phase 1: Swap In Documentation
- [ ] Review WORKFLOW-v2.md and workflow-guide-v2.md one final time
- [ ] Rename current docs to -v1.md (preserve for reference)
  - `ai/WORKFLOW.md` → `ai/WORKFLOW-v1.md`
  - `ai/docs/workflow-guide.md` → `ai/docs/workflow-guide-v1.md`
- [ ] Rename v2 docs to production names
  - `ai/WORKFLOW-v2.md` → `ai/WORKFLOW.md`
  - `ai/docs/workflow-guide-v2.md` → `ai/docs/workflow-guide.md`
- [ ] Delete workflow-issues-guide.md (merged into workflow-guide.md)
- [ ] Update any cross-references in other docs
- [ ] Regenerate TOC.md

### Phase 2: Archive ai/todos/backlog
- [ ] Review remaining items in ai/todos/backlog/
- [ ] For items worth keeping: create GitHub Issues via /pw-issue
- [ ] Create git tag: `backlog-archive-YYYYMMDD`
- [ ] Delete ai/todos/backlog/ folder
- [ ] Update .gitignore if needed

### Phase 3: Deprecate skyline.ms Issues (Optional)
- [ ] Bulk close all open issues on skyline.ms/home/issues
- [ ] Add closing comment pointing to GitHub Issues
- [ ] Document in ai/docs/mcp/issues-strategy.md that list is deprecated

### Phase 4: Team Communication
- [ ] Update developer-setup-guide.md with tiered requirements:
  - Tier 1 (Everyone): gh.exe
  - Tier 2 (Power users): LabKeyMcp with +claude account
  - Tier 3 (Automation): Gmail MCP
- [ ] Brief team on /pw-startissue workflow
- [ ] Update any onboarding documentation

## Success Criteria

- [ ] WORKFLOW.md and workflow-guide.md reflect GitHub Issues workflow
- [ ] ai/todos/backlog/ no longer exists (archived in git tag)
- [ ] Team understands new workflow
- [ ] /pw-startissue is the standard way to begin work

## Risks & Considerations

- **Team adoption**: New workflow requires understanding; don't rush
- **Backlog recovery**: Git tag ensures items can be recovered if needed
- **skyline.ms issues**: Bulk close is optional; can defer indefinitely

## Notes

This is documentation/process work - no code changes. Can be done incrementally.
The v2 documentation is already complete; this TODO is about the swap-in and cleanup.
