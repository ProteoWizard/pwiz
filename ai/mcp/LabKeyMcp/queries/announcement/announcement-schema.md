# Announcement Table Schema

**Schema:** `announcement`
**Table:** `Announcement`

This table exists in multiple containers across skyline.ms. See [announcement-usage.md](../announcement-usage.md) for the full list.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| RowId | Integer | | AI, PK, Req, RO | Primary key |
| EntityId | Text | | Req | GUID that uniquely identifies this row |
| CreatedBy | Integer | core.SiteUsers.UserId | RO | User who created |
| Created | DateTime | | RO | Creation timestamp |
| ModifiedBy | Integer | core.SiteUsers.UserId | RO | User who last modified |
| Modified | DateTime | | RO, V | Last modification timestamp |
| Parent | Text | announcement.Announcement.EntityId | | EntityId of parent (null if top-level) |
| Title | Text | | | Post title |
| Expires | DateTime | | | Expiration date |
| Body | Text | | | Raw body content |
| RendererType | Text | wiki.RendererType.Value | | HTML, MARKDOWN, RADEOX, TEXT_WITH_LINKS |
| Status | Text | | | Status field (usage varies by container) |
| AssignedTo | Integer | core.SiteUsers.UserId | | Assigned user |
| DiscussionSrcIdentifier | Text | | | EntityId of attached object |
| DiscussionSrcURL | Text | | | URL to attached object |
| LastIndexed | DateTime | | RO | Full-text search index timestamp |
| DiscussionSrcEntityType | Text | | | Entity type of discussion source |
| Folder | Text | core.Containers.EntityId | Req | Container reference |
| FormattedBody | Text | | | HTML-rendered body content |

## Key Relationships

- **Parent** → Self-reference for thread replies (EntityId of parent post)
- **EntityId** → Used by `corex.documents` for attachments (documents.parent = Announcement.EntityId)

## Notes

- Top-level posts have `Parent = null`
- Replies have `Parent` set to the top-level post's EntityId
- Use `FormattedBody` for display, `Body` for raw content
- `EntityId` is critical for attachment lookups
