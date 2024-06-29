SELECT i.Name, i.Comment, i.Value as English, ja.Value as Japanese, ja.Comment as JapaneseComment, ja.OriginalInvariantValue as OriginalEnglish, (SELECT MIN(FilePath) FROM ResourceLocation loc INNER JOIN ResxFile f ON loc.ResxFileId = f.Id WHERE loc.InvariantResourceId = i.Id) AS FirstFile, (SELECT COUNT(Id) FROM ResourceLocation loc WHERE loc.InvariantResourceId = i.Id) as FileCount
FROM InvariantResource i 
LEFT JOIN LocalizedResource ja ON i.Id = ja.InvariantResourceId AND ja.Language = 'ja' 
WHERE i.Name NOT LIKE '>>%' AND i.Type is NULL AND i.MimeType is NULL AND (ja.Comment IS NOT NULL or zh.Comment IS NOT NULL);
