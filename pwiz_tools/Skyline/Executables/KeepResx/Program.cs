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
            @"skyline\executables\autoqc\*",
            @"skyline\executables\localizationhelper\*",
            @"skyline\executables\multiload\*",
            @"skyline\executables\skylinepeptidecolorgenerator\*",
            @"skyline\executables\skylinerunner\*",
            @"skyline\executables\tools\exampleargcollector\*",
            @"skyline\executables\tools\exampleinteractivetool\*",
            @"skyline\executables\tools\xltcalc\c#\skylineintegration\properties\resources.resx",
            @"skyline\controls\startup\tutoriallinkresources.resx",
            @"skyline\skylinenightly\*",
            @"skyline\skylinetester\*",
            @"skyline\testutil\*"
        };

        private const string ExtResx = ".ja.resx";
        private static readonly string[] ExtNotResx = new string[0]; // "zh-CHS.resx" or ".ja.resx" ;
        private const string ExtNew = ".resx";

        private static readonly string[][] findReplace =
        {
            new[] {"<?xml version='1.0' encoding='UTF-8'?>", "<?xml version=\"1.0\" encoding=\"utf-8\"?>" },
            new[] {"<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "<?xml version=\"1.0\" encoding=\"utf-8\"?>" },
            new[] {"<xsd:schema xmlns=\"\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\" id=\"root\">",
                "<xsd:schema id=\"root\" xmlns=\"\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\">"},
            new[] {"\"/>", "\" />"},
            new[] {"<data name=\">>", "<data name=\"&gt;&gt;"},
            new[] {"<value/>", "<value />"}
        };

        /// <summary>
        /// For removing all source code files except .resx files
        /// </summary>
        private static bool RemoveNonResx { get { return false; } }
        /// <summary>
        /// For moving resx files from ExtResx to ExtNew (e.g. .cn.resx from translators to .zh-CHS.resx)
        /// </summary>
        private static bool MoveResx { get { return false; } }
        /// <summary>
        /// In case files from localizers have just \n (Unix) newlines instead of \r\n (Windows)
        /// </summary>
        private static bool FixResx { get { return false; } }

        /// <summary>
        /// In case files from localizers have UTF8 prefix, which needs to be removed
        /// </summary>
        private static bool FixResxUtf8 { get { return true; } }

        /// <summary>
        /// Replace strings in findeReplace list if true
        /// </summary>
        private static bool DoFindReplace { get { return true; } }

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
                    else if (FixResxUtf8)
                    {
                        FixUtf8Prefix(fileName);
                    }
                    if (DoFindReplace)
                    {
                        FindReplaceText(fileName);
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
        private static void FixUtf8Prefix(string fileName)
        {
            try
            {
                // ReadAllText will strip encoding characters at the beginning
                var bytes = File.ReadAllBytes(fileName);
                if (bytes[0] != 0xEF)
                    return;
                var listBytes = bytes.ToList();
                listBytes.RemoveRange(0, 3);
                File.WriteAllBytes(fileName, listBytes.ToArray());
            }
            catch (Exception)
            {
                Console.WriteLine("Failure writing {0}", fileName);
            }
        }
        private static void FindReplaceText(string fileName)
        {
            try
            {
                string fileText = File.ReadAllText(fileName);
                foreach (var rep in findReplace)
                {
                    fileText = fileText.Replace(rep[0], rep[1]);
                }
                File.WriteAllText(fileName, fileText);
            }
            catch (Exception)
            {
                Console.WriteLine("Failure writing {0}", fileName);
            }
        }
    }
}
