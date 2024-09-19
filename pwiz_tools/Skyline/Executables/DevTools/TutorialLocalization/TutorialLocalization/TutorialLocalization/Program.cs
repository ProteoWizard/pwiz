using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CommandLine;

namespace TutorialLocalization
{
    internal class Program
    {
        public class Options
        {
            [Value(0)]
            public string Folder { get; set; }
        }
        static int Main(string[] args)
        {
            int result = -1;
            Parser.Default.ParseArguments<Options>(args).WithParsed(options=>result = LocalizeTutorials(options)).WithNotParsed(HandleParseError);
            return result;
        }

        public static int LocalizeTutorials(Options options)
        {
            var folderPath = Path.GetFullPath(options.Folder);
            if (!Directory.Exists(folderPath))
            {
                Console.Error.WriteLine("Folder {0} does not exist", folderPath);
                return -1;
            }

            var outputFile = "mergedTutorials.zip";
            File.Delete(outputFile);
            var zipFile = new Ionic.Zip.ZipFile(outputFile, Encoding.UTF8);
            var tutorialsLocalizer = new TutorialsLocalizer(zipFile);
            foreach (var subfolder in Directory.GetDirectories(folderPath))
            {
                tutorialsLocalizer.AddTutorialFolder(subfolder, Path.GetFileName(subfolder));
            }
            tutorialsLocalizer.SaveLocalizationCsvFiles();
            zipFile.Save();
            return 0;
        }

        static void HandleParseError(IEnumerable<Error> errors)
        {
            foreach (var e in errors)
            {
                if (e is HelpRequestedError || e is VersionRequestedError)
                {
                    continue;
                }
                Console.WriteLine($"Error: {e}");
            }
        }
    }
}
