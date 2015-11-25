using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// ReSharper disable NonLocalizedString
namespace KeepResx
{
    static class Program
    {
        private static readonly string[] Excludes =
        {
            @"msconvertgui\*",
            @"seems\*",
            @"topograph\*",
            @"shared\zedgraph\*",
            @"shared\proteomedb\forms\proteomedbform.resx",
            @"skyline\executables\localizationhelper\*",
            @"skyline\executables\multiload\*",
            @"skyline\executables\skylinepeptidecolorgenerator\*",
            @"skyline\executables\skylinerunner\*",
            @"skyline\executables\tools\exampleargcollector\*",
            @"skyline\executables\tools\exampleinteractivetool\*",
            @"skyline\controls\startup\tutoriallinkresources.resx",
            @"skyline\skylinenightly\*",
            @"skyline\skylinetester\*",
            @"skyline\testutil\*"
        };

        private const string ExtResx = ".resx";
        private static readonly string[] ExtNotResx = new string[0]; // { ".ja.resx", ".zh-CHS.resx" };
        private const string ExtNew = ".ja.resx";

        private static bool MoveResx { get { return false; } }
        private static bool RemoveNonResx { get { return false; } }
        private static bool FixResx { get { return true; } }

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Please specify a single directory from which to delete everything but ResX files.");
                return;
            }

            var dirPath = args[0];
            var listForms = new List<string>();
            KeepOnlyResX(dirPath, listForms);
//            listForms.Sort();
//            using (var writer = new StreamWriter("Forms.txt"))
//            {
//                foreach (var listForm in listForms)
//                {
//                    writer.WriteLine(listForm);
//                }
//            }
        }

        private static bool KeepOnlyResX(string dirPath, List<string> listForms)
        {
            bool containsResX = false;
            foreach (var fileName in Directory.EnumerateFiles(dirPath))
            {
                if (IsIncludedResx(fileName))
                {
                    listForms.Add(Path.GetFileNameWithoutExtension(fileName));
                    if (MoveResx)
                    {
                        string newFileName = fileName.Substring(0, fileName.Length - ExtResx.Length) + ExtNew;
                        DeleteFileIfPossible(newFileName);
                        File.Move(fileName, newFileName);
                    }
                    else if (FixResx)
                    {
                        FixNewlines(fileName);
                    }
                    containsResX = true;
                    continue;
                }

                if (RemoveNonResx)
                {
                    DeleteFileIfPossible(fileName);
                }
            }

            foreach (var dirName in Directory.EnumerateDirectories(dirPath))
            {
                if (KeepOnlyResX(dirName, listForms))
                    containsResX = true;
            }

            if (!containsResX && RemoveNonResx)
            {
                DeleteDirectoryIfPossible(dirPath);
            }
            return containsResX;
        }

        private static bool IsIncludedResx(string fileName)
        {
            if (!fileName.EndsWith(ExtResx))
                return false;
            if (ExtNotResx.Any(fileName.EndsWith))
            {
                return false;
            }
            return !Excludes.Any(exclude => MatchesExclude(fileName, exclude));
        }

        private static bool MatchesExclude(string fileName, string exclude)
        {
            string fullPathLower = Path.GetFullPath(fileName).ToLower();
            if (exclude.EndsWith("*"))
                return fullPathLower.IndexOf(exclude.Substring(0, exclude.Length - 1), StringComparison.Ordinal) != -1;

            return fullPathLower.EndsWith(exclude);
        }

        private static void DeleteFileIfPossible(string fileName)
        {
            try
            {
                File.Delete(fileName);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }
        }

        private static void DeleteDirectoryIfPossible(string dirPath)
        {
            try
            {
                Directory.Delete(dirPath);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }
        }

        private static void FixNewlines(string fileName)
        {
            try
            {
                string fileText = File.ReadAllText(fileName);
                fileText = Regex.Replace(fileText, @"\r\n?|\n", "\r\n");
                File.WriteAllText(fileName, fileText);
            }
            catch (Exception)
            {
                Console.WriteLine("Failure writing {0}", fileName);
            }
        }
    }
}
