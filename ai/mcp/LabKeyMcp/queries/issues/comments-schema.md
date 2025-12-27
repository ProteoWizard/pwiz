# issues.Comments Table Schema

**Container:** `/home/issues`
**Schema:** `issues`
**Table:** `Comments`

Comments and updates on issues.

## Columns

| Column | Type | Lookup | Description |
|--------|------|--------|-------------|
| CommentId | Integer | | Comment ID (AI, PK, Req, RO) |
| IssueId | Integer | issues.issues.IssueId | Parent issue (PK, Req) |
| CreatedBy | Integer | core.SiteUsers.UserId | Comment author (RO) |
| Created | DateTime | | Comment timestamp (Req) |
| Comment | Text | | **⚠️ LARGE** - Comment content, use `get_issue_details()` which saves to file |

## Usage Notes

- Linked to issues via IssueId
- CommentId + IssueId form composite primary key
- Used to track issue history and discussion
- Comment field can be arbitrarily large - avoid querying directly
