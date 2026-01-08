-- Query: leaks_by_date
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Memory and handle leaks within a date range
--
-- Parameters:
--   StartDate (DATE) - Start of date range
--   EndDate (DATE) - End of date range
--
-- Used by: get_daily_test_summary()
-- Note: Combines memoryleaks and handleleaks tables

PARAMETERS (StartDate DATE, EndDate DATE)

SELECT
    m.testname,
    u.username AS computer,
    'memory' AS leak_type
FROM memoryleaks m
JOIN testruns t ON m.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE t.posttime >= StartDate
  AND t.posttime < TIMESTAMPADD('SQL_TSI_DAY', 1, EndDate)

UNION ALL

SELECT
    h.testname,
    u.username AS computer,
    'handle' AS leak_type
FROM handleleaks h
JOIN testruns t ON h.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE t.posttime >= StartDate
  AND t.posttime < TIMESTAMPADD('SQL_TSI_DAY', 1, EndDate)

ORDER BY testname, leak_type
