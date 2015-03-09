/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Reflection;
using System.Windows.Forms;
using Ionic.Zip;

namespace SkylineTester
{
    public partial class CreateZipInstallerWindow : Form
    {
        // Excluded files must be lower-case!
        private static readonly List<string> EXCLUDED_FILES = new List<string>
        {
            "testresults",
            "skylinetester.zip",
            "testrunner.log",
            "testrunnermemory.log"
        };

        public string ZipDirectory { get; private set; }

        public CreateZipInstallerWindow()
        {
            InitializeComponent();
            textBoxZipDirectory.Text = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Select a folder to contain the zip file.",
                ShowNewFolderButton = true
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    textBoxZipDirectory.Text = dlg.SelectedPath;
            }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            ZipDirectory = textBoxZipDirectory.Text;
            if (!Directory.Exists(ZipDirectory))
            {
                MessageBox.Show("That folder does not exist.");
                return;
            }

            Close();
        }

        public static void CreateZipFile(string zipPath)
        {
            Console.WriteLine();
            Console.WriteLine("# Creating " + Path.GetFileName(zipPath) + "...");
            Console.WriteLine();

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            Directory.SetCurrentDirectory(exeDirectory);
            
            var solutionDirectory = exeDirectory;
            while (!File.Exists(Path.Combine(solutionDirectory, "Skyline.sln")))
            {
                solutionDirectory = Path.GetDirectoryName(solutionDirectory);
                if (solutionDirectory == null)
                    throw new ApplicationException("Can't find solution directory");
            }

            using (var zipFile = new ZipFile(zipPath))
            {
                // DotNetZip has a _bug_ which causes an extraction error without this
                // (see http://stackoverflow.com/questions/15337186/dotnetzip-badreadexception-on-extract)
                zipFile.ParallelDeflateThreshold = -1;

                if ((String.Empty + Path.GetFileName(zipPath)).ToLower() == "skylinenightly.zip")
                {
                    // Add files to top level of zip file.
                    var files = new[]
                    {
                        "SkylineNightly.exe",
                        "SkylineNightly.pdb",
                        "Microsoft.Win32.TaskScheduler.dll",
                        "Ionic.Zip.dll"
                    };
                    foreach (var file in files)
                    {
                        Console.WriteLine(file);
                        zipFile.AddFile(file);
                    }
                }

                else
                {
                    // Add SkylineTester at top level of zip file.
                    Console.WriteLine("SkylineTester.exe");
                    zipFile.AddFile("SkylineTester.exe");

                    // Add .skytr files at top level of zip file.
                    var skytrDirectory = Path.Combine(solutionDirectory, @"SkylineTester\Run files");
                    foreach (var skytrFile in Directory.EnumerateFiles(skytrDirectory, "*.skytr"))
                        AddFile(skytrFile, zipFile, ".");

                    // Add each subdirectory in the bin directory.
                    foreach (var directory in Directory.EnumerateDirectories("."))
                    {
                        if (Include(directory))
                        {
                            var name = Path.GetFileName(directory) ?? "";
                            Console.WriteLine(Path.Combine(SkylineTesterWindow.SkylineTesterFiles, name));
                            zipFile.AddDirectory(directory, Path.Combine(SkylineTesterWindow.SkylineTesterFiles, name));
                        }
                    }

                    // Add each file in the bin directory.
                    foreach (var file in Directory.EnumerateFiles("."))
                    {
                        if (Include(file))
                            AddFile(file, zipFile);
                    }

                    // Add test zip files.
                    var zipFilesList = new List<string>();
                    FindZipFiles(solutionDirectory, zipFilesList);
                    var zipFilesDirectory = Path.Combine(SkylineTesterWindow.SkylineTesterFiles, "TestZipFiles");
                    foreach (var testZipFile in zipFilesList)
                    {
                        var testZipDirectory = Path.GetDirectoryName(testZipFile);
                        if (testZipDirectory == null)
                            continue;
                        testZipDirectory = Path.Combine(zipFilesDirectory,
                            testZipDirectory.Substring(solutionDirectory.Length + 1));
                        AddFile(testZipFile, zipFile, testZipDirectory);
                    }

                    // Add unit testing DLL.
                    var unitTestingDll = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        @"Microsoft Visual Studio 12.0\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll");
                    AddFile(unitTestingDll, zipFile);
                }

                Console.WriteLine();
                Console.WriteLine("# Saving...");
                zipFile.Save();
                Console.WriteLine();
                Console.WriteLine("# {0} size: {1:F1} MB", Path.GetFileName(zipPath), new FileInfo(zipPath).Length / (1024.0*1024));
                Console.WriteLine("# Done.");
                Console.WriteLine();
            }
        }

        static bool Include(string fileOrDirectory)
        {
            var name = Path.GetFileName(fileOrDirectory);
            return (name != null && !EXCLUDED_FILES.Contains(name.ToLower()));
        }

        static void AddFile(string filePath, ZipFile zipFile, string zipDirectory = SkylineTesterWindow.SkylineTesterFiles)
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
