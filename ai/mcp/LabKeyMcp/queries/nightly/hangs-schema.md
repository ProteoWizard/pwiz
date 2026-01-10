# hangs Table Schema

**Container:** `/home/development/Nightly x64` (and other test folders)
**Schema:** `testresults`
**Table:** `hangs`

Base table for test hang detections. A hang is detected when the test log file doesn't change for over 1 hour during a run.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| id | Integer | | AI, PK, Req, RO | Primary key |
| testrunid | Integer | testresults.testruns.id | Req | FK to test run |
| testname | Text | | Req | Test class name |
| pass | Integer | | Req | Pass number when hang occurred |
| timestamp | DateTime | | Req | When hang was detected |
| language | Text | | Req | UI language |

## Notes

- A hang is first detected when the log stops updating for >1 hour
- Email alert is sent when hang is detected: `[COMPUTER (branch)] !!! TestResults alert`
- The test run is force-stopped at the scheduled end time (540 min for standard, 720 min for perf)
- SkylineNightly terminates TestRunner.exe at the run time limit
- The hang alert shows the last COMPLETED test, not the hung test (log only flushes on line completion)

## Hang Detection Context

| Duration | Meaning |
|----------|---------|
| 540 min | Standard run time limit (Nightly x64, Release Branch, Integration) |
| 720 min | Performance run time limit (Performance Tests, Release Branch Perf, Integration with Perf) |
| < Expected | TestRunner.exe quit early (crash or error) |
| > Expected | Bug in SkylineNightly (not a test bug) |
| "(hang)" in duration | Run was terminated due to a hung test |

## Related

- Hang alerts in email: Subject format `[COMPUTER (branch)] !!! TestResults alert`
- The 8:00 AM summary email shows which test was actually hung
- See `ai/docs/mcp/nightly-tests.md` for full context
