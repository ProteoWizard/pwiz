---
description: Generate consolidated daily report (nightly tests, exceptions, support)
---

# Daily Consolidated Report

Generate a consolidated daily report covering:
1. Nightly test results (all 6 test folders)
2. User-submitted exceptions
3. Support board activity

**Arguments**:
- Date in YYYY-MM-DD format (optional, defaults to auto-calculated dates)
- Effort level: `quick`, `standard`, or `deep` (optional, defaults to `standard`)

## Effort Levels

| Level | Duration | Scope |
|-------|----------|-------|
| `quick` | ~1-2 min | Generate report, send email, minimal investigation |
| `standard` | ~15-30 min | Report + investigate top issues, git blame for regressions, update TODO |
| `deep` | Full session | Comprehensive analysis, follow every thread, learn from developer emails |

**Default is `standard`** - balance between quick reporting and meaningful investigation.

### Quick Mode
- Read emails, generate MCP reports, send summary email
- Archive processed emails
- No investigation follow-up

### Standard Mode (Default)
- Everything in Quick, plus:
- Investigate NEW failures (not in yesterday's data)
- Git blame/log to identify commits that may have caused regressions
- Check if known issues have fixes pending
- Update TODO with improvement ideas
- Write execution log

### Deep Mode
- Everything in Standard, plus:
- Review ALL failures, not just new ones
- Historical regression analysis (when did each failing test start failing?)
- Read and learn from forwarded developer emails
- Cross-reference exception patterns with code changes
- Propose actionable fixes, not just observations
- Comprehensive execution log with reasoning

## Data Sources

This command uses **two complementary data sources**:

1. **Inbox emails** (PRIMARY for summary statistics):
   - `TestResults MM/DD - MM/DD ...` - Nightly test summary sent at 8:00 AM
   - `[COMPUTER (branch)] !!! TestResults alert` - Hang alert when log frozen >1 hour
   - `New posts to /home/issues/exceptions` - Exception digest sent at 12:00 AM
   - `Support board summary` - Support digest (may not be present if no activity)

2. **LabKey MCP** (for detailed drill-down):
   - `get_daily_test_summary()` - Detailed per-run data
   - `save_exceptions_report()` - Full stack traces
   - `get_support_summary()` - Support thread details

## CRITICAL: Data Validation Requirements

**This report MUST fail if required data cannot be obtained. NEVER substitute stale data.**

### Required Data (MUST have - fail if missing)

| Data Source | Required? | Failure Action |
|-------------|-----------|----------------|
| Nightly test data (MCP) | **YES** | FAIL the report - do not send email |
| Fresh MCP query results | **YES** | FAIL - never use cached files from prior days |

### Optional Data (zero is valid)

| Data Source | Zero Valid? | Notes |
|-------------|-------------|-------|
| Exceptions | Yes (rare) | No exceptions some days is possible |
| Support threads | Yes (common) | No new threads is normal |

### Validation Rules

1. **Nightly tests**: The MCP call `get_daily_test_summary()` MUST succeed and return runs for today's date
   - If MCP call fails: FAIL the report
   - If MCP returns zero runs for today: FAIL (indicates either no tests ran or MCP permission issues)

2. **Never use stale data**: If you cannot query fresh data from MCP:
   - Do NOT fall back to reading old `nightly-report-*.md` files from prior days
   - Do NOT use `daily-summary-*.json` files as the primary data source
   - These files are for historical comparison ONLY, not substitutes for today's data

3. **Failure notification**: If the report cannot complete due to missing data:
   - Send an ERROR email with subject: `[ERROR] Skyline Daily Summary - Month DD, YYYY - Data Unavailable`
   - Body should explain: which data source failed, likely cause (MCP permissions?), how to fix
   - Exit with non-zero status so the scheduled task shows as failed

### Why This Matters

Silent fallback to cached data produces reports that look valid but contain stale information. This is worse than no report at all because it creates false confidence. A failed report is immediately visible and actionable.

## Default Date Logic

Each report type has different day boundaries:

| Report | Window | Default Date |
|--------|--------|--------------|
| Nightly | 8:01 AM to 8:00 AM next day | Today (if after 8 AM) or yesterday |
| Exceptions | 12:00 AM to 11:59 PM | Yesterday (complete 24h) |
| Support | Last N days | 1 day |

## Instructions

### Step 1: Determine Dates

If user provided a date argument, use it for all reports.

If no date provided, calculate defaults:
- For nightly: Current time before 8 AM -> yesterday's date; after 8 AM -> today's date
- For exceptions: Yesterday's date
- For support: 1 day lookback

### Step 2: Read Inbox Emails (Primary Source)

Search the Gmail inbox for today's notification emails:

```
search_emails(query="in:inbox from:skyline@proteinms.net newer_than:2d")
```

Look for these email types:
1. **TestResults email** - Subject format: `TestResults MM/DD - MM/DD (8AM - 8AM) | Err: N Warn: N Pass: N Missing: N | N tests run`
2. **Hang alert email** - Subject format: `[COMPUTER (branch)] !!! TestResults alert` - Indicates log frozen >1 hour, first sign of a hang
3. **Exceptions email** - Subject: `New posts to /home/issues/exceptions`
4. **Support email** - Subject contains support board references (may be absent)

For each email found, use `read_email(messageId)` to get full content.

### Step 3: Parse Email Content

**From TestResults email, extract:**
- Subject line summary: Err/Warn/Pass/Missing counts, total tests
- Per-computer table with: Computer, Memory, Tests, PostTime, Duration, Failures, Leaks, Git hash
- Failure/Leak/Hang matrix showing which tests failed on which computers
- "(hang)" notation in Duration column
- Missing computers list
- Multiple folders: Nightly x64, Release Branch, Performance Tests

**Color coding meanings (see ai/docs/mcp/nightly-tests.md for full details):**
- Green (#caff95): All metrics normal, no failures/leaks
- Yellow (#ffffca): 3-4 SDs from trained mean (passes below, memory above)
- Red (#ffcaca): Failures/leaks/hangs OR >4 SDs from mean OR short duration
- Red (missing row): Expected computer didn't report (activated, has training)
- Gray (#cccccc): Unexpected computer reported (not activated, may lack training)

**From Hang alert emails, extract:**
- Computer name and branch from subject (e.g., `[BOSS-PC (trunk)]`)
- Timestamp of the alert (when the hang was first detected)
- End of the log showing the last test that completed before the hang
- Note: Currently we see the test BEFORE the hang, not the hung test itself, because log output is only flushed on line completion. The 8:00 AM report will show which test was actually hung.

**From Exceptions email, extract:**
- Each exception entry with:
  - Location (file:line)
  - Version and Installation ID
  - Timestamp
  - Full stack trace
  - Link to view on skyline.ms
- **Important**: Group by Installation ID to identify same user hitting repeatedly vs different users

### Step 4: Generate MCP Reports (PRIMARY Source)

Run these three MCP calls for detailed data:

```
get_daily_test_summary(report_date="YYYY-MM-DD")
save_exceptions_report(report_date="YYYY-MM-DD")
get_support_summary(days=1)
```

**CRITICAL VALIDATION**: After each MCP call, verify it succeeded:

1. **Nightly tests** (`get_daily_test_summary`):
   - Check the return value indicates success
   - Verify the generated file `ai/.tmp/nightly-report-YYYYMMDD.md` has TODAY's date in filename
   - Confirm the report contains actual test runs (not "0 runs" or empty tables)
   - **If validation fails**: STOP and send ERROR email (see Data Validation Requirements above)

2. **Exceptions** (`save_exceptions_report`):
   - Zero exceptions is valid - just note "0 exceptions reported"
   - MCP failure is still an error (different from zero results)

3. **Support** (`get_support_summary`):
   - Zero threads is valid and common
   - MCP failure is still an error

Read the generated reports from `ai/.tmp/`:
- `nightly-report-YYYYMMDD.md` - **MUST exist with today's date**
- `exceptions-report-YYYYMMDD.md` - May show zero exceptions
- `support-report-YYYYMMDD.md` - May show zero threads

### Step 5: Cross-Reference and Reconcile

Compare email data with MCP data:
- If counts differ, note the discrepancy
- Email is authoritative for the current nightly window
- MCP provides deeper drill-down data

### Step 6: Analyze Patterns (MCP Tool)

Use the `analyze_daily_patterns` MCP tool to compare today's results against history:

```
analyze_daily_patterns(report_date="YYYY-MM-DD", days_back=7)
```

This tool automatically:
- Loads today's and yesterday's daily summary JSONs
- Compares failures, leaks, hangs
- Detects patterns and returns prioritized **Action Items**:
  - 🔴 **SYSTEMIC**: Affects 3+ machines (code issue, not environment)
  - 🆕 **NEW**: First appearance since yesterday (likely regression)
  - 🌐 **EXTERNAL**: Involves external service (Koina, Panorama, etc.)
  - ⚠️ **MISSING N DAYS**: Computer hasn't reported for multiple days
  - ✅ **RESOLVED**: Issue no longer present

**Note**: This tool requires historical JSON data from previous runs. If no history exists, it will prompt you to run `/pw-daily` to start building history.

### Step 7: Present Consolidated Summary

Provide a summary highlighting:
- **Nightly**: Use email subject line counts (Err/Warn/Pass/Missing), list specific failures/leaks/hangs
- **Exceptions**: Count exceptions, group by unique issue (location + error message), note if same user hit multiple times
- **Support**: Unanswered threads requiring attention

Include historical insights from Step 6:
- "NEW: TestFoo started failing today (not in previous 7 days)"
- "RESOLVED: TestBar no longer failing (was failing yesterday)"
- "RECURRING: COMPUTER-X missing for 3 consecutive days"
- "TREND: ExceptionSignature appeared 2 days ago, now at 5 occurrences"

### Step 8: Save Daily Summary JSON (MCP Tool)

Use the `save_daily_summary` MCP tool to save structured data for pattern detection:

```
save_daily_summary(
    report_date="YYYY-MM-DD",
    nightly_summary='{"errors": N, "warnings": N, "passed": N, "missing": N, "total_tests": N}',
    nightly_failures='{"TestName": ["COMPUTER1", "COMPUTER2"]}',
    nightly_leaks='{"TestName": ["COMPUTER1"]}',
    nightly_hangs='{"TestName": ["COMPUTER1"]}',
    missing_computers='["COMPUTER1", "COMPUTER2"]',
    exception_count=N,
    exception_signatures='{"ExceptionType at File.cs:line": {"count": N, "installation_ids": ["id1"]}}',
    support_threads=N
)
```

**Extract this data from the MCP reports generated in Step 4:**
- Parse `nightly-report-YYYYMMDD.md` for failures, leaks, hangs by test name and computer
- Count exceptions from `exceptions-report-YYYYMMDD.md`
- Count unanswered threads from `support-report-YYYYMMDD.md`

The tool saves to `ai/.tmp/history/daily-summary-YYYYMMDD.json` and confirms success.

**Why this matters**: This historical data enables `analyze_daily_patterns` to detect NEW failures, RESOLVED issues, and recurring patterns. Without it, pattern detection cannot function.

### Step 9: Archive Processed Emails

After completing the report, archive all processed notification emails to keep the inbox clean for the next run:

```
batch_modify_emails(messageIds=[...], removeLabelIds=["INBOX"])
```

This ensures:
- Tomorrow's reporter sees only new emails
- No duplicate processing of old notifications
- Clear signal that inbox emails are unprocessed items

### Step 10: Investigate Regressions (Standard/Deep Mode)

**Skip in Quick mode.**

For NEW failures (tests failing today but not yesterday):

1. **Identify the test file**: Use grep to find the test class
2. **Find related code**: Look at files the test exercises
3. **Git blame**: Check recent commits to those files
   ```bash
   git log --oneline --since="7 days ago" -- path/to/file.cs
   ```
4. **Cross-reference with git hash**: Compare failure start date with commit dates
5. **Document findings**: "TestFoo likely regressed by commit abc123 (author, date)"

For **Deep mode**, also:
- Query `save_test_failure_history()` for each failing test
- Find the exact date failures started
- Trace back to specific commits

### Step 11: Learn from Developer Emails (Deep Mode)

**Skip in Quick/Standard mode.**

Search for forwarded developer emails analyzing test results:

```
search_emails(query="in:inbox subject:Fwd: newer_than:7d")
```

For each forwarded email:
1. **Read the developer's analysis**: What did they notice? What conclusions did they draw?
2. **Compare to system report**: Did the system report this same issue? Did it reach similar conclusions?
3. **Identify gaps**: What did the developer see that the system missed?
4. **Learn patterns**: Add investigation patterns to TODO for future automation

This creates a **feedback loop**: Developer analyses become training signal for improving the automated system.

### Step 12: Self-Improvement Reflection

After completing the report, reflect on the reporting system itself:

1. **Read the active TODO** at `ai/todos/active/TODO-20251228_daily_report_improvements.md`
   - This contains the backlog of planned improvements
   - Do NOT re-suggest items already in this TODO
2. **Analyze this session** for potential improvements:
   - Were there data gaps between email and MCP?
   - Was any information hard to extract or missing?
   - Could the report format be improved?
   - Are there new patterns worth tracking?
   - Were there false positives/negatives in anomaly detection?
   - (Deep mode) What did developers notice that the system missed?
3. **Propose NEW improvements only** (don't repeat items already in the active TODO)
4. **If you have new ideas**, add them to the Progress Log section of the active TODO
5. **Report in email**:
   - "New improvement idea added to TODO: [brief description]"
   - OR "No new improvement ideas (reviewed active TODO)"

### Step 13: Write Execution Log

Write a log of what was analyzed and decided during this session:

```
File: ai/.tmp/logs/daily-session-YYYYMMDD.md
```

**Log contents:**
```markdown
# Daily Session Log - YYYY-MM-DD

**Effort level**: quick | standard | deep
**Duration**: X minutes
**Started**: HH:MM
**Completed**: HH:MM

## Data Sources Consulted
- [ ] Nightly email
- [ ] Hang alert emails (count: N)
- [ ] Exception digest email
- [ ] Support email
- [ ] MCP nightly report
- [ ] MCP exceptions report
- [ ] MCP support report
- [ ] Historical JSON files (N days)
- [ ] Developer forwarded emails (N)

## Key Observations
- [What was notable about today's results]

## Investigations Performed
- [What was followed up on and why]
- [Git blame results, if any]
- [Historical queries, if any]

## Conclusions
- [Actionable findings]
- [Root causes identified]
- [Recommendations made]

## System Improvements Identified
- [New ideas added to TODO, or "none"]

## Comparison to Developer Analysis (Deep mode)
- [What developers noticed that system missed]
- [Gaps to address]
```

This log enables:
- Reviewing what the system actually did (not just what it reported)
- Tracking improvement over time
- Debugging when the system misses something

## Output Files

Reports saved to `ai/.tmp/`:
- `nightly-report-YYYYMMDD.md` - Full test results from MCP
- `exceptions-report-YYYYMMDD.md` - Exception details with stack traces
- `support-report-YYYYMMDD.md` - Support thread summary

Historical data saved to `ai/.tmp/history/`:
- `daily-summary-YYYYMMDD.json` - Structured daily summary for trend analysis
- Files accumulate over time; do not delete (enables longitudinal analysis)

Session logs saved to `ai/.tmp/logs/`:
- `daily-session-YYYYMMDD.md` - Execution log of what was analyzed and decided
- Enables review of system behavior and improvement tracking

Improvement tracking:
- `ai/todos/active/TODO-20251228_daily_report_improvements.md` - Active backlog

## Scheduled Session Configuration

The daily report runs as a scheduled Claude Code session. To configure effort level:

**In Claude Code settings** (scheduled task configuration):
```
Prompt: /pw-daily standard
```

Or for deep analysis:
```
Prompt: /pw-daily deep
```

**Session budget considerations:**
- Quick: ~$0.10-0.20 (minimal API calls)
- Standard: ~$0.50-2.00 (investigation, git queries)
- Deep: ~$5.00-15.00 (comprehensive analysis, may approach context limits)

## Email Configuration

**Default recipient**: brendanx@proteinms.net

## Email Summary Format

When sending the summary email to the configured recipient, include:

```
Subject: Skyline Daily Summary - Month DD, YYYY

## Quick Status
- Nightly: [Err: X | Warn: X | Pass: X | Missing: X] - N tests
- Exceptions: X new (Y unique issues, Z users affected)
- Support: X threads needing attention

## Key Findings
[Prioritized list of issues requiring attention]

## Details
[Expandable sections for each category]

## Improvement Ideas
[Whether TODO file was generated or not]

---
Generated by Claude Code Daily Report System
```

## Follow-up Investigation

- **Pattern analysis**: Use `analyze_daily_patterns(report_date)` to compare with history
- **Test failures**: Use `save_test_failure_history(test_name, start_date)`
- **Test logs**: Use `save_run_log(run_id)`
- **Exception details**: Use `get_exception_details(exception_id)`
- **Support threads**: Use `get_support_thread(thread_id)`

## Related

- `/pw-nightly` - Nightly tests only
- `/pw-exceptions` - Exceptions only
- `/pw-support` - Support board only
- `ai/docs/nightly-test-analysis.md` - Test analysis workflow
- `ai/docs/exception-triage-system.md` - Exception triage workflow
