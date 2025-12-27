# Support Board Access

Access support board threads and user questions from skyline.ms via the LabKey MCP server.

## Data Location

| Property | Value |
|----------|-------|
| Server | `skyline.ms` |
| Container | `/home/support` |
| Schema | `announcement` |
| Tables | `Announcement`, `Threads` |

**Key columns in Announcement:**

| Column | Description |
|--------|-------------|
| `RowId` | Unique post identifier |
| `EntityId` | GUID for attachment lookups |
| `Title` | Post/thread title |
| `FormattedBody` | HTML-rendered content |
| `Parent` | EntityId of parent post (null for thread starters) |
| `Created` | When posted |
| `CreatedBy` | User who posted |

## MCP Tools

| Tool | Description |
|------|-------------|
| `query_support_threads(days, max_rows)` | Query recent threads with response counts |
| `get_support_thread(thread_id)` | Get full thread with all posts, save to `ai/.tmp/support-thread-{id}.md` |
| `get_support_summary(days)` | Generate activity report, save to `ai/.tmp/support-report-YYYYMMDD.md` |

### Attachment Tools

| Tool | Description |
|------|-------------|
| `list_attachments(parent_entity_id)` | List attachments for a post |
| `get_attachment(parent_entity_id, filename)` | Download attachment (text returned directly, binary saved to `ai/.tmp/attachments/`) |

## Usage Examples

**Query recent threads:**
```
query_support_threads(days=7)
```

**Get full thread with all replies:**
```
get_support_thread(73628)
```
Returns metadata and saves full thread to `ai/.tmp/support-thread-73628.md`.

**Generate daily activity report:**
```
get_support_summary(days=1)
```
Categorizes threads as unanswered (need response) vs active (has responses).

**List attachments on a support post:**
```
list_attachments("a1b2c3d4-5678-90ab-cdef-1234567890ab")
```

**Download an attachment:**
```
get_attachment("a1b2c3d4-5678-90ab-cdef-1234567890ab", "data_file.csv")
```
Text files are returned directly; binary files are saved to `ai/.tmp/attachments/`.

## Slash Commands

| Command | Description |
|---------|-------------|
| `/pw-support` | Generate support board activity report |

## Triage Workflow

1. **Generate summary**: `get_support_summary(days=1)` for daily review
2. **Identify unanswered**: Report shows threads needing response
3. **Read thread**: `get_support_thread(thread_id)` for full context
4. **Check attachments**: `list_attachments(entity_id)` if user uploaded files
5. **Download data**: `get_attachment(entity_id, filename)` to examine user data

## Server-Side Queries

| Query | Description |
|-------|-------------|
| `announcement_threads_recent` | Recent threads with response counts |
| `announcement_thread_posts` | All posts in a thread |
| `documents_metadata` | Attachments without binary column |

## Future Enhancements

- Support thread response posting
- Attachment upload capability
