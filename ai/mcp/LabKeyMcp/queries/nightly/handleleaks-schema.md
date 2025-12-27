# handleleaks Table Schema

**Container:** `/home/development/Nightly x64` (and other test folders)
**Schema:** `testresults`
**Table:** `handleleaks`

Base table for handle leak records.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| id | Integer | | AI, PK, Req, RO | Primary key |
| testrunid | Integer | testresults.testruns.id | Req | FK to test run |
| testname | Text | | Req | Test class name |
| handles | Double | | | Handle count leaked |
| type | Text | | | Leak type description |

## Notes

- One row per handle leak detected
- Links to testruns via testrunid
- Used by `handleleaks_by_computer` query
