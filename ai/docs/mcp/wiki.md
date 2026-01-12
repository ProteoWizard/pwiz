# Wiki Documentation Access

Access and update wiki pages on skyline.ms via the LabKey MCP server.

## Data Location

| Property | Value |
|----------|-------|
| Server | `skyline.ms` |
| Schema | `wiki` |
| Tables | `CurrentWikiVersions`, `AllWikiVersions` |

### Wiki Containers

Any LabKey folder can contain wiki pages. The two main repositories:

| Container | Access | Content |
|-----------|--------|---------|
| `/home/software/Skyline` | Public | Install pages, tutorials, release announcements |
| `/home/development` | Authenticated | `release-prep`, `DeployToDockerHub`, `NewMachineBootstrap`, dev tools |

Other wiki content locations:

| Container | Access | Content |
|-----------|--------|---------|
| `/home` | Public | Landing page for skyline.ms |
| `/home/software/Skyline/daily` | Semi-public (signup required) | Skyline-daily release announcements |
| `/home/software/Skyline/events/<event>` | Public | Event registration, course info |
| `/home/software/Skyline/events/<event>/participants` | Restricted (participants/instructors) | Photos, posters, contacts, surveys |

**Default container**: Most MCP tools default to `/home/software/Skyline`. Use `container_path` parameter for others:
```
get_wiki_page("DeployToDockerHub", container_path="/home/development")
```

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

## MCP Tools

| Tool | Description |
|------|-------------|
| `list_wiki_pages(container_path)` | List all pages with metadata (no body) |
| `get_wiki_page(page_name)` | Get full page content, save to `ai/.tmp/wiki-{name}.md` |
| `update_wiki_page(page_name, new_body, title)` | Update page content (optional title change) |
| `list_wiki_attachments(page_name)` | List attachments for a wiki page |
| `get_wiki_attachment(page_name, filename)` | Download attachment from wiki page |

## Usage Examples

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

> **Note:** Pages with `<iframe>` or `<script>` elements (tutorial wrappers) require "Allow Iframes and Scripts" permission, which the Agents group now has.

**List wiki page attachments:**
```
list_wiki_attachments("NewMachineBootstrap")
```

**Download a wiki attachment:**
```
get_wiki_attachment("NewMachineBootstrap", "new-machine-setup.md")
```
Text files are returned directly; binary files (PDF, images) are saved to `ai/.tmp/attachments/`.

## Permissions

- ✅ Read all wiki content
- ✅ Add new wiki pages
- ✅ Update existing simple HTML/Markdown pages
- ❌ Delete pages or folders
- ❌ Update pages with `<iframe>` or `<script>` (security restriction)

## Wiki-to-File Mappings

Some wiki pages are kept in sync with files committed to the repository. The ai/docs files are the **source of truth**:

| Wiki Page | Source File | Sync Pattern |
|-----------|-------------|--------------|
| AIDevSetup | ai/docs/developer-setup-guide.md | **Body** - wiki body content = file content |
| NewMachineBootstrap | ai/docs/new-machine-setup.md | **Attachment** - file attached to wiki page |

**Update workflow:**
1. Edit the ai/docs source file
2. Commit to repository
3. Run `/pw-upconfig` to sync wiki pages

## Update Workflow

For pages that can be edited (simple HTML without iframes/scripts):

1. **Read current content**: `get_wiki_page("PageName")` → saved to `ai/.tmp/wiki-PageName.md`
2. **Review and modify**: Edit the content as needed
3. **Update page**: `update_wiki_page("PageName", new_content)`
4. **Verify**: Check the live page at `https://skyline.ms/home/software/Skyline/wiki-page.view?name=PageName`

The wiki maintains full version history, so changes can be reverted if needed.

## Server-Side Queries

| Query | Description |
|-------|-------------|
| `wiki_page_list` | All pages without body content |
| `wiki_page_content` | Single page with full body (parameterized) |

## Future Enhancements

- Tutorial sync workflow (`/pw-tutorial-sync`) for coordinating Git and wiki updates
- Attachment upload capability
