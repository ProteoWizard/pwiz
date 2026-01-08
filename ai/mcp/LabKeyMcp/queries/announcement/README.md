# Announcement Queries

**Schema:** `announcement`

Queries for the Announcement table, which is used across multiple containers on skyline.ms.

## Containers Using Announcement

See [announcement-usage.md](../announcement-usage.md) for the full list including:
- `/home/support` - Support board
- `/home/issues/exceptions` - Exception tracking
- `/home/software/Skyline/daily` - Beta releases
- `/home/software/Skyline/events/*` - Event registration
- `/home/software/Skyline/funding/*` - Funding appeals
- `/home/software/Skyline/releases` - Release announcements

## Queries

| Query | Description |
|-------|-------------|
| announcement_threads_recent | Recent threads with metadata |
| announcement_thread_posts | All posts in a thread with EntityId |

## Schema

See [announcement-schema.md](announcement-schema.md) for the full table structure.

## Related

- `support/documents_metadata.sql` - Attachment lookup (works with any Announcement container that has corex schema)
