-- Query: failures_with_traces_by_date
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Test failures with full stack traces for the nightly 8AM-8AM window
--
-- Parameters:
--   WindowStart (TIMESTAMP) - Start of nightly window (e.g., 2025-12-04 08:01:00)
--   WindowEnd (TIMESTAMP) - End of nightly window (e.g., 2025-12-05 08:00:00)
--
-- Used by: save_daily_failures() - for stack trace normalization and fingerprinting
--
-- Note: Stack traces can be large. This query is designed to be saved to disk,
-- not returned directly to the conversation.
--
-- The nightly "day" runs from 8:01 AM the day before to 8:00 AM the report date.
-- For report_date 2025-12-05:
--   WindowStart = 2025-12-04 08:01:00
--   WindowEnd = 2025-12-05 08:00:00

PARAMETERS (WindowStart TIMESTAMP, WindowEnd TIMESTAMP)

SELECT
    f.testname,
    u.username AS computer,
    f.testrunid,
    t.posttime,
    f.pass AS passnum,
    f.stacktrace
FROM testfails f
JOIN testruns t ON f.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE t.posttime >= WindowStart
  AND t.posttime <= WindowEnd
ORDER BY f.testname, t.posttime DESC
