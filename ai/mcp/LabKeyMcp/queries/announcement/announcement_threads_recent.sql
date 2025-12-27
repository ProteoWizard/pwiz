-- Query: announcement_threads_recent
-- Schema: announcement
-- Description: Recent threads with metadata (works in any Announcement container)
--
-- Parameters:
--   DaysBack (INTEGER, default 30) - Number of days back to query
--
-- Used by: /home/support, /home/issues/exceptions, and other Announcement containers
-- Deployed as: announcement_threads_recent

PARAMETERS (DaysBack INTEGER DEFAULT 30)

SELECT
    RowId,
    Title,
    Created,
    CreatedBy,
    ResponseCount
FROM Threads
WHERE Created > TIMESTAMPADD('SQL_TSI_DAY', -DaysBack, NOW())
ORDER BY Created DESC
