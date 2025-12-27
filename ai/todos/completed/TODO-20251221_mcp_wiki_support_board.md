# TODO-20251221_mcp_wiki_support_board.md

## Branch Information
- **Branch**: `ai-context`
- **Base**: `ai-context`
- **Created**: 2025-12-21
- **Status**: 🚧 In Progress
- **PR**: (pending)
- **Objective**: Add wiki read/write and support board access to the LabKey MCP server

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

### Wiki Write Implementation Challenges

1. **No wiki-getWikiPage.api endpoint**: Had to parse `wiki-edit.view` HTML instead
2. **JS object format in edit page**: Uses single quotes (`entityId: 'guid'`), not JSON double quotes
3. **Session reuse critical**: CSRF token is tied to session; must reuse same session for metadata fetch and save
4. **WAF encoding required**: Body content must be URL-encoded, then Base64-encoded, with WAF hint prefix

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

**Important:** Permissions must be set at the specific folder level (`/home/software/Skyline`), not just at project root. Project root permissions don't inherit to wiki containers.

**Content Security Limitation:** The Agents group does **not** have "Allow Iframes and Scripts" permission. This means:
- Can edit simple HTML wiki pages (like AIDevSetup) ✅
- Cannot edit tutorial wrapper pages that use `<iframe>` or `<script>` elements ❌
- Tutorial content (actual HTML files) lives in Git, not wiki - only wrapper pages are affected

## Tasks

### Phase 1: Read-Only Access ✅

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
- [x] Document wiki/support tools in ai/docs/ → `wiki-support-system.md`

### Phase 1.5: Attachments ✅ (support board) / 🚧 (wiki)

Support board posts and wiki pages can have file attachments.

**Solution:** Brendan created external schema `corex` exposing `core.documents` table.

**Server-side queries created:**
- [x] `documents_metadata` (corex schema) - Lists attachments without binary column
- [x] `support_thread_posts` expanded to include EntityId

**MCP tools implemented:**
- [x] `list_attachments(parent_entity_id)` - Lists attachments for a post/page
- [x] `get_attachment(parent_entity_id, filename)` - Downloads via HTTP URL
  - Text files (.bat, .py, .txt, etc.) → returns content directly
  - Binary files (.png, .sky, etc.) → saves to `ai/.tmp/attachments/`

**Support board attachments: ✅ Working**
- Tested on thread 73628 - successfully downloaded `launch_skyline.bat`
- EntityId available via `get_support_thread()` output

**Wiki attachments: 🚧 Blocked**
- WikiVersions table does NOT have EntityId column
- Wiki pages DO have EntityId (seen in download URLs like `wiki-download.view?entityId=...`)
- Need to find source table that has wiki page EntityId
- Also need to add `corex` schema to `/home/software/Skyline` container

**Wiki attachment download URLs observed:**
```
NewMachineBootstrap: entityId=945040f8-bdf6-103e-9d4b-22f53556b982
UI Modes page:       entityId=f22a0806-871e-1037-a1ed-e465a3935ecb
```

**Next steps for wiki attachments:**
- [ ] Explore wiki schema for Pages table or similar with EntityId
- [ ] Add `corex` schema to `/home/software/Skyline` container
- [ ] Update `get_wiki_page()` to extract EntityId if found

**corex.documents Schema (reference):**
| Column | Type | Description |
|--------|------|-------------|
| rowid | Integer | Primary key |
| parent | Text | **EntityId of parent object** |
| documentname | Text | **Filename** |
| documentsize | Integer | File size in bytes |
| documenttype | Text | MIME type |
| document | Other | **Binary content (up to 50MB) - NEVER query directly** |

### Phase 2: Wiki Write Access ✅ (with limitations)

**Endpoints discovered via Chrome DevTools:**
- Save wiki: `wiki-saveWiki.view` (POST)
- Attach files: `wiki-attachFiles.api`
- Get TOC: `wiki-getWikiToc.api` (GET)

**CSRF Token Pattern (from ReportErrorDlg.cs:313-342):**
```python
# 1. GET request to establish session and get CSRF token
session = LabKeySession(server, login, password)
session._establish_session()  # GET /project/home/begin.view? for CSRF cookie

# 2. Reuse same session for all requests (critical for CSRF to work)
metadata, session = _get_wiki_page_metadata(page_name, server, container, session)
session.post_json(save_url, payload)  # CSRF token added automatically
```

**RendererType values (4 types):**
- `HTML` - Standard HTML markup
- `MARKDOWN` - Markdown syntax
- `RADEOX` - LabKey Wiki syntax (legacy)
- `TEXT_WITH_LINKS` - Plain text with auto-linked URLs

