# testpasses Table Schema

**Container:** `/home/development/Nightly x64` (and other test folders)
**Schema:** `testresults`
**Table:** `testpasses`

**WARNING:** This table has 700M+ rows. Always filter by testrunid.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| id | Integer | | AI, PK, Req, RO | Primary key |
| testrunid | Integer | testresults.testruns.id | Req | FK to test run |
| testname | Text | | Req | Test class name |
| pass | Integer | | Req | Pass number (iteration) |
| testid | Integer | | Req | Test identifier |
| language | Text | | Req | UI language |
| managedmemory | Double | | Req | Managed memory (MB) |
| totalmemory | Double | | Req | Total memory (MB) |
| duration | Integer | | Req | Test duration (seconds) |
| timestamp | DateTime | | | |
| userandgdihandles | Integer | | | User/GDI handle count |
| committedmemory | Integer | | | Committed memory |
| handles | Integer | | | Handle count |

## Notes

- Each test may have multiple rows per run (iterations for leak testing)
- Use `testpasses_detail` parameterized query instead of querying directly
- Use GROUP BY testname and AVG(duration) for per-test timing analysis
