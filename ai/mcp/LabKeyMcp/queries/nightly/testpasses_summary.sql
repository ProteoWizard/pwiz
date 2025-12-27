-- Query: testpasses_summary
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Average test durations and counts for a specific run
--
-- Parameters:
--   RunId (INTEGER) - The test run ID to query
--
-- Use this to compare runs by calling twice and computing deltas client-side

PARAMETERS (RunId INTEGER)

SELECT
    p.testname,
    COUNT(*) AS pass_count,
    AVG(p.duration) AS avg_duration,
    MIN(p.duration) AS min_duration,
    MAX(p.duration) AS max_duration
FROM testpasses p
WHERE p.testrunid = RunId
GROUP BY p.testname
ORDER BY p.testname
