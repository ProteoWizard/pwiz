-- Query: failures_history
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Test failures with stack traces and git hash for history backfill
--
-- Parameters:
--   StartDate (TIMESTAMP) - Start of date range
--   EndDate (TIMESTAMP) - End of date range
--
-- Used by: backfill_nightly_history() - for building nightly-history.json
--
-- Enhancements over failures_with_traces_by_date:
--   - Includes githash for commit correlation
--   - Returns run_id for URL generation

PARAMETERS (StartDate TIMESTAMP, EndDate TIMESTAMP)

SELECT
    f.testname,
    u.username AS computer,
    f.testrunid AS run_id,
    CAST(t.posttime AS DATE) AS run_date,
    t.githash,
    f.pass AS passnum,
    f.language,
    f.stacktrace
FROM testfails f
JOIN testruns t ON f.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE t.posttime >= StartDate
  AND t.posttime <= EndDate
ORDER BY t.posttime, f.testname
