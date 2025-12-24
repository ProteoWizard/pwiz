-- Query: compare_run_timings
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Compare average test durations between two runs to identify slowdowns
--
-- Parameters:
--   RunIdBefore (INTEGER) - The baseline run ID
--   RunIdAfter (INTEGER) - The comparison run ID
--
-- Returns tests sorted by duration increase (biggest slowdowns first)
--
-- STATUS: Draft - needs testing on server. LabKey may not support subqueries or FULL OUTER JOIN.
-- Alternative approach: Create two separate queries and join client-side.

PARAMETERS (RunIdBefore INTEGER, RunIdAfter INTEGER)

SELECT
    COALESCE(before_run.testname, after_run.testname) AS testname,
    before_run.avg_duration AS duration_before,
    after_run.avg_duration AS duration_after,
    (after_run.avg_duration - before_run.avg_duration) AS delta_seconds,
    CASE
        WHEN before_run.avg_duration > 0
        THEN 100.0 * (after_run.avg_duration - before_run.avg_duration) / before_run.avg_duration
        ELSE NULL
    END AS delta_percent
FROM (
    SELECT testname, AVG(duration) AS avg_duration
    FROM testpasses
    WHERE testrunid = RunIdBefore
    GROUP BY testname
) before_run
FULL OUTER JOIN (
    SELECT testname, AVG(duration) AS avg_duration
    FROM testpasses
    WHERE testrunid = RunIdAfter
    GROUP BY testname
) after_run
ON before_run.testname = after_run.testname
WHERE after_run.avg_duration IS NOT NULL
ORDER BY delta_seconds DESC
