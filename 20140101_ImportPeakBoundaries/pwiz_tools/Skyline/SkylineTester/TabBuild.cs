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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void OpenBuild()
        {
            buttonDeleteBuild.Enabled = Directory.Exists(_buildDir);
        }

        private bool HasBuildPrerequisites
        {
            get
            {
                if (_subversion == null)
                {
                    MessageBox.Show(
                        "Subversion is required to build Skyline.  You can install it from http://sourceforge.net/projects/win32svn/");
                    return false;
                }

                if (_devenv == null)
                {
                    MessageBox.Show("Visual Studio 10.0 is required to build Skyline.");
                    return false;
                }

                return true;
            }
        }

        private void RunBuild(object sender, EventArgs e)
        {
            if (!HasBuildPrerequisites)
                return;
            var architectures = new List<int>();
            if (Build32.Checked)
                architectures.Add(32);
            if (Build64.Checked)
                architectures.Add(64);
            if (architectures.Count == 0)
            {
                MessageBox.Show("Select 32 or 64 bit architecture (or both).");
                return;
            }

            if (!ToggleRunButtons(tabBuild))
            {
                commandShell.Stop();
                return;
            }

            _runningTab = tabBuild;
            commandShell.LogFile = _defaultLogFile;
            Tabs.SelectTab(tabOutput);

            GenerateBuildCommands(architectures);
            commandShell.Run(BuildDone);
        }

        private void BuildDone(bool success)
        {
            ToggleRunButtons(null);

            if (success && StartSln.Checked && _devenv != null)
            {
                var slnDirectory = Path.Combine(_buildDir, @"pwiz_tools\Skyline");
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
        }

        private void GenerateBuildCommands(IList<int> architectures)
        {
            var architectureList = string.Join("- and ", architectures);
            commandShell.Add("# Build Skyline {0}-bit...", architectureList);

            if (Directory.Exists(_buildDir))
            {
                if (NukeBuild.Checked)
                {
                    commandShell.Add("#@ Deleting Build directory...\n");
                    commandShell.Add("# Deleting old Skyline build directory...");
                    commandShell.Add("rmdir /s {0}", Quote(_buildDir));
                }
                else if (UpdateBuild.Checked)
                {
                    commandShell.Add("#@ Cleaning Build directory...\n");
                    commandShell.Add("# Cleaning Skyline build directory...");
                    commandShell.Add("{0} cleanup {1}", Quote(_subversion), Quote(_buildDir));
                }
            }

            if (NukeBuild.Checked)
            {
                commandShell.Add("#@ Checking out Skyline source files...\n");
                commandShell.Add("# Checking out Skyline source files...");
                commandShell.Add("{0} checkout {1} {2}",
                    Quote(_subversion),
                    Quote(BuildTrunk.Checked
                        ? @"https://svn.code.sf.net/p/proteowizard/code/trunk/pwiz"
                        : BranchUrl.Text),
                    Quote(_buildDir));
            }

            commandShell.Add("# Building Skyline...");
            commandShell.Add("cd {0}", Quote(_buildDir));
            foreach (int architecture in architectures)
            {
                commandShell.Add("#@ Building Skyline {0} bit...\n", architecture);
                commandShell.Add("{0} {1} --i-agree-to-the-vendor-licenses toolset=msvc-10.0 nolog",
                    Quote(Path.Combine(_buildDir, @"pwiz_tools\build-apps.bat")),
                    architecture);
            }

            commandShell.Add("#@ Build done.\n");
            commandShell.Add("# Build done.");
        }

        private string Quote(string s)
        {
            return "\"" + s + "\"";
        }


        private void buttonDeleteBuild_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(_buildDir))
            {
                statusLabel.Text = "Deleting Build folder...";
                var task = new BackgroundWorker();
                task.DoWork += (o, args) =>
                {
                    Directory.Delete(_buildDir, true);
                    Invoke(new Action(() => statusLabel.Text = ""));
                };
                task.RunWorkerAsync();
            }
            buttonDeleteBuild.Enabled = false;
        }
    }
}
