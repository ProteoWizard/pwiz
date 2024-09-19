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
            [Option(Default = "MergedTutorials.zip")]
            public string Output { get; set; }
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

            var outputFile = options.Output;
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
