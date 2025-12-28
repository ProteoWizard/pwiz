-- Query: testruns_detail
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Extended test run information with date range filtering
--
-- Parameters:
--   StartDate (TIMESTAMP) - Start of date range (inclusive)
--   EndDate (TIMESTAMP) - End of date range (inclusive)

PARAMETERS (StartDate TIMESTAMP, EndDate TIMESTAMP)

SELECT
    t.id,
    t.posttime,
    u.username AS computer,
    t.duration,
    t.numpassed,
    t.numfailed,
    t.numleaked,
    t.averagememory,
    t.revision
FROM testruns t
JOIN "user" u ON t.userid = u.id
WHERE CAST(t.posttime AS DATE) >= StartDate
  AND CAST(t.posttime AS DATE) <= EndDate
ORDER BY t.posttime DESC
