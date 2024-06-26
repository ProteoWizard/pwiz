SELECT Id, Name, Comment, English, Japanese, JapaneseIssue, OriginalEnglish, FileCount, CASE WHEN FileCount = 1 THEN FirstFile END AS File FROM (
SELECT i.Id, i.Name, i.Comment, i.Value as English, ja.Value as Japanese, ja.Problem as JapaneseIssue, ja.OriginalInvariantValue as OriginalEnglish, (SELECT MIN(FilePath) FROM ResourceLocation loc INNER JOIN ResxFile f ON loc.ResxFileId = f.Id WHERE loc.InvariantResourceId = i.Id) AS FirstFile, (SELECT COUNT(Id) FROM ResourceLocation loc WHERE loc.InvariantResourceId = i.Id) as FileCount
FROM InvariantResource i 
LEFT JOIN LocalizedResource ja ON i.Id = ja.InvariantResourceId AND ja.Language = 'ja' 
WHERE i.Name NOT LIKE '>>%' AND i.Type is NULL AND i.MimeType is NULL AND ja.Problem IS NOT NULL);
