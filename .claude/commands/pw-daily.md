---
description: Generate consolidated daily report (nightly tests, exceptions, support)
---

# Daily Consolidated Report

Generate a consolidated daily report covering:
1. Nightly test results (all 6 test folders)
2. User-submitted exceptions
3. Support board activity

**Argument**: Date in YYYY-MM-DD format (optional, defaults to auto-calculated dates)

## Default Date Logic

Each report type has different day boundaries:

| Report | Window | Default Date |
|--------|--------|--------------|
| Nightly | 8:01 AM to 8:00 AM next day | Today (if after 8 AM) or yesterday |
| Exceptions | 12:00 AM to 11:59 PM | Yesterday (complete 24h) |
| Support | Last N days | 1 day |

## Instructions

Generate all three reports in sequence:

### Step 1: Determine Dates

If user provided a date argument, use it for all reports.

If no date provided, calculate defaults:
- For nightly: Current time before 8 AM → yesterday's date; after 8 AM → today's date
- For exceptions: Yesterday's date
- For support: 1 day lookback

### Step 2: Generate Reports

Run these three MCP calls:

```
get_daily_test_summary(report_date="YYYY-MM-DD")
save_exceptions_report(report_date="YYYY-MM-DD")
get_support_summary(days=1)
```

### Step 3: Read and Summarize

Read the generated reports from `ai/.tmp/`:
- `nightly-report-YYYYMMDD.md`
- `exceptions-report-YYYYMMDD.md`
- `support-report-YYYYMMDD.md`

### Step 4: Present Summary

Provide a consolidated summary highlighting:
- **Nightly**: Any failures or leaks across all machines
- **Exceptions**: New exception types, high-frequency exceptions
- **Support**: Unanswered threads requiring attention

## Output Files

All reports saved to `ai/.tmp/`:
- `nightly-report-YYYYMMDD.md` - Full test results
- `exceptions-report-YYYYMMDD.md` - Exception details with stack traces
- `support-report-YYYYMMDD.md` - Support thread summary

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
