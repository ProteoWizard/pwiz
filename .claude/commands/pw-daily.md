---
description: Generate consolidated daily report (nightly tests, exceptions, support)
---

# Daily Consolidated Report

Generate a consolidated daily report covering:
1. Nightly test results (all 6 test folders)
2. User-submitted exceptions
3. Support board activity

**Argument**: Date in YYYY-MM-DD format (optional, defaults to auto-calculated dates)

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

### Step 4: Generate MCP Reports (Secondary Source)

Run these three MCP calls for detailed data:

```
get_daily_test_summary(report_date="YYYY-MM-DD")
save_exceptions_report(report_date="YYYY-MM-DD")
get_support_summary(days=1)
```

Read the generated reports from `ai/.tmp/`:
- `nightly-report-YYYYMMDD.md`
- `exceptions-report-YYYYMMDD.md`
- `support-report-YYYYMMDD.md`

### Step 5: Cross-Reference and Reconcile

Compare email data with MCP data:
- If counts differ, note the discrepancy
- Email is authoritative for the current nightly window
- MCP provides deeper drill-down data

### Step 6: Load Historical Data

Read previous daily summaries from `ai/.tmp/history/` to enable trend analysis:

```
# Read last 7 days of history (or more for specific investigations)
Glob pattern: ai/.tmp/history/daily-summary-*.json
```

For each historical file found, load and compare:
- **NEW failures**: Tests in today's failures not in yesterday's
- **RESOLVED failures**: Tests in yesterday's failures not in today's
- **Recurring missing**: Computers missing for multiple consecutive days
- **Exception trends**: Signatures appearing/increasing over time

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

### Step 8: Save Daily Summary JSON

Write a structured JSON summary for future trend analysis:

```
File: ai/.tmp/history/daily-summary-YYYYMMDD.json
```

**JSON Schema:**
```json
{
  "date": "YYYY-MM-DD",
  "generated_at": "ISO timestamp",
  "nightly": {
    "summary": { "errors": N, "warnings": N, "passed": N, "missing": N, "total_tests": N },
    "failures": { "TestName": ["COMPUTER1", "COMPUTER2"] },
    "leaks": { "TestName": ["COMPUTER1"] },
    "hangs": { "TestName": ["COMPUTER1"] },
    "missing_computers": ["COMPUTER1", "COMPUTER2"]
  },
  "exceptions": {
    "count": N,
    "by_signature": {
      "ExceptionType at File.cs:line": {
        "count": N,
        "installation_ids": ["id1", "id2"]
      }
    }
  },
  "support": {
    "threads_needing_attention": N
  }
}
```

This file accumulates over time, enabling queries like:
- "When did TestFoo start failing?" (scan backwards for first appearance)
- "How often does this exception occur?" (count across days)
- "Which computers are chronically missing?" (count consecutive days)

### Step 9: Archive Processed Emails

After completing the report, archive all processed notification emails to keep the inbox clean for the next run:

```
batch_modify_emails(messageIds=[...], removeLabelIds=["INBOX"])
```

This ensures:
- Tomorrow's reporter sees only new emails
- No duplicate processing of old notifications
- Clear signal that inbox emails are unprocessed items

### Step 10: Self-Improvement Reflection

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
3. **Propose NEW improvements only** (don't repeat items already in the active TODO)
4. **If you have new ideas**, add them to the Progress Log section of the active TODO
5. **Report in email**:
   - "New improvement idea added to TODO: [brief description]"
   - OR "No new improvement ideas (reviewed active TODO)"

## Output Files

Reports saved to `ai/.tmp/`:
- `nightly-report-YYYYMMDD.md` - Full test results from MCP
- `exceptions-report-YYYYMMDD.md` - Exception details with stack traces
- `support-report-YYYYMMDD.md` - Support thread summary

Historical data saved to `ai/.tmp/history/`:
- `daily-summary-YYYYMMDD.json` - Structured daily summary for trend analysis
- Files accumulate over time; do not delete (enables longitudinal analysis)

Improvement tracking:
- `ai/todos/active/TODO-20251228_daily_report_improvements.md` - Active backlog

## Email Summary Format

When sending the summary email, include:

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
