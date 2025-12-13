---
name: skyline-nightlytests
description: Use when working with Skyline nightly test results, failures, or leaks. Activate for querying test runs from skyline.ms, analyzing test failures, investigating memory/handle leaks, or identifying which computer to use for debugging.
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

**Data location**: skyline.ms -> /home/development/Nightly x64 -> testresults schema

**MCP tools available**:
- `query_test_runs(days, max_rows)` - Recent test run summaries
- `get_run_failures(run_id)` - Failed tests with stack traces
- `get_run_leaks(run_id)` - Memory and handle leaks
- `query_table` - Generic queries including custom server-side queries

**Server-side queries** (use via query_table):
- `handleleaks_by_computer` - Handle leaks grouped by computer
- `testfails_by_computer` - Test failures grouped by computer

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
query_test_runs(days=7)  # Find run with failures
get_run_failures(run_id=...)  # Get details
```

### "Are there memory leaks I should investigate?"

```
query_test_runs(days=7)  # Find run with leaks
get_run_leaks(run_id=...)  # Get details
```
