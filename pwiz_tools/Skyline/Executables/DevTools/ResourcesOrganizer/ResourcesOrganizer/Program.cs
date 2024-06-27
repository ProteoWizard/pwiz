using System.Text;
using CommandLine;
using ResourcesOrganizer.ResourcesModel;

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
            var exclude = options.Exclude.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in options.Path)
            {
                var otherDb = ResourcesDatabase.ReadFile(file, exclude);
                database = database.Add(otherDb);
            }
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
                languages = otherDb.ResourcesFiles.Values
                    .SelectMany(file => file.Entries.SelectMany(entry => entry.LocalizedValues.Keys)).Distinct()
                    .ToList();
            }
            database = database.ImportTranslations(otherDb, languages);
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
            using var fileSaver = new FileSaver(options.Output);
            database.ExportLocalizationCsv(fileSaver.SafeName!, options.Language);
            fileSaver.Commit();
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
