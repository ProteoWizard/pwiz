﻿/*
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
            "microsoft.visualstudio.qualitytools.unittestframework.dll", // Ignore if this appears in a build dir - gets added explicitly
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
                if (string.IsNullOrEmpty(solutionDirectory))
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
                        "SkylineNightlyShim.exe",
                        "SkylineNightly.exe",
                        "SkylineNightly.pdb",
                        "Microsoft.Win32.TaskScheduler.dll",
                        "DotNetZip.dll"
                    };
                    foreach (var file in files)
                    {
                        Console.WriteLine(file);
                        zipFile.AddFile(file);
                    }
                }

                else if ((String.Empty + Path.GetFileName(zipPath)).ToLower() == "bibliospec.zip")
                {
                    // Create a BiblioSpec distro
                    var files = new List<string>
                    {
                        "BlibBuild.exe",
                        "BlibFilter.exe",
                        "MassLynxRaw.dll",
                        "cdt.dll",
                        "modifications.xml",
                        "quantitation_1.xsd",
                        "quantitation_2.xsd",
                        "unimod_2.xsd"
                    };
                    var dir = Directory.GetCurrentDirectory();
                    files.Add(dir.Contains("Debug") ? "msparserD.dll" : "msparser.dll");

                    // Locate BlibToMS2
                    var parent = dir.IndexOf("Skyline\\", StringComparison.Ordinal);
                    if (parent > 0)
                    {
                        dir = dir.Substring(0, parent);
                        var blib2ms2 = dir + "Shared\\BiblioSpec\\obj\\x64\\BlibToMs2.exe";
                        if (File.Exists(blib2ms2)) // Don't worry about this for a 32 bit build, we don't distribute that
                        {
                            files.Add(blib2ms2);
                        }
                    }
                    foreach (var file in files)
                    {
                        Console.WriteLine(file);
                        zipFile.AddFile(file, string.Empty);
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
                        if (string.IsNullOrEmpty(testZipDirectory))
                            continue;
                        testZipDirectory = Path.Combine(zipFilesDirectory,
                            testZipDirectory.Substring(solutionDirectory.Length + 1));
                        AddFile(testZipFile, zipFile, testZipDirectory);
                    }

                    // Add the file that we use to determine which branch this is from
                    AddFile(Path.Combine(solutionDirectory,"..\\..\\pwiz\\Version.cpp"), zipFile);

                    // Add unit testing DLL.
                    const string relativeUnitTestingDll =
                        @"PublicAssemblies\Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll";
                    var unitTestingDll = SkylineTesterWindow.GetExistingVsIdeFilePath(relativeUnitTestingDll);
                    if (unitTestingDll == null)
                        throw new ApplicationException(string.Format("Can't find {0}", relativeUnitTestingDll));
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
