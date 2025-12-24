# testfails Table Schema

**Container:** `/home/development/Nightly x64` (and other test folders)
**Schema:** `testresults`
**Table:** `testfails`

Base table for failed test records. MCP server queries this directly via `get_run_failures()`.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| id | Integer | | AI, PK, Req, RO | Primary key |
| testrunid | Integer | testresults.testruns.id | Req | FK to test run |
| testname | Text | | Req | Test class name |
| pass | Integer | | Req | Pass number when failed |
| testid | Integer | | Req | Test identifier |
| stacktrace | Text | | | Full stack trace |
| language | Text | | | UI language |
| timestamp | DateTime | | | |

## Notes

- One row per test failure
- Links to testruns via testrunid
- stacktrace contains the full exception details for debugging
