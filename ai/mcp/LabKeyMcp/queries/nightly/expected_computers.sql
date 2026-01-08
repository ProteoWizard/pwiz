-- Query: expected_computers
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Expected computer list with trained baseline statistics for anomaly detection
--
-- No parameters - returns all active computers for this test folder
--
-- Used by: get_daily_test_summary() to detect missing computers and anomalies
-- Underlying tables: user (computer names), userdata (baseline statistics)

SELECT
    u.username AS computer,
    ud.meantestsrun,
    ud.stddevtestsrun,
    ud.meanmemory,
    ud.stddevmemory
FROM userdata ud
JOIN "user" u ON ud.userid = u.id
WHERE ud.active = true
ORDER BY u.username
