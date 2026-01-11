-- Query: testruns_detail
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Extended test run information with date range filtering
--
-- Parameters:
--   StartDate (TIMESTAMP) - Start of date range (inclusive)
--   EndDate (TIMESTAMP) - End of date range (inclusive)

PARAMETERS (
    StartDate TIMESTAMP,
    EndDate TIMESTAMP
)

SELECT
    t.id AS run_id,
    u.username AS computer,
    t.posttime,
    t.duration,
    t.os,
    t.passedtests,
    t.averagemem,
    t.failedtests,
    t.leakedtests,
    t.revision,
    t.githash,
    t.flagged,
    h.testname AS hung_test,
    h.pass AS hung_pass,
    h.language AS hung_language
FROM testruns t
JOIN "user" u ON t.userid = u.id
LEFT OUTER JOIN hangs h ON t.id = h.testrunid
WHERE CAST(t.posttime AS DATE) >= StartDate
  AND CAST(t.posttime AS DATE) <= EndDate
ORDER BY t.posttime DESC
