# Skyline Wiki and Support Board System

This document describes the system for accessing wiki documentation and support board threads from skyline.ms using Claude Code.

## Overview

The skyline.ms LabKey server hosts two key content systems:

1. **Wiki pages** - Documentation, tutorials (26+), installation guides, release notes
2. **Support board** - User questions, discussions, and troubleshooting threads

Claude Code can read and (for simple pages) update this content via MCP tools to assist with documentation maintenance and support triage.

## Architecture

```
Claude Code
    │
    └── MCP Protocol (stdio)
            │
            └── LabKeyMcp Server (Python)
                    │
                    ├── labkey Python SDK (queries)
                    │       │
                    │       └── skyline.ms LabKey Server
                    │               ├── /home/software/Skyline (wiki)
                    │               │       └── wiki.CurrentWikiVersions (180+ pages)
                    │               └── /home/support (announcements)
                    │                       └── announcement.Announcement (threads)
                    │
                    └── HTTP requests (wiki updates, attachments)
                            │
                            └── wiki-saveWiki.view, announcements-download.view
```

## Data Locations

### Wiki Pages

| Property | Value |
|----------|-------|
| Server | `skyline.ms` |
| Container | `/home/software/Skyline` |
| Schema | `wiki` |
| Tables | `CurrentWikiVersions`, `AllWikiVersions` |

**Key columns:**

| Column | Description |
|--------|-------------|
| `Name` | Page identifier (e.g., `tutorial_method_edit`) |
| `Title` | Display title |
| `Body` | Page content (HTML or wiki markup) |
| `RendererType` | HTML, MARKDOWN, RADEOX, TEXT_WITH_LINKS |
| `Version` | Version number (integer) |
| `Modified` | Last modification timestamp |

**Tutorial naming convention:**
- English: `tutorial_<name>` (e.g., `tutorial_method_edit`)
- Japanese: `tutorial_<name>_ja`
- Chinese: `tutorial_<name>_zh`

### Support Board

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

### Attachments

Both wiki pages and support posts can have file attachments. These are stored in:

| Property | Value |
|----------|-------|
| Schema | `corex` |
| Query | `documents_metadata` |
| Key column | `parent` (EntityId of wiki page or announcement) |

**Note:** The `documents` table contains a binary `document` column up to 50MB. Always use `documents_metadata` (which excludes this column) to list attachments, then download via HTTP.

## Available MCP Tools

### Wiki Tools

| Tool | Description |
|------|-------------|
| `list_wiki_pages(container_path)` | List all pages with metadata (no body) |
| `get_wiki_page(page_name)` | Get full page content, save to `ai/.tmp/wiki-{name}.md` |
| `update_wiki_page(page_name, new_body)` | Update page content (simple HTML only) |

### Support Board Tools

| Tool | Description |
|------|-------------|
| `query_support_threads(days, max_rows)` | Query recent threads with response counts |
| `get_support_thread(thread_id)` | Get full thread with all posts, save to `ai/.tmp/support-thread-{id}.md` |
| `get_support_summary(days)` | Generate activity report, save to `ai/.tmp/support-report-YYYYMMDD.md` |

### Attachment Tools

| Tool | Description |
|------|-------------|
| `list_attachments(parent_entity_id)` | List attachments for a post or wiki page |
| `get_attachment(parent_entity_id, filename)` | Download attachment (text returned directly, binary saved to `ai/.tmp/attachments/`) |

## Usage Examples

### Wiki Access

**List all wiki pages:**
```
list_wiki_pages()
```

**Get a specific tutorial page:**
```
get_wiki_page("tutorial_method_edit")
```
Returns metadata and saves full content to `ai/.tmp/wiki-tutorial_method_edit.md`.

**Update a wiki page:**
```
update_wiki_page("AIDevSetup", "<html>New content here</html>")
```

> **Limitation:** The agent account cannot update pages containing `<iframe>` or `<script>` elements (tutorial wrapper pages). This is a LabKey permission restriction. Actual tutorial content lives in Git, not the wiki.

### Support Board Access

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

### Attachment Access

**List attachments on a support post:**
```
list_attachments("a1b2c3d4-5678-90ab-cdef-1234567890ab")
```

**Download an attachment:**
```
get_attachment("a1b2c3d4-5678-90ab-cdef-1234567890ab", "data_file.csv")
```
Text files are returned directly; binary files are saved to `ai/.tmp/attachments/`.

## Authentication and Permissions

The system uses a dedicated agent account:
- **Email**: `claude.c.skyline@gmail.com`
- **Group**: "Agents" on skyline.ms
- **Role**: "Editor without Delete" on `/home/software/Skyline`

**Permissions:**
- ✅ Read all wiki and announcement content
- ✅ Add new wiki pages
- ✅ Update existing simple HTML pages
- ❌ Delete pages or folders
- ❌ Update pages with `<iframe>` or `<script>` (security restriction)

## Slash Commands

| Command | Description |
|---------|-------------|
| `/pw-support` | Generate support board activity report |
| `/pw-upconfig` | Sync developer setup guide between wiki and ai/docs |

## Server-Side Queries

Custom queries are defined on the LabKey server to provide pre-filtered data:

| Query | Container | Description |
|-------|-----------|-------------|
| `wiki_page_list` | `/home/software/Skyline` | All pages without body content |
| `wiki_page_content` | `/home/software/Skyline` | Single page with full body (parameterized) |
| `announcement_threads_recent` | `/home/support` | Recent threads with response counts |
| `announcement_thread_posts` | `/home/support` | All posts in a thread |
| `documents_metadata` | `/home/support` | Attachments without binary column |

Query SQL files are documented in `ai/mcp/LabKeyMcp/queries/`.

## Wiki Update Workflow

For pages that can be edited (simple HTML without iframes/scripts):

1. **Read current content**: `get_wiki_page("PageName")` → saved to `ai/.tmp/wiki-PageName.md`
2. **Review and modify**: Edit the content as needed
3. **Update page**: `update_wiki_page("PageName", new_content)`
4. **Verify**: Check the live page at `https://skyline.ms/home/software/Skyline/wiki-page.view?name=PageName`

The wiki maintains full version history, so changes can be reverted if needed.

## Support Triage Workflow

1. **Generate summary**: `get_support_summary(days=1)` for daily review
2. **Identify unanswered**: Report shows threads needing response
3. **Read thread**: `get_support_thread(thread_id)` for full context
4. **Check attachments**: `list_attachments(entity_id)` if user uploaded files
5. **Download data**: `get_attachment(entity_id, filename)` to examine user data

## Future Enhancements

- Wiki attachment support (blocked on EntityId not exposed in WikiVersions table)
- Tutorial sync workflow (`/pw-tutorial-sync`) for coordinating Git and wiki updates
- Support thread response posting
- Attachment upload capability

## Related Documentation

- [MCP Development Guide](mcp-development-guide.md) - Patterns for extending MCP capabilities
- [Exception Triage System](exception-triage-system.md) - User-reported crash analysis
- [Nightly Test Analysis](nightly-test-analysis.md) - Test results data access
- [LabKey MCP Server README](../mcp/LabKeyMcp/README.md) - Setup instructions
- [Query Documentation](../mcp/LabKeyMcp/queries/README.md) - Server-side query reference
