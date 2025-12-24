# Documents Table Schema

**Container:** `/home/support` (and other containers with corex schema)
**Schema:** `corex`
**Table:** `documents`

## Columns

| Column | Type | Description |
|--------|------|-------------|
| rowid | Integer | Primary key |
| parent | Text | EntityId of parent object (post, wiki page, etc.) |
| documentname | Text | Filename |
| documentsize | Integer | File size in bytes |
| documenttype | Text | MIME type |
| document | Binary | **⚠️ LARGE** - Up to 50MB binary blob, use `get_attachment()` |

## Notes

- The `document` column contains binary file data up to 50MB
- Always use `documents_metadata` query which excludes this column
- `parent` is the EntityId of the attachment's parent (announcement post, wiki page, etc.)
