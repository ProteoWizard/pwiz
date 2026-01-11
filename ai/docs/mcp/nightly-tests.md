# Skyline Nightly Test Analysis System

This document describes the system for querying and analyzing Skyline nightly test results from skyline.ms using Claude Code.

## Overview

Skyline runs nightly automated tests across the codebase. Results are stored on the skyline.ms LabKey server and include:
- Test run summaries (pass/fail/leak counts, duration, memory usage)
- Individual test failures with stack traces
- Memory leak detections
- Handle leak detections

Claude Code can query this data via MCP tools to assist with test failure analysis and leak investigations.

## Data Sources

There are **two complementary data sources** for nightly test information:

### 1. Email Summary (8:00 AM Daily)

A summary email is sent at 8:00 AM to the skyline-dev list (also routed to claude.c.skyline@gmail.com):

**Subject format:**
```
TestResults MM/DD - MM/DD (8AM - 8AM) | Err: N Warn: N Pass: N Missing: N | N tests run
```

**Email content includes:**
- Per-computer results table with color-coded status
- Failure/Leak/Hang matrix showing which tests had issues on which computers
- Missing computers list
- Results for multiple folders: Nightly x64, Release Branch, Performance Tests

**Color coding in email (important for interpretation):**

| Color | Context | Meaning |
|-------|---------|---------|
| Green (#caff95) | Computer row | All metrics normal, no failures/leaks |
| Yellow (#ffffca) | Passes column | 3-4 SDs below trained mean |
| Yellow (#ffffca) | Memory column | 3-4 SDs above trained mean |
| Red (#ffcaca) | Computer row | Has failures, leaks, or hangs |
| Red (#ffcaca) | Passes column | >4 SDs below trained mean |
| Red (#ffcaca) | Memory column | >4 SDs above trained mean |
| Red (#ffcaca) | Duration column | Shorter than expected (540 normal, 720 perf) |
| Red (#ffcaca) | Missing row | Expected computer did not report (activated, has training data) |
| Gray (#cccccc) | Computer row | Unexpected computer reported (not activated, may lack training data) |

**Failure matrix notation:**
- Red X = Test failure
- Orange X = Leak (tooltip shows "Handle leak" or "Memory and handle leak")
- Navy X = Hang

**Duration notation:** "(hang)" appended to duration value indicates the run was terminated due to a hung test.

### 2. LabKey MCP Server (Detailed Data)

The MCP server provides detailed programmatic access for drill-down:
- Per-run details with statistical anomaly detection
- Full stack traces for failures
- Memory and handle leak measurements per test pass
- Historical data for trend analysis

**Use email for:** Quick triage, subject line summary, pass/warn/err counts
**Use MCP for:** Detailed investigation, stack traces, historical analysis

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

All 6 test folders are accessible via the MCP server by specifying the `container_path` parameter.

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
| `duration` | Run duration in minutes (540 for base, 720 for perf runs) |
| `passedtests` | Number of passed tests |
| `failedtests` | Number of failed tests |
| `leakedtests` | Number of tests with leaks |
| `averagemem` | Average memory usage in MB |
| `githash` | Git commit hash tested (e.g., "cd98ae308") |
| `os` | Operating system (e.g., "Microsoft Windows NT 10.0.19045.0") |
| `userid` | Foreign key to user table (computer name) |
| `revision` | Sequential build number from TeamCity |
| `flagged` | Whether the run was flagged for attention |

### Custom Queries (Server-Side Views)

These queries are created on the LabKey server to provide pre-joined, aggregated data:

| Query | Description | Parameters |
|-------|-------------|------------|
| `testruns_detail` | Test run summaries with computer, git hash, OS | `StartDate`, `EndDate` |
| `testpasses_detail` | Per-pass test data with computer name | `RunId` (required) |
| `failures_by_date` | Test failures with test names for a date range | `StartDate`, `EndDate` |
| `leaks_by_date` | Memory/handle leaks with test names and type | `StartDate`, `EndDate` |
| `expected_computers` | Active computers with trained mean/stddev for anomaly detection | (none) |
| `handleleaks_by_computer` | Handle leaks grouped by computer and test name | (none) |
| `testfails_by_computer` | Test failures grouped by computer and test name | (none) |
| `compare_run_timings` | Compare test durations between two runs | `RunIdBefore`, `RunIdAfter` |

### Test Run Summary Query (testruns_detail)

The primary query for reviewing nightly test results. Returns one row per test run with all key metrics:

```
query_table(
    schema_name="testresults",
    query_name="testruns_detail",
    container_path="/home/development/Nightly x64",
    parameters={"StartDate": "2025-12-07", "EndDate": "2025-12-14"}
)
```

**Returns:**

| Column | Description |
|--------|-------------|
| `run_id` | Unique run identifier |
| `computer` | Machine name (e.g., "BOSS-PC", "KAIPOT-PC1") |
| `posttime` | When the run was posted |
| `duration` | Run duration in minutes |
| `os` | Operating system version |
| `passedtests` | Number of passed tests |
| `failedtests` | Number of failed tests |
| `leakedtests` | Number of tests with leaks |
| `averagemem` | Average memory usage in MB |
| `githash` | Git commit hash tested |
| `revision` | TeamCity build number |
| `flagged` | Whether flagged for attention |

**Example output:**

| computer | posttime | duration | passed | fail | leaks | githash |
|----------|----------|----------|--------|------|-------|---------|
| BOSS-PC | 12/14 06:01 | 540 | 9148 | 0 | 0 | afdaac3ad |
| EKONEIL01 | 12/14 06:01 | 540 | 12769 | 0 | 0 | afdaac3ad |

**Tip:** A full base run should be ~540 minutes. Shorter runs may indicate hangs or incomplete testing.

### Per-Pass Test Data Analysis (testpasses_detail)

The `testpasses_detail` query returns handle/memory measurements for each pass of a test within a specific run. This is essential for diagnosing leaks.

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

**Why parameterized?** The `testpasses` table has 700M+ rows. The `RunId` parameter filters BEFORE joining, making it fast. Without it, the query would timeout.

**Returns:**

| Column | Description |
|--------|-------------|
| `run_date` | When the test run occurred |
| `computer` | Which machine ran the test |
| `testrunid` | Run ID for correlation |
| `testname` | Test name |
| `passnum` | Pass number (0, 1, 2...) |
| `handles` | Total handles at measurement |
| `userandgdihandles` | User and GDI handles |
| `managedmemory` | Managed heap in MB |
| `totalmemory` | Total memory in MB |
| `duration` | Test duration in seconds |

**Example output** for TestMethodRefinementTutorial in run 74829:

| Pass | Handles | User+GDI | Observation |
|------|---------|----------|-------------|
| 0 | 1024 | 129 | Starting baseline |
| 1 (start) | 1141 | 139 | +117 handles after pass 0 |
| 1 (end) | 1233 | 150 | Handles grow during pass |

This reveals whether handles leak between passes or accumulate within a pass.

### Leak Analysis Queries

**Example**: Query `handleleaks_by_computer` with filter `testname=TestMethodRefinementTutorial` returns:

| computer | testname | leak_count | avg_handles | last_seen |
|----------|----------|------------|-------------|-----------|
| KAIPO-DEV | TestMethodRefinementTutorial | 28 | 3.7 | 2025-12-08 |
| BRENDANX-UW5 | TestMethodRefinementTutorial | 19 | 2.7 | 2025-12-02 |

This immediately answers "which computer should I use to debug this leak?"

### Comparing Test Performance Between Runs (compare_run_timings)

The `compare_run_timings` query identifies performance regressions by comparing test durations between two runs. This is essential for diagnosing why test counts drop after code changes.

```
query_table(
    schema_name="testresults",
    query_name="compare_run_timings",
    container_path="/home/development/Performance Tests",
    parameters={"RunIdBefore": 79482, "RunIdAfter": 79619},
    max_rows=20
)
```

**Returns:**

| Column | Description |
|--------|-------------|
| `testname` | Test name |
| `passes` | Number of passes run (high = memory variance, low = stable) |
| `duration_before` | Average seconds per pass in baseline run |
| `duration_after` | Average seconds per pass in comparison run |
| `delta_avg` | Per-pass slowdown in seconds |
| `delta_total` | Total time impact: `passes × delta_avg` |
| `delta_percent` | Percentage slowdown |

**Example output** comparing runs before/after FilesView merge:

| testname | passes | before | after | delta_avg | delta_total | delta_% |
|----------|--------|--------|-------|-----------|-------------|---------|
| TestFilesTreeForm | 4 | NULL | 20 | NULL | NULL | NULL |
| TestImportHundredsOfReplicates | 1 | 136 | 2405 | 2269 | 2269 | 1668 |
| TestSynchronizeSummaryZooming | 1 | 1 | 12 | 11 | 11 | 1100 |

**Key insights from this output:**
- **New tests** appear at top with NULL before values (e.g., TestFilesTreeForm)
- **Major regressions** show large delta_total values
- **Pass count** indicates memory stability: low passes = stable memory, high passes = memory variance (system reruns until stable)
- **delta_total** is the actual time impact on the run (more useful than delta_avg for prioritizing fixes)

**Use case**: After merging a feature branch, compare a run from before the merge to one after to identify which tests slowed down and whether new tests are behaving properly.

## Available MCP Tools

| Tool | Description |
|------|-------------|
| `get_daily_test_summary(report_date)` | Query all 6 folders, save report to ai/.tmp/ |
| `save_test_failure_history(test_name, start_date, container_path)` | Collect stack traces for a test, detect patterns |
| `save_run_log(run_id, part)` | Save log section (full/git/build/testrunner/failures) to ai/.tmp/ |
| `save_run_xml(run_id)` | Save structured XML test data to ai/.tmp/ for analysis |
| `query_test_runs(days, max_rows)` | Query recent test runs with summaries |
| `get_run_failures(run_id)` | Get failed tests and stack traces for a run |
| `get_run_leaks(run_id)` | Get memory and handle leaks for a run |
| `fetch_labkey_page(view_name, container_path, params)` | Fetch any LabKey page (HTML), save to ai/.tmp/ |

### Daily Test Summary

The primary entry point for daily test review. Queries all 6 test folders in one call:

```
get_daily_test_summary(report_date="2025-12-14")
```

Returns a brief summary and saves a full markdown report to `ai/.tmp/nightly-report-YYYYMMDD.md` including:
- Summary table with pass/fail/leak/anomaly counts per folder
- Per-computer details with stddev-based anomaly detection
- Missing computers that didn't report
- **Failures by Test** - which tests failed on which computers
- **Leaks by Test** - which tests leaked on which computers

### Web Page Fetching (Developer View)

The `fetch_labkey_page` tool fetches authenticated LabKey pages - the same HTML that developers see in browsers. This provides richer context than API queries alone.

```
fetch_labkey_page(
    view_name="project-begin.view",
    container_path="/home/development/Nightly x64"
)
```

**Saves to:** `ai/.tmp/page-project_begin-Nightly_x64-YYYYMMDD.html`

**Available pages for nightly test analysis:**

| Page | Description | Useful For |
|------|-------------|------------|
| `project-begin.view` | Main dashboard with today's results | Current status, missing computers, top failures |
| `testresults-showRun.view?runId=N` | Single run details | Deep dive into specific run |
| `testresults-showFailures.view?failedTest=X` | Failure history for test | Pattern detection, regression timing |
| `testresults-longTerm.view` | Long-term trends | Historical patterns |
| `testresults-showUser.view?userId=N` | Results by computer | Machine-specific issues |

**What the HTML contains:**
- Color-coded status (same as email: green/yellow/red/gray)
- Training thresholds per computer (Good/Warn/Error boundaries)
- Top failures with language breakdown pie charts
- Missing computer list with deactivation links
- Links to drill-down pages

**Example workflow:**
1. Fetch today's dashboard: `fetch_labkey_page("project-begin.view", "/home/development/Nightly x64")`
2. Read the saved HTML to find specific run IDs or failure patterns
3. Drill down with: `fetch_labkey_page("testresults-showRun.view", params={"runId": 79713})`

This is equivalent to what developers do manually when triaging test results.

### Stack Trace Pattern Analysis

When a test fails on multiple machines, use `save_test_failure_history` to determine if they share the same root cause:

```
save_test_failure_history(
    test_name="TestPanoramaDownloadFile",
    start_date="2025-12-14",
    container_path="/home/development/Release Branch"
)
```

Returns summary and saves to `ai/.tmp/test-failures-{testname}.md` with:
- All stack traces grouped by unique pattern
- Pattern count (1 = same root cause, multiple = different issues)
- Affected computers per pattern

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

**Examine per-pass data for a specific run:**
> "Show me the handle counts for TestMethodRefinementTutorial in run 74829"

Use `query_table` with `testpasses_detail`, passing `param_name="RunId"` and `param_value="<run_id>"`, plus a filter on `testname`. This shows how handles/memory change across test passes.

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

To add new custom queries (like `handleleaks_by_computer`), see [MCP Development Guide](development-guide.md) for the server-side query pattern. This approach keeps complex JOINs on the LabKey server rather than in Python code.

## Future Enhancements

- Stack trace normalization (handle path/locale differences for better pattern detection)
- Trend analysis across multiple runs
- Flaky test identification (tests that pass/fail inconsistently)
- Automatic correlation with git commits
- `memoryleaks_by_computer` query for memory leak analysis

## Recently Implemented

- `/pw-nightly` slash command for daily test review ✓
- `get_daily_test_summary` - Query all 6 folders in one call ✓
- `save_test_failure_history` - Stack trace pattern grouping ✓
- `save_run_log` - Full log download via HTTP endpoint ✓
- `save_run_xml` - Structured XML test data via HTTP endpoint ✓
- `compare_run_timings` - Compare test durations between runs to identify regressions ✓
- `failures_by_date` / `leaks_by_date` queries - Test names for date ranges ✓
- `expected_computers` query - Stddev-based anomaly detection ✓
- "Failures by Test" / "Leaks by Test" sections in daily report ✓
- `fetch_labkey_page` - Fetch authenticated LabKey pages (HTML) for developer-level views ✓

## Related Documentation

- [MCP Development Guide](development-guide.md) - Patterns for extending MCP capabilities
- [Exceptions](exceptions.md) - User-reported crash analysis
- [Wiki](wiki.md) - Documentation access
- [Support](support.md) - Support board access
- [LabKey MCP Server README](../mcp/LabKeyMcp/README.md) - Setup instructions
- [Query Documentation](../mcp/LabKeyMcp/queries/README.md) - Server-side query reference
