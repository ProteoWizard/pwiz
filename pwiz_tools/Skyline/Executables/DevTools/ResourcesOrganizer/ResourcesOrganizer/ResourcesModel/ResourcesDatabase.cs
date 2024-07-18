/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using CsvHelper;
using NHibernate;
using ResourcesOrganizer.DataModel;

namespace ResourcesOrganizer.ResourcesModel
{
    public record ResourcesDatabase
    {
        public static readonly ResourcesDatabase Empty = new();
        public ImmutableDictionary<string, ResourcesFile> ResourcesFiles { get; init; }
            = ImmutableDictionary<string, ResourcesFile>.Empty;

        public static ResourcesDatabase ReadFile(string path, HashSet<string> exclude)
        {
            var extension = Path.GetExtension(path);
            if (extension.Equals(".db", StringComparison.OrdinalIgnoreCase))
            {
                return ReadDatabase(path);
            }
            if (extension.Equals(".resx", StringComparison.OrdinalIgnoreCase))
            {
                var resourcesFile = ResourcesFile.Read(path, path);
                return new ResourcesDatabase
                {
                    ResourcesFiles = ImmutableDictionary<string, ResourcesFile>.Empty.Add(path, resourcesFile)
                };
            }
            if (Directory.Exists(path))
            {
                var files = new Dictionary<string, ResourcesFile>();
                AddFolder(files, path, path, exclude);
                return new ResourcesDatabase
                {
                    ResourcesFiles = files.ToImmutableDictionary()
                };
            }

            throw new ArgumentException("I don't know how to read {0}", path);
        }

        public static ResourcesDatabase ReadDatabase(string databasePath)
        {
            using var sessionFactory = SessionFactoryFactory.CreateSessionFactory(databasePath, false);
            using var session = sessionFactory.OpenStatelessSession();

            return ReadDatabase(session);
        }

        public static ResourcesDatabase ReadDatabase(IStatelessSession session)
        {
            var invariantResources = session.Query<InvariantResource>()
                .ToDictionary(resource => resource.Id!.Value);
            var localizedResources = session.Query<LocalizedResource>()
                .ToLookup(localizedResource => localizedResource.InvariantResourceId);
            var resourcesFiles = new Dictionary<string, ResourcesFile>();
            foreach (var fileGroup in session.Query<ResourceLocation>().GroupBy(location => location.ResxFileId))
            {
                var resxFile = session.Get<ResxFile>(fileGroup.Key);
                var xmlContent = resxFile.XmlContent!;

                var entries = new List<ResourceEntry>();
                foreach (var resourceLocation in fileGroup.OrderBy(loc => loc.SortIndex))
                {
                    var localizedValues = new Dictionary<string, LocalizedValue>();
                    foreach (var localizedResource in localizedResources[resourceLocation.InvariantResourceId])
                    {
                        localizedValues.Add(localizedResource.Language!, new LocalizedValue(
                            localizedResource.Value!,
                            LocalizationIssue.ParseIssue(localizedResource.Issue)));
                    }
                    var invariantResource = invariantResources[resourceLocation.InvariantResourceId];
                    var entry = new ResourceEntry(resourceLocation.Name!, invariantResource.GetKey())
                    {
                        XmlSpace = invariantResource.XmlSpace,
                        LocalizedValues = localizedValues.ToImmutableDictionary(),
                        Position = resourceLocation.Position
                    };

                    entries.Add(entry);
                }
                resourcesFiles.Add(resxFile.FilePath!, new ResourcesFile(resxFile.FilePath!)
                {
                    Entries = entries.ToImmutableList(),
                    XmlContent = xmlContent
                });
            }

            return new ResourcesDatabase
            {
                ResourcesFiles = resourcesFiles.ToImmutableDictionary()
            };
        }

        public IEnumerable<string> GetLanguages()
        {
            return ResourcesFiles.Values
                .SelectMany(file => file.Entries.SelectMany(entry => entry.LocalizedValues.Keys)).Distinct()
                .OrderBy(lang => lang);
        }

