---
description: Sync developer config wiki pages with ai/docs
---

# Update Developer Configuration Wiki Pages

Compare the wiki pages on skyline.ms with their source files in ai/docs and update if needed.

## Pages to Compare

| Wiki Page | Source File |
|-----------|-------------|
| AIDevSetup | ai/docs/developer-setup-guide.md |
| NewMachineBootstrap | ai/docs/new-machine-bootstrap.md |

## Instructions

1. Fetch both wiki pages using `get_wiki_page(page_name)`
2. Read the corresponding ai/docs files
3. Compare content (ignore minor whitespace/line ending differences)
4. Report which pages match and which need updating
5. If updates needed, ask user for confirmation before updating via `update_wiki_page()`

## Notes

- The ai/docs files are the source of truth
- Wiki pages use HTML renderer but content is Markdown
- Line ending differences (CRLF vs LF) are expected and not significant
