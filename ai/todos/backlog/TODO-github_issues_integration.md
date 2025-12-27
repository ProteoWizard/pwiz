# TODO: GitHub Issues Integration

## Summary
Add structured access to ProteoWizard GitHub issues for unified issue tracking.

## Background
The ProteoWizard project has two issue tracking systems:
- **skyline.ms LabKey issues** - ~330 open (Dec 2024), historically for Skyline
- **GitHub issues** - ~87 open, for ProteoWizard library (msconvert, data readers)

Team preference is GitHub for consistency with open source norms. This integration would provide visibility into GitHub issues alongside LabKey issues.

## Recommended Approach: `gh` CLI Skills (Not MCP Server)

### Why Not an MCP Server?
The `gh` CLI already provides excellent GitHub access:
- `gh issue list --json` - Structured output
- `gh issue view` - Full issue details
- `gh issue create` - Create issues
- Already configured and working in Claude Code

An MCP server would duplicate this functionality with additional maintenance burden.

### Proposed Implementation
Create skills/commands that wrap `gh` with formatted output:

```
/pw-ghissues [days]     - Recent GitHub issues report
/pw-ghissue <number>    - Full issue details
```

These would:
1. Call `gh issue list --json` / `gh issue view --json`
2. Format output as markdown report
3. Save to `ai/.tmp/github-issues-*.md` for consistency with LabKey reports

## Scope

### Phase 1: Read Access
- [ ] Create `/pw-ghissues` skill for issue listing
- [ ] Create `/pw-ghissue` skill for single issue details
- [ ] Save formatted reports to `ai/.tmp/`
- [ ] Document in `ai/docs/`

### Phase 2: Unified View (Optional)
- [ ] Combined report showing both LabKey and GitHub issues
- [ ] Cross-reference between systems

### Phase 3: Write Operations (Optional)
- [ ] Issue creation via `gh issue create`
- [ ] Comment addition via `gh issue comment`

## Technical Notes

### `gh` JSON Output
```bash
gh issue list --repo ProteoWizard/pwiz --json number,title,state,createdAt,author,labels
gh issue view 123 --repo ProteoWizard/pwiz --json number,title,body,comments,state
```

### Authentication
`gh` uses existing GitHub authentication (OAuth via browser or token).
No additional credential management needed.

## Priority
Low - LabKey issues have more historical data. GitHub integration is nice-to-have.

## Related
- `ai/todos/completed/TODO-20251224_mcp_issues_integration.md` - LabKey issues
- `gh` CLI documentation: https://cli.github.com/manual/
