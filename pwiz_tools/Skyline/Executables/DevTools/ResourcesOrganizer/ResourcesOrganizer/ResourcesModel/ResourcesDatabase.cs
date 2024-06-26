using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.IO.Compression;
using NHibernate;
using ResourcesOrganizer.DataModel;

namespace ResourcesOrganizer.ResourcesModel
{
    public record ResourcesDatabase
    {
        public static readonly ResourcesDatabase Empty = new();
        public ImmutableDictionary<string, ResourcesFile> ResourcesFiles { get; init; }
            = ImmutableDictionary<string, ResourcesFile>.Empty;

        /// <summary>
        /// If true, then untranslated entries are treated the same as entries where the translated text
        /// is the same as the English
        /// </summary>
        public bool OverrideAll { get; init; }

        public static ResourcesDatabase ReadFile(string path, HashSet<string> exclude)
        {
            var extension = Path.GetExtension(path);
            if (extension.Equals(".db", StringComparison.OrdinalIgnoreCase))
            {
                return ReadDatabase(path);
            }
            if (extension.Equals(".resx", StringComparison.OrdinalIgnoreCase))
            {
                var resourcesFile = ResourcesFile.Read(path);
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
                        localizedValues.Add(localizedResource.Language!, new LocalizedValue
                        {
                            Value = localizedResource.Value!,
                            Problem = localizedResource.Problem
                        });
                    }
                    var invariantResource = invariantResources[resourceLocation.InvariantResourceId];
                    var entry = new ResourceEntry
                    {
                        Name = resourceLocation.Name!,
                        Invariant = invariantResource.GetKey(),
                        MimeType = invariantResource.MimeType,
                        XmlSpace = invariantResource.XmlSpace,
                        LocalizedValues = localizedValues.ToImmutableDictionary(),
                        Position = resourceLocation.Position
                    };
                    entries.Add(entry);
                }
                resourcesFiles.Add(resxFile.FilePath!, new ResourcesFile
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

        public void SaveAtomic(string path)
        {
            using var fileSaver = new FileSaver(path);
            Save(fileSaver.SafeName);
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

        public void Export(string path)
        {
            using var stream = File.Create(path);
            using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
            foreach (var file in ResourcesFiles)
            {
                var entry = zipArchive.CreateEntry(file.Key);
                using (var entryStream = entry.Open())
                {
                    file.Value.ExportResx(entryStream, null, OverrideAll);
                }
                foreach (var language in file.Value.Entries
                             .SelectMany(resourceEntry => resourceEntry.LocalizedValues.Keys).Distinct())
                {
                    var folder = Path.GetDirectoryName(file.Key) ?? string.Empty;
                    var fileName = Path.GetFileNameWithoutExtension(file.Key) + "." + language + Path.GetExtension(file.Key);
                    var localizedEntry = zipArchive.CreateEntry(Path.Combine(folder, fileName));
                    using var localizedStream = localizedEntry.Open();
                    file.Value.ExportResx(localizedStream, language, OverrideAll);
                }
            }
        }

        private IDictionary<InvariantResourceKey, long> SaveInvariantResources(IStatelessSession session)
        {
            var result = new Dictionary<InvariantResourceKey, long>();
            foreach ((InvariantResourceKey key, List<ResourceEntry> entries) in GetInvariantResources().OrderBy(kvp=>kvp.Key))
            {
                var mimeType = entries.Select(entry => entry.MimeType).Distinct().Single();
                var xmlSpace = entries.Select(entry => entry.XmlSpace).Distinct().Single();

                var invariantResource = new InvariantResource
                {
                    Comment = key.Comment,
                    Name = key.Name,
                    File = key.File,
                    Type = key.Type,
                    Value = key.Value,
                    MimeType = mimeType,
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
                    if (translations.Count > 1)
                    {
                        Console.Error.WriteLine("{0} was translated into {1} as all of the following: {2}", entryGroup.Key, localizedEntryGroup.Key, string.Join(",", translations.Select(t=>TextUtil.Quote(t.Value))));
                    }

                    var localizedResource = new LocalizedResource
                    {
                        InvariantResourceId = invariantResourceId,
                        Language = localizedEntryGroup.Key,
                        Value = translations[0].Value,
                        Problem = translations[0].Problem,
                        OriginalInvariantValue = translations[0].OriginalInvariantValue
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
                    SortIndex= ++sortIndex,
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
                var groups = group.GroupBy(tuple => tuple.Item2.Normalize(OverrideAll)).ToList();
                foreach (var compatibleGroup in groups)
                {
                    var entries = compatibleGroup.Select(tuple => tuple.Item2).ToList();
                    if (entries.Count > totalCount / 2)
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
        public ResourcesDatabase ImportTranslations(ResourcesDatabase oldDb, IList<string> languages)
        {
            var oldResources = oldDb.GetInvariantResources();
            var oldResourcesWithoutText = oldDb.ResourcesFiles.Values.SelectMany(file=>file.Entries)
                .ToLookup(entry => entry.Invariant with {Value = string.Empty, File = null});
            var newFiles = new Dictionary<string, ResourcesFile>();
            foreach (var resourcesEntry in ResourcesFiles.ToList())
            {
                var newEntries = new List<ResourceEntry>();
                foreach (var entry in resourcesEntry.Value.Entries)
                {
                    var localizedValues = new Dictionary<string, LocalizedValue>();
                    foreach (var language in languages)
                    {
                        var fileQualifiedKey = entry.Invariant.FileQualified(resourcesEntry.Key);
                        if (!oldResources.TryGetValue(fileQualifiedKey, out var oldEntries))
                        {
                            if (!oldResources.TryGetValue(entry.Invariant, out oldEntries))
                            {
                                oldEntries = [];
                            }
                        }

                        var oldTranslations = oldEntries.Select(oldEntry => oldEntry.GetTranslation(language)?.Value)
                            .OfType<string>().Distinct().ToList();

                        if (oldTranslations.Count == 1)
                        {
                            var localizedValue = oldTranslations.First();
                            if (string.IsNullOrEmpty(localizedValue) && !string.IsNullOrEmpty(entry.Invariant.Value))
                            {
                                localizedValues.Add(language, new LocalizedValue
                                {
                                    Value = entry.Invariant.Value,
                                    Problem = LocalizationComments.EmptyLocalizedResource
                                });
                                continue;
                            }
                            localizedValues.Add(language, new LocalizedValue { Value = oldTranslations.First() });
                            continue;
                        }

                        string problem;
                        if (oldTranslations.Count > 1)
                        {
                            problem = LocalizationComments.InconsistentTranslation;
                        }
                        else if (oldEntries.Any())
                        {
                            problem = LocalizationComments.MissingTranslation;
                        }
                        else
                        {
                            var oldFuzzyMatches = oldResourcesWithoutText[
                                    entry.Invariant with { Value = string.Empty }].ToList();
                            var oldEnglishValues = oldFuzzyMatches
                                .Select(oldEntry => oldEntry.Invariant.Value).Distinct().ToList();

                            if (oldEnglishValues.Count == 1)
                            {
                                var oldFuzzyTranslations = oldFuzzyMatches
                                    .Select(oldEntry => oldEntry.GetTranslation(language)?.Value).OfType<string>()
                                    .Distinct().ToList();
                                if (oldFuzzyTranslations.Count == 1)
                                {
                                    localizedValues.Add(language, new LocalizedValue
                                    {
                                        Problem = LocalizationComments.EnglishTextChanged,
                                        OriginalInvariantValue = oldEnglishValues[0],
                                        Value = oldFuzzyTranslations[0]
                                    });
                                }
                                continue;
                            }

                            if (oldEnglishValues.Count == 0)
                            {
                                problem = LocalizationComments.NewResource;
                            }
                            else
                            {
                                problem = LocalizationComments.MissingTranslation;
                            }
                        }
                        localizedValues.Add(language, new LocalizedValue{Value = string.Empty, Problem = problem});
                    }

                    newEntries.Add(entry with {LocalizedValues = localizedValues.ToImmutableDictionary()});
                }
                var newResourcesFile = resourcesEntry.Value with {Entries = newEntries.ToImmutableList()};
                newFiles.Add(resourcesEntry.Key, newResourcesFile);
            }

            return this with { ResourcesFiles = newFiles.ToImmutableDictionary() };
        }

        [Pure]
        public ResourcesDatabase Intersect(ResourcesDatabase database)
        {
            var keysToKeep = database.ResourcesFiles.Values
                .SelectMany(file => file.Entries.Select(entry => entry.Invariant)).ToHashSet();
            var newFiles = new Dictionary<string, ResourcesFile>();
            foreach (var entry in ResourcesFiles.ToList())
            {
                var newFile = entry.Value.Intersect(keysToKeep);
                
                if (newFile.Entries.Count > 0)
                {
                    newFiles.Add(entry.Key, newFile);
                }
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

                var resourcesFile = ResourcesFile.Read(file.FullName);
                files[filePath] = resourcesFile;
            }

            foreach (var folder in directoryInfo.GetDirectories())
            {
                AddFolder(files, folder.FullName, Path.Combine(relativePath, folder.Name), exclude);
            }
        }
    }
}
