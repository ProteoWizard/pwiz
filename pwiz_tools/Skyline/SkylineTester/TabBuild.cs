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
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
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
            if (!ToggleRunButtons(tabBuild))
            {
                commandShell.Stop();
                return;
            }

            _runningTab = tabBuild;
            Tabs.SelectTab(tabOutput);

            GenerateBuildCommands();
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

        private void GenerateBuildCommands()
        {
            commandShell.Add("# Build Skyline {0}-bit...", Build32.Checked ? 32 : 64);

            if (Directory.Exists(_buildDir))
            {
                if (BuildClean.Checked)
                {
                    commandShell.Add("# Deleting old Skyline build directory...");
                    commandShell.Add("rmdir /s {0}", Quote(_buildDir));
                }
                else
                {
                    commandShell.Add("# Cleaning Skyline build directory...");
                    commandShell.Add("{0} cleanup {1}", Quote(_subversion), Quote(_buildDir));
                }
            }

            commandShell.Add("# Checking out Skyline source files...");
            commandShell.Add("{0} checkout {1} {2}", 
                Quote(_subversion),
                Quote(BuildTrunk.Checked
                    ? @"https://svn.code.sf.net/p/proteowizard/code/trunk/pwiz"
                    : BranchUrl.Text),
                Quote(_buildDir));

            commandShell.Add("# Building Skyline...");
            commandShell.Add("cd {0}", Quote(_buildDir));
            commandShell.Add("{0} {1} --i-agree-to-the-vendor-licenses toolset=msvc-10.0 nolog",
                Quote(Path.Combine(_buildDir, @"pwiz_tools\build-apps.bat")),
                Build32.Checked ? 32 : 64);

            commandShell.Add("# Build done.");
        }

        private string Quote(string s)
        {
            return "\"" + s + "\"";
        }
    }
}
