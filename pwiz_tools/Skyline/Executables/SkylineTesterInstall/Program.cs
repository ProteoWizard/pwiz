using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.Zip;

namespace SkylineTesterInstall
{
    internal static class Program
    {
        private const string SkylineTesterZip = "SkylineTester.zip";
        private const string FilesDir = "SkylineTester Files";

        // Excluded files must be lower-case!
        private static readonly string[] ExcludedFiles = 
        {
            "skylinetesterinstall.exe",
            "skylinetesterinstall.pdb",
            "testrunner.log",
            "testrunnermemory.log"
        };

        static void Main()
        {
            Console.WriteLine("\nCreating " + SkylineTesterZip + "...\n");

            if (File.Exists(SkylineTesterZip))
                File.Delete(SkylineTesterZip);

            using (var zipFile = new ZipFile(SkylineTesterZip))
            {
                // DotNetZip has a _bug_ which causes an extraction error without this
                // (see http://stackoverflow.com/questions/15337186/dotnetzip-badreadexception-on-extract)
                zipFile.ParallelDeflateThreshold = -1;

                // Add SkylineTester at top level of zip file.
                Console.WriteLine("SkylineTester.exe");
                zipFile.AddFile("SkylineTester.exe");

                // Add each subdirectory in the bin directory.
                foreach (var directory in Directory.EnumerateDirectories("."))
                {
                    var name = Path.GetFileName(directory);
                    if (name != null)
                    {
                        Console.WriteLine(Path.Combine(FilesDir, name));
                        zipFile.AddDirectory(directory, Path.Combine(FilesDir, name));
                    }
                }

                // Add each file in the bin directory.
                foreach (var file in Directory.EnumerateFiles("."))
                {
                    var name = Path.GetFileName(file);
                    if (name == null || ExcludedFiles.Contains(name.ToLower()))
                        continue;
                    AddFile(file, zipFile);
                }

                // Add test zip files.
                var solutionDirectory = Environment.CurrentDirectory;
                while (!File.Exists(Path.Combine(solutionDirectory, "Skyline.sln")))
                {
                    solutionDirectory = Path.GetDirectoryName(solutionDirectory);
                    if (solutionDirectory == null)
                        throw new ApplicationException("Can't find solution directory");
                }
                var zipFilesList = new List<string>();
                FindZipFiles(solutionDirectory, zipFilesList);
                var zipFilesDirectory = Path.Combine(FilesDir, "TestZipFiles");
                foreach (var testZipFile in zipFilesList)
                {
                    var testZipDirectory = Path.GetDirectoryName(testZipFile);
                    if (testZipDirectory == null)
                        continue;
                    testZipDirectory = Path.Combine(zipFilesDirectory, testZipDirectory.Substring(solutionDirectory.Length + 1));
                    AddFile(testZipFile, zipFile, testZipDirectory);
                }

                // Add unit testing DLL.
                var unitTestingDll = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Microsoft Visual Studio 10.0\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll");
                AddFile(unitTestingDll, zipFile);

                zipFile.Save();
                Console.WriteLine("\nDone.\n");
            }
        }

        static void AddFile(string filePath, ZipFile zipFile, string zipDirectory = FilesDir)
        {
            var name = Path.GetFileName(filePath);
            if (name == null)
                return;
            Console.WriteLine(Path.Combine(zipDirectory, name));
            zipFile.AddFile(filePath, zipDirectory);
        }

        static void FindZipFiles(string directory, List<string> zipFilesList)
        {
            // Does this directory contains any .cs files?
            if (Directory.GetFiles(directory, "*.cs").Length == 0)
                return;

            // Get all zip files in the current directory.
            zipFilesList.AddRange(Directory.GetFiles(directory, "*.zip"));

            // Get all sub-directories in current directory:
            var subDirectories = Directory.GetDirectories(directory);

            // And iterate through them:
            foreach (string subDirectory in subDirectories)
            {
                FindZipFiles(subDirectory, zipFilesList);
            }
        }
    }
}
