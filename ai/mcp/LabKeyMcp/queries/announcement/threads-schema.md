# Threads Table Schema

**Schema:** `announcement`
**Table:** `Threads`

View that aggregates Announcement posts into threads with response counts.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| RowId | Integer | | | Thread ID (same as top-level Announcement RowId) |
| EntityId | Text | | | GUID for this thread |
| CreatedBy | Integer | core.SiteUsers.UserId | RO | User who created |
| Created | DateTime | | | When thread was created |
| Modified | DateTime | | RO, V | Last modification timestamp |
| LastIndexed | DateTime | | RO | Full-text search index timestamp |
| LatestId | Integer | | | RowId of most recent post |
| ResponseCount | Long | | | Number of replies |
| Title | Text | | | Thread title |
| AssignedTo | Integer | core.SiteUsers.UserId | | Assigned user |
| Status | Text | | | Thread status |
| Expires | DateTime | | | Expiration date |
| ResponseCreatedBy | Integer | core.SiteUsers.UserId | | User who made most recent post |
| ResponseCreated | DateTime | | | When most recent post was made |
| Folder | Text | core.Containers.EntityId | | Container reference |

## Notes

- This is a view, not a base table
- One row per thread (top-level post)
- ResponseCount is computed from child Announcement rows
- Used by `announcement_threads_recent` query
