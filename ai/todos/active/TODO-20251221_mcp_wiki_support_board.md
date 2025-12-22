# TODO-20251221_mcp_wiki_support_board.md

## Branch Information
- **Branch**: `ai-context`
- **Base**: `ai-context`
- **Created**: 2025-12-21
- **Status**: 🚧 In Progress
- **PR**: (pending)
- **Objective**: Add read-only wiki and support board access to the LabKey MCP server

## Background

The skyline.ms LabKey server hosts:
1. **Wiki pages** - Documentation, tutorials (26+), tips, installation guides
2. **Support board** - User questions and discussions

Claude Code should be able to read and eventually edit these to help maintain documentation, especially tutorials which exist in 3 languages (en, ja, zh-CHS).

## What We Learned

### Wiki Schema (`/home/software/Skyline`)

**Tables:**
- `CurrentWikiVersions` - Current state of all wiki pages
- `AllWikiVersions` - Full version history
- `RendererType` - Available formats: HTML, MARKDOWN, RADEOX (LabKey wiki markup), TEXT_WITH_LINKS

**Key Columns in WikiVersions:**
| Column | Description |
|--------|-------------|
| Name | Wiki page identifier (e.g., `tutorial_method_edit`) |
| Title | Display title |
| Path | Folder hierarchy as array (e.g., `['tutorials', 'tutorials-intro', 'tutorial_method_edit']`) |
| Body | Page content (HTML or wiki markup) - **needs verification** |
| RendererType | HTML, MARKDOWN, RADEOX, TEXT_WITH_LINKS |
| Version | Version number (integer) |
| Created/Modified | Timestamps |
| CreatedBy/ModifiedBy | User IDs |

**Statistics:**
- 180 wiki pages in `/home/software/Skyline`
- Tutorial pages follow naming: `tutorial_<name>`, `tutorial_<name>_ja`, `tutorial_<name>_zh`
- Example: `tutorial_method_edit` has 69 versions (since 2011)

### Support Board Schema (`/home/support`)

**Tables:**
- `Announcement` - Individual posts with Body content
- `Threads` - Thread summaries
- `AnnouncementByDate` - Posts organized by date
- `ResponseCounts` - Response statistics

### Tutorial Architecture

```
Git Repository (source of truth)
pwiz_tools/Skyline/Documentation/
├── Tutorials/
│   ├── en/MethodEdit/index.html + *.png
│   ├── ja/MethodEdit/index.html + *.png
│   └── zh-CHS/MethodEdit/index.html + *.png
├── shared/
│   ├── SkylineStyles.css
│   └── skyline.js
└── tutorial.js (proposed to add)

Deployment to skyline.ms (Apache)
/tutorials/<version>/
├── MethodEdit/en/, ja/, zh-CHS/
├── shared/SkylineStyles.css, skyline.js
└── tutorial.js (at /tutorials/tutorial.js)

LabKey Wiki
tutorial_method_edit wiki page
└── <iframe src="/tutorials/25-1/MethodEdit/en/">
```

**tutorial.js key variables:**
```javascript
var version = '25-1';    // Current release
var altVersion = '';     // Beta version (empty = disabled)
```

### Permissions

Agent account (`claude.c.skyline@gmail.com`) has "Editor without Delete" role:
- Can read all wiki and announcement content
- Can add new pages
- Can update existing pages
- Cannot delete pages or folders (preserves history)

## Tasks

### Phase 1: Read-Only Access (Current)

**Server-side queries (user creates on skyline.ms):**
- [x] Create `wiki_page_content` query in wiki schema (see SQL above)
- [x] Create `wiki_page_list` query in wiki schema
- [x] Create `support_threads_recent` query in announcement schema
- [x] Create `support_thread_posts` query in announcement schema

**MCP server tools (Claude Code implements):**
- [x] Add `get_wiki_page()` tool to server.py
- [x] Add `list_wiki_pages()` tool to server.py
- [x] Add `query_support_threads()` tool to server.py
- [x] Add `get_support_thread()` tool to server.py
- [x] Test read access to tutorial wiki pages
- [x] Test read access to support board posts

**Documentation:**
- [ ] Document wiki/support tools in ai/docs/

### Phase 1.5: Support Board Attachments (Future)

Support board posts can include file attachments (e.g., batch files, screenshots, Skyline documents).
Currently these are not accessible via MCP, which limits diagnostic capability.

**Known URL pattern:**
```
https://skyline.ms/home/support/announcements-download.view?entityId={entityId}&name={filename}
```
Example: `entityId=2f518904-be28-103e-9d4b-22f53556b982&name=launch_skyline.bat`

**Investigation needed:**
- [ ] Find table/API that lists attachments for an announcement (entityId is per-attachment, not per-post)
- [ ] Check with LabKey team about attachment metadata API
- [ ] Wiki pages also have attachments - may be a shared mechanism in core schema

**Status:** Brendan to ask LabKey about attachment discovery API.

