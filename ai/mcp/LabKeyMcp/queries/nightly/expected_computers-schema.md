# expected_computers Table Schema

**Container:** `/home/development/Nightly x64` (and other test folders)
**Schema:** `testresults`
**Table/Query:** `expected_computers`

Stores trained baseline statistics for each test computer, used for anomaly detection.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| computer | Text | | | Computer name |
| meantestsrun | Double | | | Average number of tests per run |
| stddevtestsrun | Double | | | Standard deviation of test count |
| meanmemory | Double | | | Average memory usage |
| stddevmemory | Double | | | Standard deviation of memory |

## Notes

- One row per expected computer in this test folder
- Baseline values are manually trained/configured
- Used by `get_daily_test_summary()` to detect anomalies
- When tests run significantly below mean, flagged as anomaly
- Missing computers (expected but no run) are reported
