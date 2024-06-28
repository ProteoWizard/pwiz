﻿using System.Text;
using CommandLine;
using ResourcesOrganizer.ResourcesModel;

namespace ResourcesOrganizer
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            return Parser.Default.ParseArguments<AddVerb, ImportTranslations, ExportVerb>(args)
                .MapResult<AddVerb, ImportTranslations, ExportVerb, int>(
                    DoAdd, 
                    DoImportTranslations,
                    DoExport,
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
                database = ResourcesDatabase.EMPTY;
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

        static int DoExport(ExportVerb options)
        {
            var database = GetDatabase(options);
            using var fileSaver = new FileSaver(options.Output);
            database.Export(fileSaver.SafeName);
            fileSaver.Commit();
            return 0;
        }

        private static ResourcesDatabase GetDatabase(Options options)
        {
            var path = options.DbFile;
            if (File.Exists(path))
            {
                return ResourcesDatabase.ReadDatabase(path);
            }
            return ResourcesDatabase.EMPTY;
        }
    }
}
