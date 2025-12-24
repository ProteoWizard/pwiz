-- Query: compare_run_timings
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Compare average test durations between two runs to identify slowdowns
--
-- Parameters:
--   RunIdBefore (INTEGER) - The baseline run ID
--   RunIdAfter (INTEGER) - The comparison run ID
--
-- Returns tests sorted by total time impact (passes * delta_seconds)
-- New tests (only in after) appear at top with NULL before values
-- Removed tests (only in before) appear at bottom with NULL after values
-- Rounds durations to seconds and percentages to integers
--
-- STATUS: Working

PARAMETERS (RunIdBefore INTEGER, RunIdAfter INTEGER)

SELECT
    COALESCE(before_run.testname, after_run.testname) AS testname,
    COALESCE(after_run.pass_count, before_run.pass_count) AS passes,
    ROUND(before_run.avg_duration) AS duration_before,
    ROUND(after_run.avg_duration) AS duration_after,
    ROUND(after_run.avg_duration - before_run.avg_duration) AS delta_avg,
    ROUND((after_run.avg_duration - before_run.avg_duration) * COALESCE(after_run.pass_count, before_run.pass_count)) AS delta_total,
    CASE
        WHEN before_run.avg_duration > 0
        THEN ROUND(((after_run.avg_duration - before_run.avg_duration) * 100.0) / before_run.avg_duration)
        ELSE NULL
    END AS delta_percent
FROM (
    SELECT testname, COUNT(*) AS pass_count, AVG(duration) AS avg_duration
    FROM testpasses
    WHERE testrunid = RunIdBefore
    GROUP BY testname
) before_run
FULL OUTER JOIN (
    SELECT testname, COUNT(*) AS pass_count, AVG(duration) AS avg_duration
    FROM testpasses
    WHERE testrunid = RunIdAfter
    GROUP BY testname
) after_run
ON before_run.testname = after_run.testname
ORDER BY delta_total DESC