**WAF (Web Application Firewall) Body Encoding:**
```python
def _encode_waf_body(content: str) -> str:
    # 1. URL-encode the content
    url_encoded = quote(content, safe="")
    # 2. Base64 encode
    base64_encoded = base64.b64encode(url_encoded.encode()).decode()
    # 3. Prepend WAF hint
    return f"/*{{{{base64/x-www-form-urlencoded/wafText}}}}*/{base64_encoded}"
```

**Implementation complete in server.py:**
- [x] `LabKeySession` class - urllib-based session with cookie jar and CSRF support
- [x] `_get_labkey_session()` - Create authenticated session
- [x] `_encode_waf_body()` / `_decode_waf_body()` - WAF encoding
- [x] `_get_wiki_page_metadata()` - Fetch edit page, parse entityId/rowId/pageVersionId
- [x] `update_wiki_page()` tool - Full update workflow

**Testing:**
- [x] Test wiki update on AIDevSetup page (added/verified `<!-- test -->` comment)
- [ ] Test by removing `<!-- test -->` comment from tutorial_method_edit page (blocked by iframe/script restriction)
- [ ] Verify page renders correctly after update
- [ ] Test with non-HTML renderer (RADEOX page)

**Status:** Wiki write works for simple HTML pages. Tutorial wrapper pages require "Allow Iframes and Scripts" permission or human review workflow.

**Reference files:**
- `ai/.tmp/wiki-save-capture.md` - Full network capture details
- `ai/.tmp/WikiSaveHttpRequests.txt` - Raw HTTP request/response headers

### Phase 3: Commands and Skills ✅ (Partial)
- [x] Create `/pw-exceptions` command for exception triage workflow
- [x] Create `/pw-upconfig` command to sync developer config wiki pages with ai/docs
- [x] Remove obsolete `/pw-confightml` command (replaced by /pw-upconfig)
- [ ] Create `skyline-wiki` skill for wiki documentation work

### Phase 3.1: Tutorial Maintenance (Future)
- [ ] Audit wiki page names vs HTML folder names for consistency
- [ ] Consider standardizing naming convention
- [ ] Commit tutorial.js to Git at `pwiz_tools/Skyline/Documentation/tutorial.js`
- [ ] Create `/pw-tutorial-sync` command for coordinating Git and wiki updates

### Phase 3.5: MCP Server Relocation ✅

Moved LabKeyMcp from `pwiz_tools/Skyline/Executables/DevTools/` to `ai/mcp/` to align with ai-context branch strategy.

**Changes made:**
- [x] Move entire LabKeyMcp directory to `ai/mcp/LabKeyMcp/`
- [x] Create `ai/mcp/README.md` explaining the MCP servers directory
- [x] Update path calculations in server.py (5 → 3 parent levels)
- [x] Update all documentation references to new path
- [x] Update `.claude/skills/skyline-exceptions/SKILL.md`
- [x] Fix `Verify-Environment.ps1` to detect wrong MCP server path
- [x] Re-register MCP server with Claude Code
- [x] Rename `support_*` queries to `announcement_*` in server.py (server-side already renamed)

### Phase 3.6: Query Documentation ✅ (Partial)

Created `queries/` directory alongside the MCP server to document all server-side LabKey queries.

**Directory structure created:**
```
ai/mcp/LabKeyMcp/queries/
├── README.md                       # Main index of all queries
├── announcement-usage.md           # How Announcement table is used across skyline.ms
├── announcement/                   # Shared Announcement table (used by multiple containers)
│   ├── announcement-schema.md      # ✅ Formatted
│   ├── announcement_threads_recent.sql
│   ├── announcement_thread_posts.sql
│   └── threads-schema.md           # ⏳ Needs paste
├── nightly/                        # Test result queries
│   ├── testruns-schema.md          # ✅ Formatted
│   ├── testpasses-schema.md        # ✅ Formatted
│   ├── testfails-schema.md         # ✅ Formatted
│   ├── handleleaks-schema.md       # ⏳ Needs paste
│   ├── user-schema.md              # ⏳ Needs paste
│   ├── testruns_detail.sql
│   ├── testpasses_detail.sql
│   ├── handleleaks_by_computer.sql
│   ├── testfails_by_computer.sql
│   └── compare_run_timings.sql     # ✅ Working - compares test timings between runs
├── support/
│   ├── documents_metadata.sql
│   └── documents-schema.md         # ✅ Pre-filled
├── wiki/
│   ├── wiki_page_content.sql
│   ├── wiki_page_list.sql
│   └── wikiversions-schema.md      # ✅ Formatted
└── exceptions/
    └── README.md                   # Explains exceptions use base Announcement table
```

