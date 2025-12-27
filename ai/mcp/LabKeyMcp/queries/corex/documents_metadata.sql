-- Query: documents_metadata
-- Container: /home/support (also /home/software/Skyline)
-- Schema: corex
-- Description: Attachment metadata excluding binary document column
--
-- Parameters:
--   ParentEntityId (VARCHAR) - EntityId of the parent post/page
--
-- IMPORTANT: This query excludes the 'document' column which can be up to 50MB

PARAMETERS (ParentEntityId VARCHAR)

SELECT
    rowid,
    parent,
    documentname,
    documentsize,
    documenttype
FROM documents
WHERE parent = ParentEntityId
