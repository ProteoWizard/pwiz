---
name: skyline-wiki
description: Use when working with Skyline wiki documentation on skyline.ms. Activate for reading or updating wiki pages, listing or downloading wiki attachments, or questions about wiki page structure and content.
---

# Skyline Wiki Documentation

**Full documentation**: ai/docs/wiki-support-system.md

## MCP Tools (Quick Reference)

| Tool | Purpose |
|------|---------|
| `list_wiki_pages()` | List all 180+ wiki pages with metadata |
| `get_wiki_page(page_name)` | Get full page content to ai/.tmp/ |
| `update_wiki_page(page_name, new_body, title)` | Update page content |
| `list_wiki_attachments(page_name)` | List files attached to a page |
| `get_wiki_attachment(page_name, filename)` | Download attachment |

## Data Location

| Property | Value |
|----------|-------|
| Server | skyline.ms |
| Container | /home/software/Skyline |
| Schema | wiki |
| Table | CurrentWikiVersions |

## Common Page Types

- **Tutorials**: `tutorial_<name>` (26+ tutorials, 3 languages each)
- **Developer docs**: `HowToBuildSkylineTip`, `NewMachineBootstrap`, `AIDevSetup`
- **Release notes**: Named by version

## Typical Workflows

**Read a page:**
```
get_wiki_page("tutorial_method_edit")
# Content saved to ai/.tmp/wiki-tutorial_method_edit.md
```

**Update a page (body only):**
```
update_wiki_page("AIDevSetup", new_content)
# Title preserved automatically
```

**Update a page (with title change):**
```
update_wiki_page("AIDevSetup", new_content, "New Title Here")
```

**Get page attachments:**
```
list_wiki_attachments("NewMachineBootstrap")
get_wiki_attachment("NewMachineBootstrap", "new-machine-setup.md")
```

## Renderer Types

| Type | Description |
|------|-------------|
| HTML | Raw HTML content |
| MARKDOWN | Markdown syntax |
| RADEOX | LabKey wiki syntax |
| TEXT_WITH_LINKS | Plain text with auto-links |

## Permissions

The agent account (Agents group) can:
- Read all wiki pages
- Update pages including those with iframes/scripts
- Read and download attachments

For full documentation, see **ai/docs/wiki-support-system.md**.
