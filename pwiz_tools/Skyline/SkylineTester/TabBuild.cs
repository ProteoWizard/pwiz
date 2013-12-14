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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void RunBuild(object sender, EventArgs e)
        {
            if (_subversion == null)
            {
                // TODO: Offer to install for the user.
                MessageBox.Show("Must install Subversion");
                return;
            }

            if (!ToggleRunButtons(tabBuild))
                return;

            Tabs.SelectTab(tabOutput);

            _buildDir = Path.Combine(_rootDir, "Build");
            _appendToLog = true;

            if (!Directory.Exists(_buildDir))
            {
                CleanupDone(null, null);
                return;
            }

            if (BuildClean.Checked)
            {
                Log(Environment.NewLine + "# Deleting old Skyline build directory" + Environment.NewLine);
                var deleteTask = new BackgroundWorker();
                deleteTask.DoWork += (o, args) =>
                {
                    try
                    {
                        Directory.Delete(_buildDir, true);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Can't delete " + _buildDir);
                        ProcessExit(null, null);
                        return;
                    }
                    CleanupDone(null, null);
                };
                deleteTask.RunWorkerAsync();
            }
            else
            {
                Log(Environment.NewLine + "# Cleaning Skyline build directory" + Environment.NewLine);
                StartProcess(
                    _subversion,
                    "cleanup " + _buildDir,
                    null,
                    CleanupDone,
                    true);
            }
        }

        private void CleanupDone(object sender, EventArgs e)
        {
            if (_runningTab == null)
                return;

            Invoke(new Action(() =>
            {
                Log(Environment.NewLine + "# Checking out Skyline source files..." + Environment.NewLine);
                var branch = BuildTrunk.Checked
                    ? @"https://svn.code.sf.net/p/proteowizard/code/trunk/pwiz"
                    : BranchUrl.Text;
                StartProcess(
                    _subversion,
                    string.Format("checkout {0} {1}", branch, _buildDir),
                    null,
                    CheckoutDone,
                    true);
            }));
        }

        private void CheckoutDone(object sender, EventArgs e)
        {
            if (_runningTab == null)
                return;

            Invoke(new Action(() =>
            {
                Log(Environment.NewLine + "# Building Skyline..." + Environment.NewLine);
                var architecture = Build32.Checked ? 32 : 64;
                StartProcess(
                    Path.Combine(_buildDir, @"pwiz_tools\build-apps.bat"),
                    architecture + @" --i-agree-to-the-vendor-licenses toolset=msvc-10.0 nolog",
                    _buildDir,
                    BuildDone,
                    true);
            }));
        }

        private void BuildDone(object sender, EventArgs e)
        {
            if (_runningTab == null)
                return;

            Invoke(new Action(() =>
            {
                Log(Environment.NewLine + "# Build done." + Environment.NewLine);

                ProcessExit(sender, e);

                if (StartSln.Checked)
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
            }));
        }
    }
}
