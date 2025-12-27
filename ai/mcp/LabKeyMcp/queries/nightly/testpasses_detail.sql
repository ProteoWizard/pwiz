-- Query: testpasses_detail
-- Container: /home/development/Nightly x64 (and other test folders)
-- Schema: testresults
-- Description: Detailed test pass information for a specific run
--
-- Parameters:
--   RunId (INTEGER) - The test run ID to query
--
-- Note: The testpasses table has 700M+ rows - MUST filter by RunId

PARAMETERS (RunId INTEGER)

SELECT
    t.posttime AS run_date,
    u.username AS computer,
    p.testrunid,
    p.testname,
    p.pass AS passnum,
    p.handles,
    p.userandgdihandles,
    p.managedmemory,
    p.totalmemory,
    p.duration
FROM testpasses p
JOIN testruns t ON p.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE p.testrunid = RunId
ORDER BY p.testname, p.pass
