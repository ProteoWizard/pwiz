SELECT Name, Comment, English, Chinese, ChineseIssue, OriginalEnglish, FileCount, CASE WHEN FileCount = 1 THEN FirstFile END AS File FROM (
SELECT i.Name, i.Comment, i.Value as English, zh.Value as Chinese, zh.Problem as ChineseIssue, zh.OriginalInvariantValue as OriginalEnglish, (SELECT MIN(FilePath) FROM ResourceLocation loc INNER JOIN ResxFile f ON loc.ResxFileId = f.Id WHERE loc.InvariantResourceId = i.Id) AS FirstFile, (SELECT COUNT(Id) FROM ResourceLocation loc WHERE loc.InvariantResourceId = i.Id) as FileCount
FROM InvariantResource i 
LEFT JOIN LocalizedResource zh ON i.Id = zh.InvariantResourceId AND zh.Language = 'zh-CHS' 
WHERE i.Name NOT LIKE '>>%' AND i.Type is NULL AND i.MimeType is NULL AND zh.Problem IS NOT NULL);
