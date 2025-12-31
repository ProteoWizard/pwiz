# Announcement Table Usage Across skyline.ms

The `announcement.Announcement` table is a general-purpose content table used throughout skyline.ms for various purposes. The same table schema exists in different containers.

## Container Map

| Container | Purpose | Notes |
|-----------|---------|-------|
| `/home/support` | Support board | User questions and team responses |
| `/home/issues/exceptions` | Exception tracking | Crash reports from Skyline users |
| `/home/software/Skyline/daily` | Beta release notes | Running list of daily/beta releases |
| `/home/software/Skyline/releases` | Release announcements | Email archives (pre-MailChimp through 2024) |
| `/home/software/Skyline/events/*` | Event registration | Many sub-folders for courses, user groups |
| `/home/software/Skyline/funding/*` | Funding appeals | 4 sub-folders for NIH grants, vendor support |

## Query Naming

Queries are prefixed `announcement_` to reflect their generic nature:
- `announcement_threads_recent` - Recent threads with metadata
- `announcement_thread_posts` - All posts in a thread with EntityId

These queries work in ANY container with an Announcement table.

## TODO

- [ ] Reuse announcement queries for exceptions container (currently uses direct table query)
- [ ] Copy 25.1 release email from MailChimp to `/home/software/Skyline/releases`

## MailChimp Transition

Release announcement emails were stored directly in the releases container through 2024. Starting 2025, emails are sent via MailChimp (scale required external service to avoid spam filtering). Archives should still be copied to the releases container for historical record.
