# memoryleaks Table Schema

**Container:** `/home/development/Nightly x64` (and other test folders)
**Schema:** `testresults`
**Table:** `memoryleaks`

Base table for memory leak records.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| id | Integer | | AI, PK, Req, RO | Primary key |
| testrunid | Integer | testresults.testruns.id | Req | FK to test run |
| testname | Text | | Req | Test class name |
| bytes | Integer | | Req | Bytes leaked |
| type | Text | | | Leak type description |

## Notes

- One row per memory leak detected
- Links to testruns via testrunid
- Used by `leaks_by_date` query
