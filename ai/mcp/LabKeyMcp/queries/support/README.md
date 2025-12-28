# Support Board Queries

**Container:** `/home/support`

The support board uses the shared `announcement.Announcement` table.

## Schema

See [../announcement/announcement-schema.md](../announcement/announcement-schema.md)

## Queries

### Shared Announcement Queries
See [../announcement/](../announcement/) - these work in any Announcement container:
- `announcement_threads_recent`
- `announcement_thread_posts`

### Attachments
See [../corex/](../corex/) for document/attachment queries (shared with wiki).

## MCP Tools

| Tool | Description |
|------|-------------|
| `query_support_threads(days, max_rows)` | Query recent threads |
| `get_support_thread(thread_id)` | Get all posts in a thread |
| `get_support_summary(days)` | Generate activity summary |
| `list_attachments(entity_id)` | List attachments for a post |
| `get_attachment(entity_id, filename)` | Download an attachment |