        public void SaveAtomic(string path)
        {
            using var fileSaver = new FileSaver(path);
            Save(fileSaver.SafeName!);
            fileSaver.Commit();
        }

        public void Save(string path)
        {
            using var sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, true);
            using var session = sessionFactory.OpenStatelessSession();
            var transaction = session.BeginTransaction();
            var invariantResources = SaveInvariantResources(session);
            SaveLocalizedResources(session, invariantResources);
            foreach (var resourceFile in ResourcesFiles)
            {
                var resxFile = new ResxFile
                {
                    FilePath = resourceFile.Key,
                    XmlContent = resourceFile.Value.XmlContent
                };
                session.Insert(resxFile);

                SaveResourcesFile(session, invariantResources, resxFile.Id!.Value, resourceFile.Key, resourceFile.Value);
            }
            transaction.Commit();
        }

        public void ExportResx(string path, bool overrideAll)
        {
            using var stream = File.Create(path);
            using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
            foreach (var file in ResourcesFiles)
            {
                var entry = zipArchive.CreateEntry(file.Key);
                using (var entryStream = entry.Open())
                {
                    using var writer = new StreamWriter(entryStream, TextUtil.Utf8Encoding);
                    writer.Write(TextUtil.SerializeDocument(file.Value.ExportResx(null, overrideAll)));
                }
                foreach (var language in file.Value.Entries
                             .SelectMany(resourceEntry => resourceEntry.LocalizedValues.Keys).Distinct())
                {
                    var folder = Path.GetDirectoryName(file.Key) ?? string.Empty;
                    var fileName = Path.GetFileNameWithoutExtension(file.Key) + "." + language + Path.GetExtension(file.Key);
                    var localizedEntry = zipArchive.CreateEntry(Path.Combine(folder, fileName));
                    using var localizedStream = localizedEntry.Open();
                    using var writer = new StreamWriter(localizedStream, TextUtil.Utf8Encoding);
                    writer.Write(TextUtil.SerializeDocument(file.Value.ExportResx(language, overrideAll)));
                }
            }
        }

        private IDictionary<InvariantResourceKey, long> SaveInvariantResources(IStatelessSession session)
        {
            var result = new Dictionary<InvariantResourceKey, long>();
            foreach ((InvariantResourceKey key, List<ResourceEntry> entries) in GetInvariantResources().OrderBy(kvp=>kvp.Key))
            {
                var xmlSpace = entries.Select(entry => entry.XmlSpace).Distinct().Single();

                var invariantResource = new InvariantResource
                {
                    Comment = key.Comment,
                    Name = key.Name,
                    File = key.File,
                    Type = key.Type,
                    MimeType = key.MimeType,
                    Value = key.Value,
                    XmlSpace = xmlSpace
                };

                session.Insert(invariantResource);
                result.Add(key, invariantResource.Id!.Value);
            }

            return result;
        }

        private void SaveLocalizedResources(IStatelessSession session,
            IDictionary<InvariantResourceKey, long> invariantResources)
        {
            foreach (var entryGroup in GetInvariantResources())
            {
                var invariantResourceId = invariantResources[entryGroup.Key];
                foreach (var localizedEntryGroup in entryGroup.Value.SelectMany(entry => entry.LocalizedValues).GroupBy(kvp => kvp.Key))
                {
                    var translations = localizedEntryGroup.Select(kvp => kvp.Value).Distinct().ToList();
                    var translatedTexts =
                        translations.Select(value => value.Value).Distinct().ToList();
                    var issues = translations.Select(localizedValue => localizedValue.Issue?.GetIssueDetails(null))
                        .OfType<string>().Distinct().ToList();

                    if (translations.Count > 1)
                    {
                        Console.Error.WriteLine("Warning: {0} had multiple translations: {1}", entryGroup.Key, TextUtil.QuoteAndSeparate(translatedTexts));
                    }

                    if (issues.Count > 1)
                    {
                        Console.Error.WriteLine("Warning: {0} had multiple issues: {1}", entryGroup.Key, TextUtil.QuoteAndSeparate(issues));
                    }

                    var localizedResource = new LocalizedResource
                    {
                        InvariantResourceId = invariantResourceId,
                        Language = localizedEntryGroup.Key,
                        Issue = issues.FirstOrDefault(),
                        Value = translatedTexts.FirstOrDefault()
                    };
                    session.Insert(localizedResource);
                }
            }
        }

