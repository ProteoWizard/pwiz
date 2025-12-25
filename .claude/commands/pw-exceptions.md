---
description: Generate daily exception report
---

# Daily Exception Report

Generate the daily exception report for Skyline user submissions.

**Argument**: Date in YYYY-MM-DD format (optional, defaults to yesterday)

## Exception Day Boundary

Exceptions are reported for a **12:00 AM to 11:59 PM** calendar day window:
- `report_date=2025-12-24` → queries all exceptions created on Dec 24

**Default behavior** (no date argument):
- Use yesterday's date (gives a complete 24-hour report)

## Quick Start

```
save_exceptions_report(report_date="YYYY-MM-DD")
```

This queries all exceptions for the day and saves a full report to `ai/.tmp/exceptions-report-YYYYMMDD.md`.

## Report Contents

- Summary table with RowId, Title, Status, Created time
- Full exception details including:
  - Stack traces (FormattedBody)
  - User email and comments
  - Skyline version
  - Created/Modified dates

## Follow-up Investigation

For a specific exception:
```
get_exception_details(exception_id)
```

For detailed triage workflow, see **ai/docs/exception-triage-system.md**.
