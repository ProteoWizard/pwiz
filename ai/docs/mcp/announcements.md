# Announcements Table

The `announcement.Announcement` table is a general-purpose content table used throughout skyline.ms for various purposes. The same schema exists in different containers, each serving a different function.

## Container Map

| Container | Purpose | MCP Tools |
|-----------|---------|-----------|
| `/home/support` | Support board | `query_support_threads`, `get_support_thread` |
| `/home/issues/exceptions` | Exception tracking | `query_exceptions`, `get_exception_details` |
| `/home/software/Skyline/daily` | Beta release notes | Use support tools with container_path override |
| `/home/software/Skyline/releases` | Release email archives | Pre-MailChimp (through 2024) |
| `/home/software/Skyline/events/*` | Event registration | Many sub-folders for courses, user groups |
| `/home/software/Skyline/funding/*` | Funding appeals | NIH grants, vendor support |

## Querying Release Notes

### Skyline-daily (Beta) Release Notes

The support tools work with any announcement container by overriding `container_path`:

```python
# List recent daily releases
query_support_threads(container_path="/home/software/Skyline/daily", days=365)

# Get specific release notes
get_support_thread(thread_id=70880, container_path="/home/software/Skyline/daily")
```

Example output for Skyline-daily 25.1.1.147:
- AlphaPeptDeep spectral library prediction
- NCE optimization for Thermo instruments
- Bug fixes for MS Fragger, library m/z tolerance

### Finding Release from Git

To find which release first contained a commit:

```bash
# Find all releases containing a commit
git tag --contains <commit-hash> --sort=version:refname | head -5

# Example: Find first release with AlphaPeptDeep
git tag --contains 5828d20cc --sort=version:refname | head -1
# Returns: Skyline-daily-25.1.1.147
```

## Table Schema

| Column | Type | Description |
|--------|------|-------------|
| RowId | Integer | Primary key |
| EntityId | Text | GUID for attachments/replies |
| Parent | Text | EntityId of parent (null if top-level) |
| Title | Text | Post title |
| Body | Text | Raw content |
| FormattedBody | Text | HTML-rendered content |
| Created | DateTime | Creation timestamp |
| CreatedBy | Integer | User ID |
| RendererType | Text | HTML, MARKDOWN, RADEOX, TEXT_WITH_LINKS |

## Key Relationships

- **Threads**: Top-level posts have `Parent = null`
- **Replies**: Child posts have `Parent` set to parent's EntityId
- **Attachments**: Linked via `corex.documents` where `documents.parent = Announcement.EntityId`

## MailChimp Transition (2025)

Release announcement emails were stored in `/home/software/Skyline/releases` through 2024. Starting 2025, emails are sent via MailChimp to handle scale and spam filtering. Archives should still be copied to the releases container for historical record.

## Related Documentation

- **ai/docs/mcp/support.md** - Support board queries
- **ai/docs/mcp/exceptions.md** - Exception tracking
- **ai/docs/release-guide.md** - Release management overview
- **ai/mcp/LabKeyMcp/queries/announcement-usage.md** - Internal schema details
