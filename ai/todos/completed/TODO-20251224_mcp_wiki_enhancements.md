# TODO: MCP Wiki Enhancements

## Branch Information
- **Branch**: `ai-context`
- **Created**: 2025-12-24
- **Status**: Complete
- **Parent**: Spun off from TODO-20251221_mcp_wiki_support_board.md

## Objective

Complete wiki-related MCP features that were deferred from the initial implementation.

## Tasks

### Wiki Attachments (Complete)

- [x] Created `list_wiki_attachments(page_name)` tool - gets entityId from HTML, queries corex.documents_metadata
- [x] Created `get_wiki_attachment(page_name, filename)` tool - downloads text or binary files
- [x] Added URL encoding for page names and filenames with spaces
- [x] Tested with NewMachineBootstrap (text file: new-machine-setup.md)
- [x] Tested with "UI Modes" (binary file: Skyline UI Modes.pdf)
- [x] Verified entityIds match known values

**Verified EntityIds:**
- NewMachineBootstrap: `945040f8-bdf6-103e-9d4b-22f53556b982` ✓
- UI Modes: `f22a0806-871e-1037-a1ed-e465a3935ecb` ✓

### Wiki Write Testing

Agents group now has "Allow Iframes and Scripts" permission (as of 2025-12-24).

- [x] Fixed title preservation bug in `update_wiki_page()` - now queries database for title/rendererType
- [x] Simplified HTML parsing - direct regex for entityId/rowId/pageVersionId (no object parsing)
- [x] Restored tutorial_method_edit title to "Targeted Method Editing"
- [x] Restored AIDevSetup title to "Developer Environment Setup for LLM-Assisted IDEs"
- [x] Verified title preservation on body-only updates (no title param)
- [x] Tested updating tutorial_method_edit page (has iframe) - works correctly
- [x] Tested AIDevSetup page (MARKDOWN renderer) - works correctly

### Skills and Commands (Complete)

- [x] Created `skyline-wiki` skill at `.claude/skills/skyline-wiki/SKILL.md`
- [x] Updated `ai/docs/wiki-support-system.md` with new attachment tools

## Deferred to Future Sprints

### Tutorial Maintenance

- Audit wiki page names vs HTML folder names for consistency
- Commit tutorial.js to Git at `pwiz_tools/Skyline/Documentation/tutorial.js`
- Create `/pw-tutorial-sync` command for Git/wiki coordination

## Related Documentation

- [Wiki and Support Board System](../../docs/wiki-support-system.md)
- [MCP Development Guide](../../docs/mcp-development-guide.md)
