# Nightly Test Queries

**Container:** `/home/development/Nightly x64` (and other test folders)
**Schema:** `testresults`

## Base Tables

| Table | Description | Schema File |
|-------|-------------|-------------|
| testruns | Test run summaries (one row per run) | testruns-schema.md |
| testpasses | Individual test passes (**700M+ rows - always filter!**) | testpasses-schema.md |
| testfails | Failed tests with stack traces | testfails-schema.md |
| handleleaks | Handle leak records | handleleaks-schema.md |
| memoryleaks | Memory leak records | memoryleaks-schema.md |
| user | Computer/user mapping | user-schema.md |
| expected_computers | Baseline statistics per computer | expected_computers-schema.md |

## Custom Queries Used by server.py

| Query | Description | File | MCP Tool |
|-------|-------------|------|----------|
| testruns_detail | Extended run info with date filtering | testruns_detail.sql | `get_daily_test_summary()` |
| failures_by_date | Failures in date range with computer info | failures_by_date.sql | `get_daily_test_summary()`, `save_test_failure_history()` |
| failures_with_traces_by_date | Failures with stack traces for 8AM window | failures_with_traces_by_date.sql | `save_daily_failures()` |
| leaks_by_date | Memory/handle leaks in date range | leaks_by_date.sql | `get_daily_test_summary()` |

## Proposed Queries (Not Yet Used)

| Query | Description | File | Status |
|-------|-------------|------|--------|
| testpasses_detail | Detailed pass data for a run | testpasses_detail.sql | Proposed |
| testpasses_summary | Average durations per test for timing analysis | testpasses_summary.sql | Proposed |
| handleleaks_by_computer | Leaks aggregated by computer | handleleaks_by_computer.sql | Proposed |
| testfails_by_computer | Failures aggregated by computer | testfails_by_computer.sql | Proposed |
| compare_run_timings | Compare durations between runs | compare_run_timings.sql | Draft (subquery issues) |

## Notes

- The `testpasses` table is huge (700M+ rows) - always filter by testrunid
- Base tables `testruns`, `testfails`, `memoryleaks`, `handleleaks` are queried directly by some MCP tools
- The `expected_computers` table/query provides trained baseline values for anomaly detection
