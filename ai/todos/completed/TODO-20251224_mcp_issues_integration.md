# TODO-20251224_mcp_issues_integration.md

## Branch Information
- **Branch**: `ai-context`
- **Base**: `ai-context`
- **Created**: 2025-12-24
- **Completed**: 2025-12-25
- **Status**: ✅ Complete
- **PR**: (pending)
- **Objective**: Add issues tracking capabilities to LabKey MCP server for skyline.ms and GitHub

## Background

The Skyline project has two issue tracking systems that are underutilized:

1. **skyline.ms LabKey issues** (`/home/issues`)
   - ~1039 total issues (330 open as of Dec 2024)
   - Categories: Defect, Todo
   - Areas: Skyline (and others)
   - Historically used when Skyline was smaller; less active now

2. **GitHub ProteoWizard issues** (https://github.com/ProteoWizard/pwiz/issues)
   - ~315 total issues (87 open)
   - Historically for ProteoWizard library (msconvert, data readers)
   - Team preference is to use GitHub for consistency with open source norms

### Relationship to TODO Tracking System

The `ai/todos/` system (backlog → active → completed) works well for tracking work from initiation to completion with version-controlled records. Team feedback suggests better integration with an issues list for:

- **Backlog phase**: Issues could feed into `ai/todos/backlog/`
- **Completion phase**: Completed TODOs could "resolve" corresponding issues

This MCP integration creates a data channel to explore this relationship and access the valuable unexecuted ideas in these issue lists.

## Scope

### Phase 1: LabKey Issues Schema (Complete)
- [x] Document issues schema structure
- [x] Create `ai/mcp/LabKeyMcp/queries/issues/` documentation
- [x] Update `ai/docs/mcp-development-guide.md` with schema-first approach
- [x] Mark large fields (Comment) with ⚠️ LARGE warnings
- [x] Document EntityId → corex.documents attachment relationship
- [x] Enable corex schema on /home/issues (user action)
- [x] Create server-side SQL queries (.sql files)
- [x] Implement `query_issues()` - list/filter issues
- [x] Implement `get_issue_details()` - full issue with comments
- [x] Implement `save_issues_report()` - summary report to file
- [x] User creates queries on LabKey server
- [x] Test with real queries
- [x] Add `save_exceptions_report()` for consistent daily reports
- [x] Update authentication docs to individual +claude accounts

### Phase 2: GitHub Issues Integration (Future)
- [ ] Research GitHub CLI (`gh`) vs GitHub API for MCP
- [ ] Design tools for querying ProteoWizard issues
- [ ] Consider unified issue view across both sources

### Phase 3: TODO Integration Strategy (Future)
- [ ] Document workflow options for issues ↔ TODOs
- [ ] Prototype bidirectional linking if warranted

## Technical Notes

### Issues Schema Structure

Container: `/home/issues`
Schema: `issues`

Key tables:
- `issues` - Main issue table (IssueId, Title, Status, Type, Area, Priority, AssignedTo, etc.)
- `Comments` - Issue comments linked by IssueId
- `IssueListDef` - Issue list configuration metadata

See `ai/mcp/LabKeyMcp/queries/issues/` for full schema documentation.

### Key Lookups
- `AssignedTo` → `issues.UsersData.UserId`
- `Type` → `lists.issues-type-lookup` (Defect, Todo)
- `Area` → `lists.issues-area-lookup`
- `Priority` → `lists.issues-priority-lookup`
- `Resolution` → `lists.issues-resolution-lookup`

## Progress Log

### 2025-12-24
- Created TODO file
- Explored `/home/issues` schema structure via MCP
- Created schema documentation:
  - `ai/mcp/LabKeyMcp/queries/issues/issues-schema.md`
  - `ai/mcp/LabKeyMcp/queries/issues/comments-schema.md`
  - `ai/mcp/LabKeyMcp/queries/issues/issuelistdef-schema.md`
- Updated `ai/mcp/LabKeyMcp/queries/README.md` with issues schema section
- Updated `ai/docs/mcp-development-guide.md`:
  - Added "Schema-First Development" section
  - Updated container paths list
- Marked large fields:
  - `issues.Comments.Comment` as ⚠️ LARGE
  - Documented EntityId → corex.documents for attachments
- Updated corex/documents-schema.md to include /home/issues container
- User enabling corex schema on /home/issues for attachment support
- Created server-side SQL queries:
  - `issues_list.sql` - List all issues with user display names
  - `issue_with_comments.sql` - Single issue with comments (parameterized)
  - `issues_by_status.sql` - Filter by status with date range
- Implemented Python MCP tools in `ai/mcp/LabKeyMcp/tools/issues.py`:
  - `query_issues()` - List/filter issues
  - `get_issue_details()` - Full issue with comments → file
  - `save_issues_report()` - Summary report → file
- Updated `tools/__init__.py` to register issues module
- Updated `tools/common.py` with ISSUES_SCHEMA constants
- Updated `queries/README.md` with issues queries section
- **Waiting**: User to create queries on LabKey server
- Discovered ORDER BY unreliable in LabKey SQL - must use API `sort` parameter
- Applied `sort="-Modified"` fix to issues.py
- Documented finding in ai/docs/mcp-development-guide.md
- Applied sort fixes to support.py, nightly.py
- Added `save_exceptions_report()` for consistent file-based daily reports
- Updated `/pw-exceptions` command to use file-based approach (matches /pw-nightly, /pw-support)
- Updated all authentication docs from shared `claude.c.skyline@gmail.com` to individual `+claude` accounts:
  - Team members: `yourname+claude@proteinms.net`
  - Interns/others: `yourname+claude@gmail.com`
  - Added warning that +claude only works with Gmail-backed providers
- Updated: developer-setup-guide.md, exception-triage-system.md, wiki-support-system.md, README.md, test_connection.py
