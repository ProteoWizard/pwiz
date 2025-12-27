-- Query: failures_by_date
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Test failures within a date range with computer and run info
--
-- Parameters:
--   StartDate (DATE) - Start of date range
--   EndDate (DATE) - End of date range
--
-- Used by: get_daily_test_summary(), save_test_failure_history()

PARAMETERS (StartDate DATE, EndDate DATE)

SELECT
    f.testname,
    u.username AS computer,
    f.testrunid,
    t.posttime
FROM testfails f
JOIN testruns t ON f.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE t.posttime >= StartDate
  AND t.posttime < TIMESTAMPADD('SQL_TSI_DAY', 1, EndDate)
ORDER BY t.posttime DESC, f.testname
