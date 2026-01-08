-- issue_with_comments: Get single issue with all comments
-- Container: /home/issues
-- Schema: issues
--
-- Parameterized query - requires IssueId parameter.
-- Returns issue metadata joined with all comments.
-- Comment field can be large - MCP tool should save to file.

PARAMETERS (IssueId INTEGER)

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
    i.EntityId,
    c.CommentId,
    c.Created AS CommentCreated,
    c.CreatedBy.DisplayName AS CommentBy,
    c.Comment
FROM issues AS i
LEFT JOIN Comments AS c ON i.IssueId = c.IssueId
WHERE i.IssueId = IssueId
ORDER BY c.Created ASC
