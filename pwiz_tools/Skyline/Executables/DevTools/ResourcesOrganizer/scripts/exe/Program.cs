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
using System.Text;
using CommandLine;
using ResourcesOrganizer.ResourcesModel;

namespace ResourcesOrganizer
{
    public class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            return Parser.Default.ParseArguments<AddVerb, ImportLastVersion, ExportResx, ExportLocalizationCsv, ImportLocalizationCsv>(args)
                .MapResult<AddVerb, ImportLastVersion, ExportResx, ExportLocalizationCsv, ImportLocalizationCsv, int>(
                    DoAdd, 
                    DoImportTranslations,
                    DoExportResx,
                    DoExportLocalizationCsv,
                    DoImportLocalizationCsv,
                    HandleParseError);
        }

        static int HandleParseError(IEnumerable<Error> errors)
        {
            foreach (var e in errors)
            {
                if (e is HelpRequestedError || e is VersionRequestedError)
                {
                    continue;
                }
                Console.WriteLine($"Error: {e}");
            }

            return 1;
        }

        public static int DoAdd(AddVerb options)
        {
            ResourcesDatabase database;
            if (options.CreateNew)
            {
                database = ResourcesDatabase.Empty;
            }
            else
            {
                database = GetDatabase(options) ?? ResourcesDatabase.Empty;
            }

            var originalFileCount = database.ResourcesFiles.Count;
            var originalResourceCount = database.ResourcesFiles.Values.Sum(file => file.Entries.Count);
            var originalLocalizationCount =
                database.ResourcesFiles.Values.Sum(file => file.Entries.Sum(entry => entry.LocalizedValues.Count));
            var exclude = options.Exclude.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in options.Path)
            {
                var otherDb = ResourcesDatabase.ReadFile(file, exclude);
                database = database.Add(otherDb);
            }
            var newFileCount = database.ResourcesFiles.Count;
            var newResourceCount = database.ResourcesFiles.Values.Sum(file => file.Entries.Count);
            var newLocalizationCount =
                database.ResourcesFiles.Values.Sum(file => file.Entries.Sum(entry => entry.LocalizedValues.Count));
            database.SaveAtomic(options.Output ?? options.DbFile);
            Console.Error.WriteLine("Added {0} new files, {1} new entries, {2} new localizations to {3}",
                newFileCount - originalFileCount, newResourceCount - originalResourceCount,
                newLocalizationCount - originalLocalizationCount, options.DbFile);
            return 0;
        }

        static int DoImportTranslations(ImportLastVersion verb)
        {
            var database = GetDatabase(verb);
            if (database == null)
            {
                 Console.Error.WriteLine("Database {0} does not exist", verb.DbFile);
                 return -1;
            }
            var otherDb = ResourcesDatabase.ReadDatabase(verb.OldDb!);
            List<string> languages = verb.Language.ToList();
            if (languages.Count == 0)
            {
                languages = otherDb.GetLanguages().ToList();
            }

            int errorCount = 0;
            foreach (var fileEntry in otherDb.ResourcesFiles)
            {
                foreach (var resourceEntry in fileEntry.Value.Entries)
                {
                    foreach (var language in languages)
                    {
                        var localizedValue = resourceEntry.GetTranslation(language);
                        if (localizedValue?.Issue != null)
                        {
                            Console.Error.WriteLine("Error: Entry {0} language {1} file {2} has issue {3}",
                                resourceEntry.Name, language, fileEntry.Key, localizedValue.Issue);
                            errorCount++;
                        }
                    }
                }
            }

            if (errorCount > 0)
            {
                return -1;
            }
            database = database.ImportLastVersion(otherDb, languages, out int reviewedCount, out int totalCount);
            Console.Error.WriteLine("Imported reviewed translations for {0}/{1} resources into {2}", reviewedCount, totalCount, verb.DbFile);
            database.SaveAtomic(verb.Output ?? verb.DbFile);
            return 0;
        }

        public static int DoExportResx(ExportResx options)
        {
            var database = GetDatabase(options);
            if (database == null)
            {
                Console.Error.WriteLine("Database {0} does not exist", options.DbFile);
                return -1;
            }
            using var fileSaver = new FileSaver(options.Output);
            database.ExportResx(fileSaver.SafeName!, options.OverrideAll);
            fileSaver.Commit();
            Console.Error.WriteLine("Wrote {0} resx files to {1}", database.ResourcesFiles.Count, options.Output);
            return 0;
        }

        public static int DoExportLocalizationCsv(ExportLocalizationCsv options)
        {
            var database = GetDatabase(options);
            if (database == null)
            {
                Console.Error.WriteLine("Database {0} does not exist", options.DbFile);
                return -1;
            }

            var languages = options.Language.ToList();
            if (languages.Count == 0)
            {
                languages = database.GetLanguages().ToList();
            }

            if (languages.Count == 0)
            {
                Console.Error.WriteLine("No languages");
                return -1;
            }

            foreach (var languageFilePath in EnumerateLocalizationFiles(options.Output!, languages))
            {
                using var fileSaver = new FileSaver(languageFilePath.FilePath);
                database.ExportLocalizationCsv(fileSaver.SafeName!, languageFilePath.Language, out int entryCount);
                fileSaver.Commit();
                Console.Error.WriteLine("Wrote {0} strings to {1}", entryCount, languageFilePath.FilePath);
            }

            return 0;
        }

        static int DoImportLocalizationCsv(ImportLocalizationCsv options)
        {
            var database = GetDatabase(options);
            if (database == null)
            {
                Console.Error.WriteLine("Database {0} does not exist", options.DbFile);
                return -1;
            }
            var languages = options.Language.ToList();
            if (languages.Count == 0)
            {
                languages = database.GetLanguages().ToList();
            }

            if (languages.Count == 0)
            {
                Console.Error.WriteLine("No languages");
                return -1;
            }

            database = ImportLocalizationCsv(database, options.Input!, languages);
            if (database == null)
            {
                return -1;
            }
            database.SaveAtomic(options.DbFile);
            return 0;
        }

        public static ResourcesDatabase? ImportLocalizationCsv(ResourcesDatabase database, string inputFile,
            List<string> languages)
        {
            foreach (var languageFilePath in EnumerateLocalizationFiles(inputFile, languages))
            {
                if (!File.Exists(languageFilePath.FilePath))
                {
                    if (languages.Count == 1)
                    {
                        Console.Out.WriteLine("Error: File {0} does not exist", languageFilePath.FilePath);
                        return null;
                    }
                    Console.Error.WriteLine("File {0} for language {1} does not exist, skipping", languageFilePath.FilePath, languageFilePath.Language);
                    continue;
                }

                database = database.ImportLocalizationCsv(languageFilePath, out int rowCount,
                    out int matchedEntryCount, out int changedEntryCount);
                Console.Out.WriteLine("Read {0} rows from {1} and changed {2}/{3} matching records in resx files", rowCount, languageFilePath.FilePath, changedEntryCount, matchedEntryCount);
            }

            return database;
        }

        private static IEnumerable<LanguageFilePath> EnumerateLocalizationFiles(string basePath,
            IList<string> languages)
        {
            if (languages.Count == 0)
            {
                yield break;
            }
            var fullPath = Path.GetFullPath(basePath);
            if (languages.Count == 1)
            {
                yield return new LanguageFilePath(languages[0], fullPath);
            }

            foreach (var language in languages)
            {
                var folder = Path.GetDirectoryName(fullPath)!;
                var baseFile = Path.GetFileNameWithoutExtension(fullPath);
                var extension = Path.GetExtension(fullPath);
                var filePath = Path.Combine(folder, baseFile + "." + language + extension);
                yield return new LanguageFilePath(language, filePath);
            }
        }

        private static ResourcesDatabase? GetDatabase(Options options)
        {
            var path = options.DbFile;
            if (File.Exists(path))
            {
                return ResourcesDatabase.ReadDatabase(path);
            }
            return null;
        }

    }
    public record LanguageFilePath(string Language, string FilePath);
}