        private void SaveResourcesFile(IStatelessSession session, IDictionary<InvariantResourceKey, long> invariantResources, long resxFileId, string filePath, ResourcesFile resourcesFile)
        {
            int sortIndex = 0;
            foreach (var entry in resourcesFile.Entries)
            {
                long invariantResourceId;
                if (!invariantResources.TryGetValue(entry.Invariant.FileQualified(filePath), out invariantResourceId) &&
                    !invariantResources.TryGetValue(entry.Invariant, out invariantResourceId))
                {
                    throw new KeyNotFoundException();
                }
                var resourceLocation = new ResourceLocation
                {
                    ResxFileId = resxFileId,
                    InvariantResourceId = invariantResourceId,
                    Name = entry.Name,
                    SortIndex = ++sortIndex,
                    Position = entry.Position
                };
                session.Insert(resourceLocation);
            }
        }

        public IDictionary<InvariantResourceKey, List<ResourceEntry>> GetInvariantResources()
        {
            var dictionary = new Dictionary<InvariantResourceKey, List<ResourceEntry>>();
            foreach (var group in ResourcesFiles.SelectMany(resourcesKvp => resourcesKvp.Value.Entries.Select(entry=>Tuple.Create(resourcesKvp.Key, entry)))
                         .GroupBy(tuple => tuple.Item2.Invariant))
            {
                var totalCount = group.Count();
                var groups = group.GroupBy(tuple => tuple.Item2.Normalize()).ToList();
                foreach (var compatibleGroup in groups)
                {
                    var entries = compatibleGroup.Select(tuple => tuple.Item2).ToList();
                    if (compatibleGroup.Key.Invariant.IsLocalizableText && entries.Count > totalCount / 2)
                    {
                        dictionary.Add(group.Key, entries);
                    }
                    else
                    {
                        foreach (var tuple in compatibleGroup)
                        {
                            var key = tuple.Item2.Invariant.FileQualified(tuple.Item1);
                            dictionary.Add(key, [tuple.Item2]);
                        }
                    }
                }
            }

            return dictionary;
        }

        [Pure]
        public ResourcesDatabase Add(ResourcesDatabase database)
        {
            var resourcesFiles = ResourcesFiles.ToDictionary();

            foreach (var resourcesFile in database.ResourcesFiles)
            {
                if (resourcesFiles.TryGetValue(resourcesFile.Key, out var existing))
                {
                    resourcesFiles[resourcesFile.Key] = existing.Add(resourcesFile.Value);
                }
                else
                {
                    resourcesFiles.Add(resourcesFile.Key, resourcesFile.Value);
                }
            }

            return this with { ResourcesFiles = resourcesFiles.ToImmutableDictionary() };
        }

        [Pure]
        public ResourcesDatabase AddFile(ResourcesFile file)
        {
            return this with { ResourcesFiles = ResourcesFiles.Add(file.RelativePath, file) };
        }

