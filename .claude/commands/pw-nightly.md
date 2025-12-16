# Daily Nightly Test Report

Generate the daily nightly test report.

## Quick Start

```
get_daily_test_summary(report_date="2025-12-15")
```

This queries all 6 test folders and saves a full report to `ai/.tmp/nightly-report-YYYYMMDD.md`.

## Follow-up Investigation

- **Stack trace patterns**: `save_test_failure_history(test_name, start_date, container_path)`
- **Full test logs**: `save_run_log(run_id)`

For detailed workflow and examples, see **ai/docs/nightly-test-analysis.md**.
