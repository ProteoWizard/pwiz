---
description: Generate daily nightly test report
---

# Daily Nightly Test Report

Generate the daily nightly test report.

**Argument**: Date in YYYY-MM-DD format (optional, defaults to current nightly day)

## Nightly Day Boundary

Nightly tests run from **8:01 AM to 8:00 AM** the next calendar day. The report_date is the END of this window:
- `report_date=2025-12-23` → queries Dec 22 8:01 AM to Dec 23 8:00 AM

**Default behavior** (no date argument):
- Before 8:00 AM: use yesterday's date (current nightly day is still in progress)
- After 8:00 AM: use today's date (current nightly day just completed)

## Quick Start

```
get_daily_test_summary(report_date="YYYY-MM-DD")
```

This queries all 6 test folders and saves a full report to `ai/.tmp/nightly-report-YYYYMMDD.md`.

## Follow-up Investigation

- **Stack trace patterns**: `save_test_failure_history(test_name, start_date, container_path)`
- **Full test logs**: `save_run_log(run_id)`

For detailed workflow and examples, see **ai/docs/nightly-test-analysis.md**.