**Implementation:**
- [ ] Create `list_post_attachments(post_id)` tool - returns filenames and sizes
- [ ] Create `get_post_attachment(post_id, filename)` tool - saves to `ai/.tmp/`
- [ ] For text files (.bat, .py, .txt, .csv), display content directly
- [ ] For images (.png, .jpg), save and return path for viewing
- [ ] For Skyline files (.sky, .skyd), save and return path

**Example use case:**
Thread 73628 had a batch file attachment that revealed the user was confusing
Skyline.exe (GUI) and SkylineCmd.exe (CLI). Seeing the attachment enabled a
much more helpful response.

### Phase 2: Write Access (Future)
- [ ] Create `update_wiki_page(container_path, page_name, content, comment)` tool
- [ ] Create `create_wiki_page(container_path, page_name, title, content)` tool
- [ ] Add confirmation/preview mechanism for edits
- [ ] Test with non-critical wiki page first
- [ ] Document edit workflow

### Phase 3: Tutorial Maintenance (Future)
- [ ] Audit wiki page names vs HTML folder names for consistency
- [ ] Consider standardizing naming convention
- [ ] Commit tutorial.js to Git at `pwiz_tools/Skyline/Documentation/tutorial.js`
- [ ] Create `/pw-tutorial-sync` command for coordinating Git and wiki updates

## Files to Modify

- `pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/server.py` - Add wiki/support tools
- `ai/docs/mcp-development-guide.md` - Document new patterns
- `ai/docs/exception-triage-system.md` - May need renaming to broader scope
- `pwiz_tools/Skyline/Documentation/tutorial.js` - Commit to Git (new file)

## Implementation Approach

Following the pattern in [mcp-development-guide.md](../docs/mcp-development-guide.md), we'll use **server-side custom queries** rather than implementing complex logic in Python.

### Required Server-Side Queries

**Wiki queries (container: `/home/software/Skyline`, schema: `wiki`):**

1. **`wiki_page_content`** - Parameterized query for full page content
```sql
PARAMETERS (PageName VARCHAR)

SELECT
    Name,
    Title,
    Body,
    RendererType,
    Version,
    Created,
    Modified,
    CreatedBy,
    ModifiedBy
FROM CurrentWikiVersions
WHERE Name = PageName
```

2. **`wiki_page_list`** - List all pages (no Body to keep small)
```sql
SELECT
    Name,
    Title,
    RendererType,
    Version,
    Modified
FROM CurrentWikiVersions
ORDER BY Name
```

**Support board queries (container: `/home/support`, schema: `announcement`):**

3. **`support_threads_recent`** - Recent threads with metadata (no Body - that's in Announcement)
```sql
PARAMETERS (DaysBack INTEGER DEFAULT 30)

SELECT
    RowId,
    Title,
    Created,
    CreatedBy,
    ResponseCount
FROM Threads
WHERE Created > TIMESTAMPADD('SQL_TSI_DAY', -DaysBack, NOW())
ORDER BY Created DESC
```

4. **`support_thread_posts`** - All posts in a thread (uses FormattedBody for HTML content)
```sql
PARAMETERS (ThreadId INTEGER)

SELECT
    a.RowId,
    a.Title,
    a.FormattedBody,
    a.Created,
    a.CreatedBy
FROM Announcement a
WHERE a.RowId = ThreadId
   OR a.Parent.RowId = ThreadId
ORDER BY a.Created
```

### MCP Server Tools to Add

Once server-side queries exist, add thin wrappers in `server.py`:

```python
@mcp.tool()
async def get_wiki_page(page_name: str, container_path: str = "/home/software/Skyline"):
    """Get full wiki page content by name."""
    return await query_table("wiki", "wiki_page_content",
                            container_path=container_path,
                            param_name="PageName", param_value=page_name)

@mcp.tool()
async def list_wiki_pages(container_path: str = "/home/software/Skyline"):
    """List all wiki pages with metadata (no body content)."""
    return await query_table("wiki", "wiki_page_list",
                            container_path=container_path, max_rows=500)

@mcp.tool()
async def query_support_threads(days: int = 30, max_rows: int = 50):
    """Query recent support board threads."""
    return await query_table("announcements", "support_threads_recent",
                            container_path="/home/support",
                            param_name="DaysBack", param_value=str(days),
                            max_rows=max_rows)

@mcp.tool()
async def get_support_thread(thread_id: int):
    """Get all posts in a support thread."""
    # Saves to ai/.tmp/support-thread-{id}.md

@mcp.tool()
async def get_support_summary(days: int = 1):
    """Generate support board activity summary."""
    # Saves to ai/.tmp/support-report-YYYYMMDD.md
```

### Slash Command

- `/pw-support` - Generate support board activity report (uses `get_support_summary`)

### Alternative: Direct Wiki API

For write operations (Phase 2), the LabKey Python SDK may not support wiki editing. May need direct HTTP calls:
- `GET wiki/<container>/getWikiPage.api?name=<pageName>`
- `POST wiki/<container>/saveWiki.api`

## Related Documentation

- [MCP Development Guide](../docs/mcp-development-guide.md)
- [Exception Triage System](../docs/exception-triage-system.md)
- [Nightly Test Analysis](../docs/nightly-test-analysis.md)
- [LabKey MCP Server README](../../pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/README.md)
