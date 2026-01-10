-- Query: leaks_history
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Memory and handle leaks with amounts and git hash for history backfill
--
-- Parameters:
--   StartDate (TIMESTAMP) - Start of date range
--   EndDate (TIMESTAMP) - End of date range
--
-- Used by: backfill_nightly_history() - for building nightly-history.json
--
-- Enhancements over leaks_by_date:
--   - Includes leak amounts (bytes for memory, handles count for handle)
--   - Includes githash for commit correlation
--   - Includes run_id for URL generation

PARAMETERS (StartDate TIMESTAMP, EndDate TIMESTAMP)

SELECT
    m.testname,
    u.username AS computer,
    m.testrunid AS run_id,
    CAST(t.posttime AS DATE) AS run_date,
    t.githash,
    'memory' AS leak_type,
    m.bytes AS leak_bytes,
    NULL AS leak_handles
FROM memoryleaks m
JOIN testruns t ON m.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE t.posttime >= StartDate
  AND t.posttime <= EndDate

UNION ALL

SELECT
    h.testname,
    u.username AS computer,
    h.testrunid AS run_id,
    CAST(t.posttime AS DATE) AS run_date,
    t.githash,
    'handle' AS leak_type,
    NULL AS leak_bytes,
    h.handles AS leak_handles
FROM handleleaks h
JOIN testruns t ON h.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE t.posttime >= StartDate
  AND t.posttime <= EndDate

ORDER BY testname, run_date
