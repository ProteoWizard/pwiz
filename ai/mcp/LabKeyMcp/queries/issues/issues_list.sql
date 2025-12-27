-- issues_list: List issues with user display names
-- Container: /home/issues
-- Schema: issues
--
-- Returns issue metadata with resolved user names for AssignedTo.
-- Does NOT include Comments (large field) - use issue_with_comments for full details.
--
-- NOTE: ORDER BY in LabKey SQL is unreliable (query runs as subquery).
-- Always use API sort parameter: sort="-Modified" in select_rows().

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
    i.ResolvedBy.DisplayName AS ResolvedBy,
    i.ClosedBy.DisplayName AS ClosedBy,
    i.EntityId
FROM issues AS i
ORDER BY i.Modified DESC
