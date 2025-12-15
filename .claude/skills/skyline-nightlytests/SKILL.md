---
name: skyline-nightlytests
description: Use when investigating Skyline nightly test issues, handle leaks, memory leaks, or test failures. Activate for questions like "what's causing this leak", "why is this test failing", or "which computer shows this problem". Also use for querying test runs from skyline.ms or analyzing patterns in test results.
---

# Skyline Nightly Test Analysis

When working with Skyline nightly test data from skyline.ms, consult these documentation files.

## Core Documentation

1. **ai/docs/nightly-test-analysis.md** - Complete system documentation
   - Test folders and their purposes
   - Available tables and queries
   - MCP tools reference
   - Analysis workflow

2. **ai/docs/mcp-development-guide.md** - MCP patterns
   - Server-side query pattern
   - How to extend with new queries

## When to Read What

- **Before querying test results**: Read ai/docs/nightly-test-analysis.md (MCP tools section)
- **To understand test folders**: Read ai/docs/nightly-test-analysis.md (Test Folders table)
- **For extending queries**: Read ai/docs/mcp-development-guide.md (Server-Side Custom Queries)
- **For MCP server changes**: Read pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/README.md

## Quick Reference

**Data location**: skyline.ms -> /home/development/{folder} -> testresults schema

**Test folders** (all accessible via `container_path` parameter):
| Folder | Duration | Purpose |
|--------|----------|---------|
| Nightly x64 | 540 min | Daily dev validation (default) |
| Performance Tests | 720 min | Extended perf testing |
| Release Branch | 540 min | Release stability |
| Integration | 540 min | Pre-merge validation |

**MCP tools available**:
- `query_test_runs(days, max_rows)` - Recent test run summaries
- `get_run_failures(run_id)` - Failed tests with stack traces
- `get_run_leaks(run_id)` - Memory and handle leaks
- `query_table` - Generic queries including custom server-side queries

**Server-side queries** (use via query_table with `parameters`):
- `testruns_detail` - Test run summaries with computer name (params: StartDate, EndDate)
- `testpasses_detail` - Per-pass handle/memory data (param: RunId)
- `handleleaks_by_computer` - Handle leaks grouped by computer
- `testfails_by_computer` - Test failures grouped by computer

## Common Operations

### Review test runs for a date range (bread-and-butter)

This is the primary way to start investigating nightly test results:

```
query_table(
    schema_name="testresults",
    query_name="testruns_detail",
    container_path="/home/development/Nightly x64",
    parameters={"StartDate": "2025-12-13", "EndDate": "2025-12-14"}
)
```

Returns: run_id, computer, posttime, duration, passedtests, failedtests, leakedtests, averagemem, githash, os

**Tip**: Duration of 540 min = complete base run; shorter may indicate hangs.

### Examine per-pass data for leak investigation

When investigating handle/memory leaks, examine how values change across test passes:

```
query_table(
    schema_name="testresults",
    query_name="testpasses_detail",
    container_path="/home/development/Nightly x64",
    parameters={"RunId": "74829"},
    filter_column="testname",
    filter_value="TestMethodRefinementTutorial"
)
```

Returns: run_date, computer, testrunid, testname, passnum, handles, userandgdihandles, managedmemory, totalmemory, duration

This reveals whether handles/memory leak between passes or accumulate within a pass.

## Common Questions

### "Which computer should I use to debug this leak/failure?"

Use the server-side queries to find computers most affected:

**For handle leaks**:
```
query_table(schema="testresults", query="handleleaks_by_computer",
            filter_column="testname", filter_value="TestMethodRefinementTutorial")
```

**For test failures**:
```
query_table(schema="testresults", query="testfails_by_computer",
            filter_column="testname", filter_value="TestToolService")
```

Results are ranked by count, so the first computer listed has the most occurrences and is the best candidate for debugging.

### "What tests are failing in nightly?"

```
# Step 1: Find runs with failures
query_table(schema_name="testresults", query_name="testruns_detail",
            parameters={"StartDate": "2025-12-13", "EndDate": "2025-12-14"})
# Look for rows where failedtests > 0

# Step 2: Get failure details
get_run_failures(run_id=...)  # Use run_id from step 1
```

### "Are there memory leaks I should investigate?"

```
# Step 1: Find runs with leaks
query_table(schema_name="testresults", query_name="testruns_detail",
            parameters={"StartDate": "2025-12-13", "EndDate": "2025-12-14"})
# Look for rows where leakedtests > 0

# Step 2: Get leak details
get_run_leaks(run_id=...)  # Use run_id from step 1
```
