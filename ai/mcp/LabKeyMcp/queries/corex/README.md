# corex Schema Queries

**Schema:** `corex`

The `corex` schema is an external schema exposing `core.Documents` for API access. It's available in containers where it has been explicitly configured.

## Availability

| Container | Status |
|-----------|--------|
| `/home/support` | ✅ Available |
| `/home/software/Skyline` | ⏳ Pending - needed for wiki attachments |

## Tables

| Table | Description | Schema File |
|-------|-------------|-------------|
| Documents | File attachments (binary storage) | documents-schema.md |

## Queries

| Query | Description | File |
|-------|-------------|------|
| documents_metadata | Attachment metadata without binary blob | documents_metadata.sql |

## Notes

- The `Documents` table contains a binary `document` column up to 50MB - **never query it directly**
- Always use `documents_metadata` query which excludes the binary column
- Attachments are linked to parent objects (Announcement posts, wiki pages) via `parent` (EntityId)
- For wiki attachments, we need EntityId which isn't exposed in WikiVersions tables
