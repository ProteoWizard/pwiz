using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KeepResx
{
    class Program
    {
        private static readonly string[] Excludes =
        {
            @"skyline\executables\localizationhelper\localizationhelper\properties\resources.resx",
            @"skyline\executables\skylinepeptidecolorgenerator\mainwindow.resx",
            @"skyline\executables\skylinepeptidecolorgenerator\properties\resources.resx",
            @"skyline\executables\tools\exampleargcollector\argcollector\testargcollector\exampletoolui.resx",
            @"skyline\executables\tools\exampleargcollector\argcollector\testargcollector\properties\resources.resx",
            @"skyline\executables\tools\exampleargcollector\argcollector\testargcollector\properties\resources.resx",
            @"skyline\controls\startup\tutoriallinkresources.resx",
            @"skyline\skylinetester\aboutwindow.resx",
            @"skyline\skylinetester\createzipinstallerwindow.resx",
            @"skyline\skylinetester\deletewindow.resx",
            @"skyline\skylinetester\findwindow.resx",
            @"skyline\skylinetester\memorychartwindow.resx",
            @"skyline\skylinetester\nightlylogswindow.resx",
            @"skyline\skylinetester\skylinetesterwindow.resx",
            @"skyline\skylinetester\properties\resources.resx",
            @"skyline\testutil\pauseandcontinueform.resx"
        };

        private const string ExtResx = ".resx";
        private static readonly string[] ExtNotResx = { ".ja.resx", ".zh-CHS.resx" };
        private const string ExtNew = ".zh-CHS.resx";

        private static bool MoveResx { get { return false; } }
        private static bool RemoveNonResx { get { return true; } }

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
            return !Excludes.Any(exclude => Path.GetFullPath(fileName).ToLower().EndsWith(exclude));
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
    }
}
