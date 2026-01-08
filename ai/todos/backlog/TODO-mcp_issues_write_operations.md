# TODO: MCP Issues Write Operations

## Summary
Add write capabilities to the LabKey issues MCP tools, similar to wiki page updates.

## Background
The current issues MCP integration is read-only. Write operations would enable:
- Creating new issues from Claude Code
- Adding comments to existing issues
- Updating issue status, assignment, priority
- Closing/resolving issues when TODOs are completed

## Scope

### Core Operations
- [ ] `create_issue(title, body, type, area, priority)` - Create new issue
- [ ] `add_issue_comment(issue_id, comment)` - Add comment to issue
- [ ] `update_issue(issue_id, status, assigned_to, ...)` - Update issue fields
- [ ] `close_issue(issue_id, resolution, comment)` - Close with resolution

### Integration with TODOs
- [ ] Link completed TODOs to resolved issues
- [ ] Auto-create issues from backlog items (optional)

## Technical Notes

### API Patterns
Follow wiki update patterns from `tools/wiki.py`:
- Use LabKey API for inserts/updates
- Handle CSRF tokens if required
- Return confirmation with issue URL

### Permissions
The `+claude` accounts in "Agents" group need write permissions on `/home/issues`.
Currently configured as "Write without delete" - verify this is sufficient.

## Priority
Medium - Useful for workflow integration but not blocking current work.

## Related
- `ai/mcp/LabKeyMcp/tools/issues.py` - Current read-only implementation
- `ai/mcp/LabKeyMcp/tools/wiki.py` - Write pattern to follow
- `ai/todos/completed/TODO-20251224_mcp_issues_integration.md` - Read implementation
