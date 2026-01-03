-- Query: all_computers
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: All computers with active status (for status management)
--
-- No parameters - returns all computers regardless of active status
--
-- Used by: list_computer_status() MCP tool
-- Underlying tables: user (computer names), userdata (active flag, baseline statistics)
--
-- LEFT OUTER JOIN: A computer in `user` may not have `userdata` yet
-- (results posted before training data configured)

SELECT
    u.username AS computer,
    u.id AS user_id,
    COALESCE(ud.active, true) AS active,
    ud.meantestsrun,
    ud.stddevtestsrun,
    ud.meanmemory,
    ud.stddevmemory
FROM "user" u
LEFT OUTER JOIN userdata ud ON ud.userid = u.id
ORDER BY u.username
