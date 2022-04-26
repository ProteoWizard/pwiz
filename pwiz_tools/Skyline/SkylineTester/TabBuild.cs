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
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace SkylineTester
{
    public class TabBuild : TabBase
    {
        public TabBuild()
        {
            MainWindow.LabelSpecifyPath.Text =
                "(Specify absolute path or relative path from {0} folder)".With(Path.GetFileName(MainWindow.RootDir));
        }

        public override void Enter()
        {
            var buildRoot = MainWindow.GetBuildRoot();
            MainWindow.ButtonDeleteBuild.Enabled = Directory.Exists(buildRoot);
            MainWindow.DefaultButton = MainWindow.RunBuild;
        }

        public override bool Run()
        {
            if (!MainWindow.HasBuildPrerequisites)
                return false;
            var architectures = GetArchitectures();
            if (architectures.Count == 0)
            {
                MessageBox.Show("Select 32 or 64 bit architecture (or both).");
                return false;
            }

            StartLog("Build", MainWindow.DefaultLogFile, true);
            CreateBuildCommands(
                GetBranchUrl(),
                MainWindow.GetBuildRoot(), 
                architectures, 
                MainWindow.NukeBuild.Checked, 
                MainWindow.UpdateBuild.Checked,
                MainWindow.RunBuildVerificationTests.Checked);
            MainWindow.RunCommands();
            return true;
        }

        public override bool Stop(bool success)
        {
            if (success && MainWindow.StartSln.Checked && MainWindow.Devenv != null)
            {
                var buildRoot = MainWindow.GetBuildRoot();
                var slnDirectory = Path.Combine(buildRoot, @"pwiz_tools\Skyline");
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = Path.Combine(slnDirectory, "Skyline.sln"),
                        WorkingDirectory = slnDirectory,
                        UseShellExecute = true,
                    }
                };
                process.Start();
            }

            return true;
        }

        public override int Find(string text, int position)
        {
            return VerifyFind(text, position, "Build");
        }

        public static List<int> GetArchitectures()
        {
            var architectures = new List<int>();
            if (MainWindow.Build32.Checked)
                architectures.Add(32);
            if (MainWindow.Build64.Checked)
                architectures.Add(64);
            return architectures;
        }

        public static string GetMasterUrl()
        {
            return @"https://github.com/ProteoWizard/pwiz";
        }

        public static string GetBranchUrl()
        {
            return MainWindow.BuildTrunk.Checked
                ? GetMasterUrl()
                : MainWindow.BranchUrl.Text;
        }

        public static bool CreateBuildCommands(
            string branchUrl, 
            string buildRoot, 
            IList<int> architectures, 
            bool nukeBuild, 
            bool updateBuild,
            bool runBuildTests)
        {
            var commandShell = MainWindow.CommandShell;
            var branchParts = branchUrl.Split('/');
            var branchName = branchParts[branchParts.Length - 1].Equals("pwiz")
                ? "Skyline (master)"
                : "Skyline ({0}/{1})".With(branchParts[branchParts.Length - 2], branchParts[branchParts.Length - 1]);
            var git = MainWindow.Git;
            
            var toolsetArg = string.Empty; // Let bjam pick the newest available - currently VS2017 and VS2019 work equally well for Skyline builds
            /* But retain this code in case we someday get back to a state where we need to choose

            // Determine toolset requirement based on .Net usage
            // Pull a file like
            // https://raw.githubusercontent.com/ProteoWizard/pwiz/master/pwiz_tools/Skyline/Skyline.csproj
            // or
            // https://raw.githubusercontent.com/ProteoWizard/pwiz/feature/VS2017-update/pwiz_tools/Skyline/Skyline.csproj
            var toolset = "msvc-14.1"; // VS2017
            for (var retry = 60; retry-- >0;)
            {
                var csProjFileUrl = "[unknown]";
                try
                {
                     csProjFileUrl = GetMasterUrl().Equals(branchUrl)
                        ? "https://raw.githubusercontent.com/ProteoWizard/pwiz/master/pwiz_tools/Skyline/Skyline.csproj"
                        : "https://raw.githubusercontent.com/ProteoWizard/pwiz/" + GetBranchPath(branchUrl) + "/pwiz_tools/Skyline/Skyline.csproj";
                    var csProjText = (new WebClient()).DownloadString(csProjFileUrl);
                    var dotNetVersion = csProjText.Split(new[] {"TargetFrameworkVersion"}, StringSplitOptions.None)[1].Split('v')[1].Split(new []{'.'});
                    if ((int.Parse(dotNetVersion[0]) >= 4) && (int.Parse(dotNetVersion[1]) >= 7))
                    {
                        toolset = "msvc-14.1"; // VS2017 for .Net 4.7.x or greater
                    }
                    break;
                }
                catch (Exception e)
                {

                    commandShell.AddImmediate("Trouble fetching {0} for .Net version inspection ({1})", csProjFileUrl, e.Message);
                    if (retry == 0)
                        throw;
                    commandShell.AddImmediate("retrying...");
                    commandShell.IsWaiting = true;
                    // CONSIDER: Wait for up to 60 seconds while pumping messages for UI. Kind of a hack, but better than
                    //           blocking the UI thread completely while trying.
                    var watch = new Stopwatch();
                    watch.Start();
                    while (watch.Elapsed.TotalSeconds < 60)
                    {
                        if (!commandShell.IsWaiting)
                            return false;   // Cancelled
                        Application.DoEvents();
                    }
                    commandShell.IsWaiting = false;
                }
            }
            var toolsetArg = "toolset=" + toolset;
            */

            var architectureList = string.Join("- and ", architectures);
            commandShell.Add("# Build {0} {1}-bit...", branchName, architectureList);
            commandShell.RunStartTime = DateTime.UtcNow;

            if (Directory.Exists(buildRoot) && updateBuild)
            {
                commandShell.Add("#@ Updating Build directory...\n");
                commandShell.Add("# Updating Build directory...");
                commandShell.Add("cd {0}", buildRoot.Quote());
                commandShell.Add("{0} pull", git.Quote());
            }

            if (nukeBuild)
            {
                commandShell.IsUnattended = true; // Don't go interactive if there's trouble deleting directory
                commandShell.Add("#@ Deleting Build directory...\n");
                commandShell.Add("# Deleting Build directory...");
                commandShell.Add("rmdir /s {0}", buildRoot.Quote());

                commandShell.Add("#@ Checking out {0} source files...\n", branchName);
                commandShell.Add("# Checking out {0} source files...", branchName);
                // Add the --progress flag for richer logging - git leaves out most progress info when it isn't writing to an actual terminal
                if (branchName.Contains("master"))
                {
                    commandShell.AddWithRetry("{0} clone {1} --progress {2}", git.Quote(), branchUrl.Quote(), buildRoot.Quote());
                }
                else
                {
                    var branch = branchUrl.Split(new[] {"tree/"}, StringSplitOptions.None)[1];
                    commandShell.AddWithRetry("{0} clone {1} --progress -b {2} {3}", git.Quote(), GetMasterUrl().Quote(), branch.Quote(), buildRoot.Quote());
                }
            }

            commandShell.Add("# Building Skyline...");
            commandShell.Add("cd {0}", buildRoot.Quote());

            foreach (int architecture in architectures)
            {
                commandShell.Add("#@ Building Skyline {0} bit...\n", architecture);
                commandShell.Add("{0} {1} {2} --i-agree-to-the-vendor-licenses {3} nolog",
                    Path.Combine(buildRoot, @"pwiz_tools\build-apps.bat").Quote(),
                    architecture,
                    runBuildTests ? "pwiz_tools/Skyline" : "pwiz_tools/Skyline//Skyline.exe",
                    toolsetArg);
            }

            commandShell.Add("# Build done.");
            return true;
        }

        private static string GetBranchPath(string branchUrl)
        {
            const string splitString = "/pwiz/tree/";
            var branchHalves = branchUrl.Split(new[] { splitString }, StringSplitOptions.None);
            if (branchHalves.Length == 1)
                throw new ArgumentException(string.Format("The branch URL {0} must contain the branch prefix {1}", branchUrl, splitString));
            // Return the part after the split string
            return branchHalves[1];
        }

        public void DeleteBuild()
        {
            var buildRoot = MainWindow.GetBuildRoot();
            if (!Directory.Exists(buildRoot) ||
                MessageBox.Show(MainWindow, "Delete \"" + buildRoot + "\" folder?", "Confirm delete",
                    MessageBoxButtons.OKCancel) != DialogResult.OK)
            {
                return;
            }

            using (var deleteWindow = new DeleteWindow(buildRoot))
            {
                deleteWindow.ShowDialog();
            }

            MainWindow.ButtonDeleteBuild.Enabled = Directory.Exists(buildRoot);
        }

        public void BrowseBuild()
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Select or create a root folder for build source files.",
                ShowNewFolderButton = true
            })
            {
                if (dlg.ShowDialog(MainWindow) == DialogResult.OK)
                    MainWindow.BuildRoot.Text = dlg.SelectedPath;
            }

            MainWindow.ButtonDeleteBuild.Enabled = Directory.Exists(MainWindow.GetBuildRoot()); // MainWindow.GetBuildRoot() is the directory that actually gets deleted in DeleteBuild()
        }
    }
}