**Key insights documented:**
- `announcement.Announcement` is a shared table used by: support board, exceptions, daily releases, events, funding, release announcements
- Current `support_*` query naming is a misnomer - should be renamed to `announcement_*`
- `testpasses` table has 700M+ rows - always filter by RunId
- `documents` table has binary blob up to 50MB - never query directly, use `documents_metadata`
- WikiVersions tables don't expose EntityId (needed for attachment lookups)

**Pending work:**
- [ ] Paste handleleaks table schema from LabKey UI
- [ ] Paste user table schema from LabKey UI
- [ ] Paste Threads view schema from LabKey UI
- [x] Fix `compare_run_timings.sql` errors on server - working with pass counts and total time impact
- [x] Rename `support_threads_recent` → `announcement_threads_recent` on server (done in prior commit)
- [x] Rename `support_thread_posts` → `announcement_thread_posts` on server (done in prior commit)

### Phase 3.7: HTTP-Based Data Access ✅

Fixed tools that need to access large blob columns (log, xml) by using HTTP endpoints instead of direct database queries.

**Tools updated:**
- [x] `save_run_log()` - Now uses `testresults-viewLog.view` HTTP endpoint
- [x] `save_run_xml()` - NEW - Uses `testresults-viewXml.view` HTTP endpoint

**Schema annotations added:**
- [x] Marked large columns with **⚠️ LARGE** in schema documentation
- [x] Added troubleshooting section to mcp-development-guide.md
- [x] Documented schema conventions for LARGE column warnings

### Phase 4: Code Refactoring ✅

The `server.py` file was ~2400 lines and has been split into modules.

**Final structure:**
```
LabKeyMcp/
├── server.py           # 52 lines - Main entry, FastMCP init, imports tools
├── tools/
│   ├── __init__.py     # register_all_tools() function
│   ├── common.py       # 4 tools: list_schemas, list_queries, list_containers, query_table
│   ├── exceptions.py   # 2 tools: query_exceptions, get_exception_details
│   ├── nightly.py      # 7 tools: query_test_runs, get_run_failures, etc.
│   ├── wiki.py         # 3 tools: list_wiki_pages, get_wiki_page, update_wiki_page
│   ├── support.py      # 3 tools: query_support_threads, get_support_thread, etc.
│   └── attachments.py  # 2 tools: list_attachments, get_attachment
├── queries/            # Server-side query documentation
└── test_connection.py
```

**Total: 21 tools registered across 6 modules.**

**Tasks:**
- [x] Create `tools/` directory structure
- [x] Extract common utilities to `tools/common.py`
- [x] Move exception tools to `tools/exceptions.py`
- [x] Move nightly test tools to `tools/nightly.py`
- [x] Move wiki tools to `tools/wiki.py`
- [x] Move support tools to `tools/support.py`
- [x] Move attachment tools to `tools/attachments.py`
- [x] Update `server.py` to import and register tools
- [x] Verify all tools still work after refactor (21 tools registered)

## Files Modified

- `ai/mcp/LabKeyMcp/server.py` - Refactored to 52 lines (was ~2400), imports from tools/
- `ai/mcp/LabKeyMcp/tools/__init__.py` - NEW - Package with register_all_tools()
- `ai/mcp/LabKeyMcp/tools/common.py` - NEW - Shared utilities and discovery tools
- `ai/mcp/LabKeyMcp/tools/exceptions.py` - NEW - Exception triage tools
- `ai/mcp/LabKeyMcp/tools/nightly.py` - NEW - Nightly test analysis tools
- `ai/mcp/LabKeyMcp/tools/wiki.py` - NEW - Wiki page tools
- `ai/mcp/LabKeyMcp/tools/support.py` - NEW - Support board tools
- `ai/mcp/LabKeyMcp/tools/attachments.py` - NEW - Attachment tools
- `ai/docs/wiki-support-system.md` - NEW - Wiki and support board documentation
- `ai/mcp/README.md` - NEW - MCP servers directory overview
- `ai/docs/mcp-development-guide.md` - Updated paths
- `ai/docs/developer-setup-guide.md` - Updated MCP server path
- `ai/docs/exception-triage-system.md` - Updated paths
- `ai/docs/nightly-test-analysis.md` - Updated paths
- `ai/scripts/Verify-Environment.ps1` - Enhanced MCP path detection
- `.claude/commands/pw-exceptions.md` - NEW - Exception report command
- `.claude/commands/pw-upconfig.md` - NEW - Wiki sync command (replaces pw-confightml)
- `.claude/skills/skyline-exceptions/SKILL.md` - Updated paths

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