        [Pure]
        public ResourcesDatabase ImportLastVersion(ResourcesDatabase reviewedDb, IList<string> languages, out int reviewedCount, out int totalCount)
        {
            reviewedCount = 0;
            totalCount = 0;
            var reviewedResources = reviewedDb.GetInvariantResources();
            var reviewedResourcesWithFileWithoutText = reviewedDb.ResourcesFiles.Values.SelectMany(file =>
                file.Entries.Select(entry =>
                    Tuple.Create(entry.Invariant with { File = file.RelativePath, Value = string.Empty }, entry)))
                .ToLookup(tuple=>tuple.Item1, tuple=>tuple.Item2);
            var reviewedResourcesWithoutText = reviewedDb.ResourcesFiles.Values.SelectMany(file => file.Entries)
                .ToLookup(entry => entry.Invariant with
                {
                    Value = string.Empty, File = entry.Invariant.IsLocalizableText ? null : entry.Invariant.File
                });
            var newFiles = new Dictionary<string, ResourcesFile>();
            foreach (var resourcesEntry in ResourcesFiles.ToList())
            {
                var newEntries = new List<ResourceEntry>();
                foreach (var entry in resourcesEntry.Value.Entries)
                {
                    var localizedValues = new Dictionary<string, LocalizedValue>();
                    foreach (var language in languages)
                    {
                        totalCount++;
                        LocalizedValue? currentValue;
                        var fileQualifiedKey = entry.Invariant.FileQualified(resourcesEntry.Key);
                        if (!reviewedResources.TryGetValue(fileQualifiedKey, out var reviewedEntries))
                        {
                            if (!reviewedResources.TryGetValue(entry.Invariant, out reviewedEntries))
                            {
                                reviewedEntries = [];
                            }
                        }

                        var reviewedTranslations = reviewedEntries.Select(reviewedEntry =>
                                reviewedEntry.GetTranslation(language)?.Value)
                            .OfType<string>().Distinct().ToList();

                        if (reviewedTranslations.Count == 1)
                        {
                            reviewedCount++;
                            localizedValues.Add(language, new LocalizedValue(reviewedTranslations[0]));
                            continue;
                        }

                        if (reviewedTranslations.Count > 1)
                        {
                            currentValue = entry.LocalizedValueIssue(LocalizationIssue.InconsistentTranslation);
                        }
                        else if (reviewedEntries.Any())
                        {
                            currentValue = entry.LocalizedValueIssue(LocalizationIssue.MissingTranslation);
                        }
                        else
                        {
                            var reviewedFuzzyMatches = reviewedResourcesWithFileWithoutText[
                                entry.Invariant with
                                {
                                    Value = string.Empty,
                                    File = resourcesEntry.Key
                                }].ToList();
                            if (reviewedFuzzyMatches.Count == 0)
                            {
                                reviewedFuzzyMatches = reviewedResourcesWithoutText[
                                    entry.Invariant with { Value = string.Empty }].ToList();
                            }
                            var oldEnglishValues = reviewedFuzzyMatches
                                .Select(oldEntry => oldEntry.Invariant.Value).Distinct().ToList();

                            if (oldEnglishValues.Count == 1)
                            {
                                var reviewedFuzzyTranslations = reviewedFuzzyMatches
                                    .Select(oldEntry => oldEntry.GetTranslation(language)?.Value).OfType<string>()
                                    .Distinct().ToList();
                                if (reviewedFuzzyTranslations.Count == 1)
                                {
                                    if (oldEnglishValues[0] == reviewedFuzzyTranslations[0])
                                    {
                                        // Old translation used to be the same as the English, just use the
                                        // new English
                                        localizedValues.Add(language, new LocalizedValue(entry.Invariant.Value));
                                    }
                                    else
                                    {
                                        localizedValues.Add(language,
                                            new LocalizedValue(entry.Invariant.Value,
                                                new EnglishTextChanged(oldEnglishValues[0], reviewedFuzzyTranslations[0])));
                                    }
                                    continue;
                                }
                            }

                            if (oldEnglishValues.Count == 0)
                            {
                                currentValue = entry.LocalizedValueIssue(LocalizationIssue.NewResource);
                            }
                            else
                            {
                                currentValue = entry.LocalizedValueIssue(LocalizationIssue.MissingTranslation);
                            }
                        }

                        localizedValues.Add(language, currentValue);
                    }

                    newEntries.Add(entry with {LocalizedValues = localizedValues.ToImmutableDictionary()});
                }
                var newResourcesFile = resourcesEntry.Value with {Entries = newEntries.ToImmutableList()};
                newFiles.Add(resourcesEntry.Key, newResourcesFile);
            }

            return this with { ResourcesFiles = newFiles.ToImmutableDictionary() };
        }

