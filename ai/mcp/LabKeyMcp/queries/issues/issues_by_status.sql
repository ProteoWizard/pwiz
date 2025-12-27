-- issues_by_status: Filter issues by status with date range
-- Container: /home/issues
-- Schema: issues
--
-- Parameterized query for filtering by status and modification date range.
-- Useful for getting open issues or recently modified issues.
--
-- Parameters:
--   Status (VARCHAR) - Issue status ('open' or 'closed')
--   StartDate (TIMESTAMP) - Start of date range (inclusive)
--   EndDate (TIMESTAMP) - End of date range (inclusive)
--
-- NOTE: ORDER BY in LabKey SQL is unreliable (query runs as subquery).
-- Always use API sort parameter: sort="-Modified" in select_rows().

PARAMETERS (Status VARCHAR, StartDate TIMESTAMP, EndDate TIMESTAMP)

SELECT
    i.IssueId,
    i.Title,
    i.Status,
    i.Type,
    i.Area,
    i.Priority,
    i.Milestone,
    i.Resolution,
    i.Created,
    i.Modified,
    i.Resolved,
    i.Closed,
    i.AssignedTo.DisplayName AS AssignedTo,
    i.CreatedBy.DisplayName AS CreatedBy,
    i.EntityId
FROM issues AS i
WHERE i.Status = Status
  AND CAST(i.Modified AS DATE) >= StartDate
  AND CAST(i.Modified AS DATE) <= EndDate
ORDER BY i.Modified DESC
