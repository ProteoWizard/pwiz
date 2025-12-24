-- Query: announcement_thread_posts
-- Schema: announcement
-- Description: All posts in a thread with EntityId for attachment lookups
--
-- Parameters:
--   ThreadId (INTEGER) - The thread RowId
--
-- Used by: /home/support, /home/issues/exceptions, and other Announcement containers
-- Deployed as: announcement_thread_posts

PARAMETERS (ThreadId INTEGER)

SELECT
    a.RowId,
    a.Title,
    a.FormattedBody,
    a.Created,
    a.CreatedBy,
    a.EntityId
FROM Announcement a
WHERE a.RowId = ThreadId
   OR a.Parent.RowId = ThreadId
ORDER BY a.Created
