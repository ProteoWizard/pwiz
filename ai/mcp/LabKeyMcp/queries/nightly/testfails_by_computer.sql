-- Query: testfails_by_computer
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Test failures aggregated by computer and test name

SELECT
    u.username AS computer,
    f.testname,
    COUNT(*) AS failure_count,
    MAX(t.posttime) AS last_seen
FROM testfails f
JOIN testruns t ON f.testrunid = t.id
JOIN "user" u ON t.userid = u.id
GROUP BY u.username, f.testname
ORDER BY failure_count DESC
