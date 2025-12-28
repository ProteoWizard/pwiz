-- Query: wiki_page_list
-- Container: /home/software/Skyline
-- Schema: wiki
-- Description: All wiki pages with metadata (excludes Body to keep response small)

SELECT
    Name,
    Title,
    RendererType,
    Version,
    Modified
FROM CurrentWikiVersions
ORDER BY Name
