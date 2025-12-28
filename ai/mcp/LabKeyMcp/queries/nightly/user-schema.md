# user Table Schema

**Container:** `/home/development/Nightly x64` (and other test folders)
**Schema:** `testresults`
**Table:** `user`

Maps userid to computer name. Joined in most queries to get human-readable computer names.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| id | Integer | | AI, PK, Req, RO | Primary key |
| username | Text | | Req | Computer name (e.g., BRENDANX-UW7) |

## Notes

- Each test computer registers as a "user" in this table
- Join: `testruns.userid = user.id`

---

# userdata Table Schema

**Table:** `userdata`

Per-computer baseline statistics for anomaly detection. This is the underlying table for `expected_computers` query.

## Columns

| Column | Type | Lookup | Attributes | Description |
|--------|------|--------|------------|-------------|
| id | Integer | | AI, PK, Req, RO | Primary key |
| userid | Integer | testresults.user.id | Req | FK to user table |
| container | Text | core.Containers.EntityId | Req, RO | Folder reference |
| meantestsrun | Double | | | Average number of tests per run |
| meanmemory | Double | | | Average memory usage |
| stddevtestsrun | Double | | | Standard deviation of test count |
| stddevmemory | Double | | | Standard deviation of memory |
| active | Boolean | | | Whether computer is actively reporting |

## Notes

- One row per computer per test folder
- Used for anomaly detection in `get_daily_test_summary()`
- The `expected_computers` query joins this with `user` to get computer names
