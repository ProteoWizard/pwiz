---
name: skyline-wiki
description: Use when working with Skyline wiki documentation on skyline.ms. Activate for reading or updating wiki pages, listing or downloading wiki attachments, or questions about wiki page structure and content.
---

# Skyline Wiki Documentation

**Full documentation**: ai/docs/mcp/wiki.md

## When to Read What

- **Before querying wiki pages**: Read ai/docs/mcp/wiki.md (MCP Tools section)
- **For updating pages**: Read ai/docs/mcp/wiki.md (Update Workflow)
- **For attachments**: Read ai/docs/mcp/wiki.md (Attachment section)
- **For MCP server changes**: Read ai/mcp/LabKeyMcp/README.md

## Slash Commands

- `/pw-upconfig` - Sync developer config wiki pages with ai/docs source files

## Wiki-to-File Mappings

These wiki pages are synced with committed files in the repository:

| Wiki Page | Source File | Sync Pattern |
|-----------|-------------|--------------|
| AIDevSetup | ai/docs/developer-setup-guide.md | Body (wiki body = file content) |
| NewMachineBootstrap | ai/docs/new-machine-setup.md | Attachment |

**Source of truth**: The ai/docs files are canonical; wiki pages are updated from them.

## Quick Reference

**Data location**: skyline.ms → /home/software/Skyline → wiki.CurrentWikiVersions

**MCP tools available**:
- `list_wiki_pages()` - All pages with metadata
- `get_wiki_page(page_name)` - Full content to ai/.tmp/
- `update_wiki_page(page_name, new_body, title)` - Update content
- `list_wiki_attachments(page_name)` - List attached files
- `get_wiki_attachment(page_name, filename)` - Download attachment

**Renderer types**: HTML, MARKDOWN, RADEOX, TEXT_WITH_LINKS
