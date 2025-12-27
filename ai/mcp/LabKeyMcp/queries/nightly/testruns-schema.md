# testruns Table Schema

**Container:** `/home/development/Nightly x64` (and other test folders)
**Schema:** `testresults`
**Table:** `testruns`

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| id | Integer | | AI, PK, Req, RO | Primary key |
| duration | Integer | | Req | Run duration in minutes |
| posttime | DateTime | | Req | When run was posted |
| os | Text | | Req | Operating system |
| revision | Integer | | Req | SVN revision number |
| container | Text | core.Containers.EntityId | RO | Folder reference |
| flagged | Boolean | | Req | Manual flag for attention |
| timestamp | DateTime | | | |
| userid | Integer | testresults.user.id | Req | FK to user (computer name) |
| xml | Other | | | **⚠️ LARGE** - Raw XML data, use `save_run_xml()` |
| pointsummary | Other | | | |
| passedtests | Integer | | | Count of passed tests |
| failedtests | Integer | | | Count of failed tests |
| leakedtests | Integer | | | Count of leaked tests |
| averagemem | Integer | | | Average memory usage (MB) |
| log | Other | | | **⚠️ LARGE** - Full test log, use `save_run_log()` |
| githash | Text | | | Git commit hash |

## Notes

- One row per nightly test run
- Links to testpasses, testfails, memoryleaks, handleleaks via id (as testrunid)
- Join to `user` table to get computer name from userid
