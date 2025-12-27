-- Query: wiki_page_content
-- Container: /home/software/Skyline
-- Schema: wiki
-- Description: Full wiki page content by name
--
-- Parameters:
--   PageName (VARCHAR) - The wiki page name (e.g., 'tutorial_method_edit')

PARAMETERS (PageName VARCHAR)

SELECT
    Name,
    Title,
    Body,
    RendererType,
    Version,
    Created,
    Modified,
    CreatedBy,
    ModifiedBy
FROM CurrentWikiVersions
WHERE Name = PageName
