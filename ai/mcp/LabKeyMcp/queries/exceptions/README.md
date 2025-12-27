# Exception Queries

**Container:** `/home/issues/exceptions`

Exception tracking uses the shared `announcement.Announcement` table.

## Schema

See [../announcement/announcement-schema.md](../announcement/announcement-schema.md)

## Current Implementation

The exception MCP tools currently query the base `Announcement` table directly with client-side date filtering:

```python
EXCEPTION_SCHEMA = "announcement"
EXCEPTION_QUERY = "Announcement"
filter_array = [QueryFilter("Created", since_date, "dategte")]
```

## Recommended Improvement

Refactor to use the shared announcement queries (see [../announcement/](../announcement/)):
- `announcement_threads_recent` - Would work for listing recent exceptions
- `announcement_thread_posts` - Would work for exception details with EntityId

This would:
- Make exceptions consistent with support board implementation
- Enable attachment support for exceptions (if ever needed)
- Reduce code duplication in server.py

## MCP Tools

| Tool | Description |
|------|-------------|
| `query_exceptions(days, max_rows)` | Query recent exceptions |
| `get_exception_details(exception_id)` | Get full exception details |
