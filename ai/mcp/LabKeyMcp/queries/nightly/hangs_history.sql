-- Query: hangs_history
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Test hangs with git hash for history backfill
--
-- Parameters:
--   StartDate (TIMESTAMP) - Start of date range
--   EndDate (TIMESTAMP) - End of date range
--
-- Used by: backfill_nightly_history() - for building nightly-history.json
--
-- Notes:
--   - A hang is recorded when the test log stops updating for >1 hour
--   - The testname is the LAST COMPLETED test, not necessarily the hung test
--     (because the log only flushes on line completion)
--   - See hangs-schema.md for full context

PARAMETERS (StartDate TIMESTAMP, EndDate TIMESTAMP)

SELECT
    h.testname,
    u.username AS computer,
    h.testrunid AS run_id,
    CAST(t.posttime AS DATE) AS run_date,
    t.githash,
    h.pass AS passnum,
    h.language
FROM hangs h
JOIN testruns t ON h.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE t.posttime >= StartDate
  AND t.posttime <= EndDate
ORDER BY t.posttime, h.testname
