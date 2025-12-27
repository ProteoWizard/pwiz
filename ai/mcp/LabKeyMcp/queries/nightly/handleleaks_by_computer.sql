-- Query: handleleaks_by_computer
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Handle leaks aggregated by computer and test name

SELECT
    u.username AS computer,
    h.testname,
    COUNT(*) AS leak_count,
    AVG(h.handles) AS avg_handles,
    MAX(t.posttime) AS last_seen
FROM handleleaks h
JOIN testruns t ON h.testrunid = t.id
JOIN "user" u ON t.userid = u.id
GROUP BY u.username, h.testname
ORDER BY leak_count DESC
