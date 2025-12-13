# Skyline Nightly Test Analysis System

This document describes the system for querying and analyzing Skyline nightly test results from skyline.ms using Claude Code.

## Overview

Skyline runs nightly automated tests across the codebase. Results are stored on the skyline.ms LabKey server and include:
- Test run summaries (pass/fail/leak counts, duration, memory usage)
- Individual test failures with stack traces
- Memory leak detections
- Handle leak detections

Claude Code can query this data via MCP tools to assist with test failure analysis and leak investigations.

## Architecture

```
Claude Code
    │
    └── MCP Protocol (stdio)
            │
            └── LabKeyMcp Server (Python)
                    │
                    └── LabKey Python SDK
                            │
                            └── skyline.ms LabKey Server
                                    │
                                    └── /home/development/Nightly x64
                                            └── testresults schema
```

## Test Folders

Test results are organized into folders under `/home/development/` on skyline.ms. Each folder serves a different purpose:

| Folder | Tests | Duration | Branch | Purpose |
|--------|-------|----------|--------|---------|
| **Nightly x64** | Base (5 projects) | 9 hrs | master | Daily dev validation - most important |
| **Performance Tests** | Base + TestPerf | 12 hrs | master | Extended perf testing on dedicated machines |
| **Release Branch** | Base | 9 hrs | release_25_1 | Release stability verification |
| **Release Branch Perf** | Base + TestPerf | 12 hrs | release_25_1 | Release performance verification |
| **Integration** | Base | 9 hrs | Assigned branch | Pre-merge validation for large branches |
| **Integration with Perf** | Base + TestPerf | 12 hrs | Assigned branch | Extended pre-merge validation |

**Base test projects**: Test, TestConnected, TestData, TestFunctional, TestTutorial (under `pwiz_tools/Skyline`)

**Most commonly queried**:
- **Nightly x64** - Daily development (default)
- **Integration** - Large branch validation before merge (e.g., PR #3687)
- **Performance Tests** - Perf regression detection

**Note**: Currently only **Nightly x64** has the `testresults` schema accessible via the MCP server. Other folders require server-side configuration to enable the schema.

## Data Location

Test results data lives at:
- **Server**: `skyline.ms`
- **Container**: `/home/development/Nightly x64` (default, others available)
- **Schema**: `testresults`

### Available Tables

| Table | Description |
|-------|-------------|
| `testruns` | Test run summaries with pass/fail/leak counts |
| `testfails` | Individual failed test details with stack traces |
| `testpasses` | Individual passed test details |
| `memoryleaks` | Memory leak detections by test |
| `handleleaks` | Handle leak detections by test |
| `hangs` | Test hang detections |

### Key Columns in `testruns`

| Column | Description |
|--------|-------------|
| `id` | Unique run identifier |
| `posttime` | When the run was posted |
| `duration` | Run duration in minutes |
| `passedtests` | Number of passed tests |
| `failedtests` | Number of failed tests |
| `leakedtests` | Number of tests with leaks |
| `averagemem` | Average memory usage in MB |
| `revision` | Git revision tested |
| `flagged` | Whether the run was flagged for attention |

### Custom Queries (Server-Side Views)

These queries are created on the LabKey server to provide pre-joined, aggregated data:

| Query | Description |
|-------|-------------|
| `handleleaks_by_computer` | Handle leaks grouped by computer and test name |
| `testfails_by_computer` | Test failures grouped by computer and test name |

**Example**: Query `handleleaks_by_computer` with filter `testname=TestMethodRefinementTutorial` returns:

| computer | testname | leak_count | avg_handles | last_seen |
|----------|----------|------------|-------------|-----------|
| KAIPO-DEV | TestMethodRefinementTutorial | 28 | 3.7 | 2025-12-08 |
| BRENDANX-UW5 | TestMethodRefinementTutorial | 19 | 2.7 | 2025-12-02 |

This immediately answers "which computer should I use to debug this leak?"

## Available MCP Tools

| Tool | Description |
|------|-------------|
| `query_test_runs(days, max_rows)` | Query recent test runs with summaries |
| `get_run_failures(run_id)` | Get failed tests and stack traces for a run |
| `get_run_leaks(run_id)` | Get memory and handle leaks for a run |

## Usage Examples

After MCP server setup, Claude Code can query test data directly:

**Query recent test runs:**
> "Show me test runs from the last 7 days"

**Investigate failures:**
> "What tests failed in run #79466?"

**Check for leaks:**
> "Show memory leaks for run #79450"

**Which computer to debug on:**
> "Which computer shows TestMethodRefinementTutorial handle leaks most frequently?"
> "What computer should I use to debug TestToolService failures?"

Use `query_table` with `handleleaks_by_computer` or `testfails_by_computer` filtered by test name. The first result (highest count) is the best debugging target.

**Analyze trends:**
> "Compare the last 14 days of test runs"

## Analysis Workflow

1. **Query recent runs**: Start with `query_test_runs(days=7)` to see recent activity
2. **Identify problems**: Look for runs with failures or leaks
3. **Drill down**: Use `get_run_failures(run_id)` to see stack traces
4. **Check leaks**: Use `get_run_leaks(run_id)` for memory/handle issues
5. **Correlate**: Cross-reference with git revisions to identify problematic commits
6. **Investigate**: Search codebase for related code based on stack traces

## Extending the System

To add new custom queries (like `handleleaks_by_computer`), see [MCP Development Guide](mcp-development-guide.md) for the server-side query pattern. This approach keeps complex JOINs on the LabKey server rather than in Python code.

## Future Enhancements

- `/pw-nightly` slash command for daily test review
- Trend analysis across multiple runs
- Flaky test identification (tests that pass/fail inconsistently)
- Automatic correlation with git commits
- Test failure grouping by stack trace signature
- `memoryleaks_by_computer` query for memory leak analysis

## Related Documentation

- [MCP Development Guide](mcp-development-guide.md) - Patterns for extending MCP capabilities
- [Exception Triage System](exception-triage-system.md) - User-reported crash analysis
- [LabKey MCP Server README](../../pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/README.md) - Setup instructions
