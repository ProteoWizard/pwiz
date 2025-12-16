---
name: skyline-nightlytests
description: Use when investigating Skyline nightly test issues, handle leaks, memory leaks, or test failures. Activate for questions like "what's causing this leak", "why is this test failing", or "which computer shows this problem". Also use for querying test runs from skyline.ms or analyzing patterns in test results.
---

# Skyline Nightly Test Analysis

**Full documentation**: ai/docs/nightly-test-analysis.md

## MCP Tools (Quick Reference)

| Tool | Purpose |
|------|---------|
| `get_daily_test_summary(report_date)` | Daily report for all 6 folders → ai/.tmp/ |
| `save_test_failure_history(test_name, start_date, container_path)` | Stack trace pattern analysis → ai/.tmp/ |
| `save_run_log(run_id)` | Full test log for grep/search → ai/.tmp/ |
| `get_run_failures(run_id)` | Stack traces for a specific run |
| `get_run_leaks(run_id)` | Leak details for a specific run |

## Test Folders

| Folder | Duration | Branch |
|--------|----------|--------|
| Nightly x64 | 540 min | master (default) |
| Performance Tests | 720 min | master |
| Release Branch | 540 min | release |
| Release Branch Performance Tests | 720 min | release |
| Integration | 540 min | assigned |
| Integration with Perf Tests | 720 min | assigned |

## Typical Workflow

1. **Daily review**: `get_daily_test_summary("2025-12-15")` → read report
2. **Pattern analysis**: If test failed on multiple machines, use `save_test_failure_history` to check if same root cause
3. **Deep dive**: Use `save_run_log` when stack traces aren't sufficient

## Anomaly Detection

- **Short duration** (< expected) = crash or premature termination
- **Full duration + low test count** = hang
- **Stddev-based**: 3σ/4σ from trained mean flags anomalies

For server-side queries, examples, and detailed workflows, see **ai/docs/nightly-test-analysis.md**.
