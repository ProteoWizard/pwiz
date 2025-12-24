# WikiVersions Table Schema

**Container:** `/home/software/Skyline`
**Schema:** `wiki`
**Tables:** `CurrentWikiVersions`, `AllWikiVersions`

Both tables share the same schema. `CurrentWikiVersions` has one row per page (latest version), `AllWikiVersions` has full version history.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| RowId | Integer | | PK, Req, RO | Primary key |
| Container | Text | core.Containers.EntityId | RO | Folder reference |
| Name | Text | | | Page identifier (e.g., `tutorial_method_edit`) |
| Path | Text | | | Full path string |
| PathParts | Text | | | Path as array |
| Depth | Integer | | | Nesting depth |
| Title | Text | | | Display title |
| Version | Integer | | | Version number |
| RendererType | Text | | | HTML, MARKDOWN, RADEOX, TEXT_WITH_LINKS |
| CreatedBy | Integer | core.SiteUsers.UserId | RO | User who created |
| Created | DateTime | | | Creation timestamp |
| ModifiedBy | Integer | core.SiteUsers.UserId | RO | User who last modified |
| Modified | DateTime | | RO, V | Last modification timestamp |
| Body | Text | | | **⚠️ LARGE** - Page content (HTML or markup), use `get_wiki_page()` |

## Notes

- `CurrentWikiVersions` - One row per page, latest version only
- `AllWikiVersions` - Full version history, multiple rows per page
- Body contains HTML or wiki markup depending on RendererType
- **EntityId is NOT in these tables** - Wiki pages DO have EntityId (visible in download URLs) but it's not exposed here. Needed for attachment lookups.
