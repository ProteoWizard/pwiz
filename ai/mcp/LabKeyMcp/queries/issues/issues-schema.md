# issues.issues Table Schema

**Container:** `/home/issues`
**Schema:** `issues`
**Table:** `issues`

Main issue tracking table containing bug reports, feature requests, and TODO items.

## Columns

| Column | Type | Lookup | Description |
|--------|------|--------|-------------|
| IssueId | Integer | | Primary key (AI, PK, Req, RO) |
| Folder | Text | core.Containers.EntityId | Container reference |
| Related | Integer | issues.all_issues.IssueId | Related issues (MVFK) |
| EntityId | Text | | Entity identifier (Req, RO) |
| IssueDefId | Integer | issues.IssueListDef.RowId | Issue definition reference |
| Duplicate | Integer | issues.issues.IssueId | Duplicate of issue |
| Status | Text | | Issue status (Req) |
| Created | DateTime | | Creation timestamp |
| CreatedBy | Integer | core.SiteUsers.UserId | Creator |
| Modified | DateTime | | Last modification (RO) |
| ModifiedBy | Integer | core.SiteUsers.UserId | Last modifier |
| Resolved | DateTime | | Resolution timestamp |
| ResolvedBy | Integer | core.Users.UserId | Resolver |
| Closed | DateTime | | Closure timestamp |
| ClosedBy | Integer | core.Users.UserId | Closer |
| AssignedTo | Integer | issues.UsersData.UserId | Assignee |
| Title | Text | | Issue title |
| Type | Text | lists.issues-type-lookup | Issue type |
| Area | Text | lists.issues-area-lookup | Product area |
| NotifyList | Text | | Email notification list |
| Priority | Integer | lists.issues-priority-lookup | Priority level |
| Milestone | Text | lists.issues-milestone-lookup | Target milestone |
| Resolution | Text | lists.issues-resolution-lookup | Resolution type |
| Sponsor | Text | lists.issues-Sponsor-lookup | Sponsor (LimPHI) |

## Key Values

### Status
- `open`
- `closed`

### Type
- `Defect` - Bug report
- `Todo` - Task/feature request

### Area
- `Skyline`
- (check lists.issues-area-lookup for full list)

### Priority
- 1-5 (lower = higher priority, observed: 3 is common)

## Attachments

Issues can have file attachments stored in `corex.documents`:
- Use `EntityId` from this table to query `corex.documents.parent`
- Always use `documents_metadata` query (excludes binary blob)
- Use `get_attachment()` or `list_attachments()` MCP tools

## Usage Notes

- Total rows: ~1039 (330 open as of Dec 2024)
- Used for Skyline issue tracking at skyline.ms
- AssignedTo references issues.UsersData (not core.Users)
- Several fields use list lookups for controlled vocabularies
- EntityId enables attachment lookup via corex.documents
