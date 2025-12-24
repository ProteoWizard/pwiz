# TODO: MCP Wiki Enhancements

## Branch Information
- **Branch**: `ai-context`
- **Created**: 2025-12-24
- **Status**: Active
- **Parent**: Spun off from TODO-20251221_mcp_wiki_support_board.md

## Objective

Complete wiki-related MCP features that were deferred from the initial implementation.

## Tasks

### Wiki Attachments (Blocked)

WikiVersions table doesn't expose EntityId needed for attachment lookups.

- [ ] Explore wiki schema for Pages table or similar with EntityId
- [ ] Request `corex` schema added to `/home/software/Skyline` container
- [ ] Update `get_wiki_page()` to extract EntityId if found

**Known EntityIds (from download URLs):**
- NewMachineBootstrap: `945040f8-bdf6-103e-9d4b-22f53556b982`
- UI Modes: `f22a0806-871e-1037-a1ed-e465a3935ecb`

### Wiki Write Testing

Agents group now has "Allow Iframes and Scripts" permission (as of 2025-12-24).

- [ ] Test updating tutorial_method_edit page (has iframe)
- [ ] Verify page renders correctly after update
- [ ] Test with non-HTML renderer (RADEOX page)

### Skills and Commands

- [ ] Create `skyline-wiki` skill for wiki documentation work

### Tutorial Maintenance (Future)

- [ ] Audit wiki page names vs HTML folder names for consistency
- [ ] Commit tutorial.js to Git at `pwiz_tools/Skyline/Documentation/tutorial.js`
- [ ] Create `/pw-tutorial-sync` command for Git/wiki coordination

### Schema Documentation (Minor)

- [ ] Paste handleleaks table schema from LabKey UI
- [ ] Paste user table schema from LabKey UI
- [ ] Paste Threads view schema from LabKey UI

## Related Documentation

- [Wiki and Support Board System](../../docs/wiki-support-system.md)
- [MCP Development Guide](../../docs/mcp-development-guide.md)
