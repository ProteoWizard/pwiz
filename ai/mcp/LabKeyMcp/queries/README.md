# LabKey Server-Side Queries

This directory documents custom queries on skyline.ms that the MCP server uses. Each .sql file shows the query definition; schema files document the underlying tables.

## Directory Structure

- `announcement/` - Shared Announcement table schema and queries (used by multiple containers)
- `corex/` - Document/attachment storage (shared between support and wiki)
- `exceptions/` - Exception-specific notes (uses base Announcement table directly)
- `nightly/` - Queries in `/home/development/Nightly x64` (also used by other test folders)
- `support/` - Support board notes (uses shared announcement + corex)
- `wiki/` - Queries in `/home/software/Skyline`

## Queries Used by server.py

### Nightly Tests (`/home/development/Nightly x64`)
| Query | Schema | Description | Used By |
|-------|--------|-------------|---------|
| testruns_detail | testresults | Extended test run info with date filtering | `get_daily_test_summary()` |
| expected_computers | testresults | Computer baseline statistics for anomaly detection | `get_daily_test_summary()` |
| failures_by_date | testresults | Test failures in date range | `get_daily_test_summary()`, `save_test_failure_history()` |
| leaks_by_date | testresults | Memory/handle leaks in date range | `get_daily_test_summary()` |

### Announcement (shared across containers)
| Query | Schema | Description |
|-------|--------|-------------|
| announcement_threads_recent | announcement | Recent threads with metadata |
| announcement_thread_posts | announcement | All posts in a thread with EntityId |

See [announcement-usage.md](announcement-usage.md) for containers using Announcement.

### Support Board (`/home/support`)

*(Uses shared announcement queries + corex for attachments)*

### Attachments (corex schema)
| Query | Schema | Description | Used By |
|-------|--------|-------------|---------|
| documents_metadata | corex | Attachment metadata (excludes binary blob) | `list_attachments()` |

Available in: `/home/support`. Pending: `/home/software/Skyline` (for wiki attachments).

### Wiki (`/home/software/Skyline`)
| Query | Schema | Description | Used By |
|-------|--------|-------------|---------|
| wiki_page_content | wiki | Full page content by name | `get_wiki_page()` |
| wiki_page_list | wiki | All pages with metadata (no body) | `list_wiki_pages()` |

## Base Tables Used Directly

These tables are queried directly by server.py without custom queries:

| Table | Schema | Container | Used By |
|-------|--------|-----------|---------|
| Announcement | announcement | /home/issues/exceptions | `query_exceptions()`, `get_exception_details()` |
| testruns | testresults | (test folders) | `query_test_runs()` |
| testfails | testresults | (test folders) | `get_run_failures()`, `save_test_failure_history()` |
| memoryleaks | testresults | (test folders) | `get_run_leaks()` |
| handleleaks | testresults | (test folders) | `get_run_leaks()` |

## Proposed Queries (Not Yet Used)

These queries exist in the documentation but aren't currently used by server.py:

| Query | Schema | Description | Status |
|-------|--------|-------------|--------|
| testpasses_detail | testresults | Detailed pass data for a specific run | Proposed |
| testpasses_summary | testresults | Average durations per test for a run | Proposed |
| handleleaks_by_computer | testresults | Handle leaks aggregated by computer | Proposed |
| testfails_by_computer | testresults | Failures aggregated by computer | Proposed |
| compare_run_timings | testresults | Compare test durations between runs | Draft (may need subquery support) |

## Schema Documentation

**Announcement:**
- `announcement/announcement-schema.md` - Shared Announcement table
- `announcement/threads-schema.md` - Threads view (aggregates posts)

**Nightly Tests (testresults):**
- `nightly/testruns-schema.md` - Test run summaries
- `nightly/testpasses-schema.md` - Individual test results
- `nightly/testfails-schema.md` - Test failures with stack traces
- `nightly/memoryleaks-schema.md` - Memory leak records
- `nightly/handleleaks-schema.md` - Handle leak records
- `nightly/user-schema.md` - Computer name mapping + userdata baseline statistics
- `nightly/expected_computers-schema.md` - Query joining user/userdata

**Wiki:**
- `wiki/wikiversions-schema.md` - Wiki version tables

**Attachments (corex):**
- `corex/documents-schema.md` - Document/attachment storage (shared)

## Maintenance

When creating or modifying queries on skyline.ms:
1. Update the corresponding .sql file here
2. Update this README if adding new queries
3. Update schema docs if using new tables

## Notes

- Queries with `PARAMETERS` require `parameters={...}` when called
- The `testpasses` table has 700M+ rows - always filter by testrunid
- The `documents` table has a binary blob column up to 50MB - always use `documents_metadata`
- The `announcement_*` queries are generic but currently named `support_*` on the server