        private static void AddFolder(Dictionary<string, ResourcesFile> files, string fullPath, string relativePath, HashSet<string> exclude)
        {
            if (exclude.Contains(relativePath))
            {
                return;
            }
            var directoryInfo = new DirectoryInfo(fullPath);
            foreach (var file in directoryInfo.GetFiles())
            {
                var filePath = Path.Combine(relativePath, file.Name);
                if (exclude.Contains(filePath))
                {
                    continue;
                }
                if (!ResourcesFile.IsInvariantResourceFile(file.FullName))
                {
                    continue;
                }

                var resourcesFile = ResourcesFile.Read(file.FullName, filePath);
                files[filePath] = resourcesFile;
            }

            foreach (var folder in directoryInfo.GetDirectories())
            {
                AddFolder(files, folder.FullName, Path.Combine(relativePath, folder.Name), exclude);
            }
        }

        public void ExportLocalizationCsv(string path, string language, out int entryCount)
        {
            var records = new List<LocalizationCsvRecord>();
            foreach (var invariantEntry in GetInvariantResources().OrderBy(kvp=>kvp.Key))
            {
                var invariantKey = invariantEntry.Key;
                if (!invariantKey.IsLocalizableText)
                {
                    continue;
                }

                var issues = invariantEntry.Value.Select(value => value.GetTranslation(language)?.Issue)
                    .OfType<LocalizationIssue>().Distinct().ToList();
                if (issues.Count > 1)
                {
                    Console.Error.WriteLine("Multiple different localization issues found for {0}: {1}", invariantEntry.Key, TextUtil.LineSeparate(issues.Select(issue=>issue.GetIssueDetails(null))));
                }

                var issue = issues.FirstOrDefault();
                var localizedValues = invariantEntry.Value.Select(value => value.GetTranslation(language))
                    .OfType<LocalizedValue>().ToList();
                var localizedText = localizedValues.Select(value => value.Value).FirstOrDefault();
                if (localizedText == null || issue != null)
                {
                    var csvRecord = new LocalizationCsvRecord
                    {
                        Name = invariantKey.Name!, 
                        Comment = invariantKey.Comment!,
                        English = invariantKey.Value,
                        File = invariantEntry.Key.File ?? string.Empty,
                        FileCount = invariantEntry.Value.Count
                    };
                    if (localizedText != invariantEntry.Key.Value)
                    {
                        csvRecord = csvRecord with { Translation = localizedText ?? string.Empty };
                    }

                    if (issue != null)
                    {
                        csvRecord = issue.StoreInCsvRecord(csvRecord);
                    }
                    records.Add(csvRecord);
                }
            }
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csvWriter.WriteRecords(records);
            entryCount = records.Count;
        }

        public ResourcesDatabase ImportLocalizationCsv(LanguageFilePath languageFilePath, out int totalEntryCount, out int totalMatchCount, out int totalChangeCount)
        {
            totalMatchCount = 0;
            totalChangeCount = 0;
            using var reader = new StreamReader(languageFilePath.FilePath);
            using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
            var allRecords = csvReader.GetRecords<LocalizationCsvRecord>().ToList();
            totalEntryCount = allRecords.Count;
            var recordsByName = allRecords.ToLookup(record => record.Name);
            var newFiles = new Dictionary<string, ResourcesFile>();
            foreach (var resourceFileEntry in ResourcesFiles)
            {
                var newFile = resourceFileEntry.Value.ImportLocalizationRecords(languageFilePath.Language, recordsByName,
                    out int matchCount, out int changeCount);
                totalMatchCount += matchCount;
                totalChangeCount += changeCount;
                newFiles.Add(resourceFileEntry.Key, newFile);
            }

            return this with { ResourcesFiles = newFiles.ToImmutableDictionary() };
        }
    }
}
