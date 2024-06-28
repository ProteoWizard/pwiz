using System.Text;
using CommandLine;
using ResourcesOrganizer.ResourcesModel;
using Parser = CommandLine.Parser;

namespace ResourcesOrganizer
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            return Parser.Default.ParseArguments<AddVerb, ImportTranslations, ExportResx, ExportLocalizationCsv>(args)
                .MapResult<AddVerb, ImportTranslations, ExportResx, ExportLocalizationCsv, int>(
                    DoAdd, 
                    DoImportTranslations,
                    DoExportResx,
                    DoExportLocalizationCsv,
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

        static int DoAdd(AddVerb options)
        {
            ResourcesDatabase database;
            if (options.CreateNew)
            {
                database = ResourcesDatabase.Empty;
            }
            else
            {
                database = GetDatabase(options);
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
            Console.Error.WriteLine("Added {0} new files, {1} new entries, {2} new localizations",
                newFileCount - originalFileCount, newResourceCount - originalResourceCount,
                newLocalizationCount - originalLocalizationCount);
            database.SaveAtomic(options.Output ?? options.DbFile);
            return 0;
        }

        static int DoImportTranslations(ImportTranslations verb)
        {
            var database = GetDatabase(verb);
            var otherDb = ResourcesDatabase.ReadDatabase(verb.OldDb);
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
                        if (localizedValue?.IssueType != null)
                        {
                            Console.Error.WriteLine("Error: Entry {0} language {1} file {2} has issue {3}",
                                resourceEntry.Name, language, fileEntry.Key, localizedValue.IssueType);
                            errorCount++;
                        }
                        else if (localizedValue?.ReviewedValue != null)
                        {
                            Console.Error.WriteLine(
                                "Error: Reviewed value for entry {0} language {1} in file {2} should be null but is {3}",
                                resourceEntry.Name, language, fileEntry.Key, localizedValue.ReviewedValue);
                            errorCount++;
                        }
                    }
                }
            }

            if (errorCount > 0)
            {
                return -1;
            }
            database = database.ImportTranslations(otherDb, languages, out int reviewedCount, out int totalCount);
            Console.Error.WriteLine("Imported reviewed translations for {0}/{1} resources", reviewedCount, totalCount);
            database.SaveAtomic(verb.Output ?? verb.DbFile);
            return 0;
        }

        static int DoExportResx(ExportResx options)
        {
            var database = GetDatabase(options);
            using var fileSaver = new FileSaver(options.Output);
            database.ExportResx(fileSaver.SafeName!, options.OverrideAll);
            fileSaver.Commit();
            return 0;
        }

        static int DoExportLocalizationCsv(ExportLocalizationCsv options)
        {
            var database = GetDatabase(options);
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
            foreach (var language in languages)
            {
                string outputFile;
                if (languages.Count == 1)
                {
                    outputFile = options.Output!;
                }
                else
                {
                    var path = Path.GetFullPath(options.Output!);
                    var folder = Path.GetDirectoryName(path)!;
                    var baseFile = Path.GetFileNameWithoutExtension(path);
                    var extension = Path.GetExtension(path);
                    outputFile = Path.Combine(folder, baseFile + "." + language + extension);
                }
                using var fileSaver = new FileSaver(outputFile);
                database.ExportLocalizationCsv(fileSaver.SafeName!, language, out int entryCount);
                fileSaver.Commit();
                Console.Error.WriteLine("Wrote {0} strings to {1}", entryCount, outputFile);
            }
            return 0;
        }

        private static ResourcesDatabase GetDatabase(Options options)
        {
            var path = options.DbFile;
            ResourcesDatabase database = ResourcesDatabase.Empty;
            if (File.Exists(path))
            {
                database = ResourcesDatabase.ReadDatabase(path);
            }

            return database;
        }
    }
}
