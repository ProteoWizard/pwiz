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
- [ ] Verify Body field is accessible in wiki queries
- [ ] Create `get_wiki_page(container_path, page_name)` tool
- [ ] Create `list_wiki_pages(container_path, path_filter)` tool
- [ ] Create `query_support_threads(days, max_rows)` tool
- [ ] Create `get_support_thread(thread_id)` tool
- [ ] Test read access to tutorial wiki pages
- [ ] Test read access to support board posts
- [ ] Document new tools in ai/docs/

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

## API Notes

The LabKey Python SDK (`labkey` package) handles data queries well. Wiki read/write operations may require:
1. Direct HTTP API calls to wiki controller endpoints
2. Or discovering if SDK has wiki support we haven't found yet

**LabKey Wiki API endpoints to investigate:**
- `GET wiki/<container>/getWikiPage.api?name=<pageName>`
- `POST wiki/<container>/saveWiki.api`

## Related Documentation

- [MCP Development Guide](../docs/mcp-development-guide.md)
- [Exception Triage System](../docs/exception-triage-system.md)
- [Nightly Test Analysis](../docs/nightly-test-analysis.md)
- [LabKey MCP Server README](../../pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/README.md)
