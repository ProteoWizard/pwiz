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

            _buildDir = Path.GetDirectoryName(Environment.CurrentDirectory) ?? "";
            _buildDir = Path.Combine(_buildDir, "Build");
            _appendToLog = true;

            if (Directory.Exists(_buildDir))
            {
                Log(Environment.NewLine + "# Cleaning Skyline build directory" + Environment.NewLine);
                StartProcess(
                    _subversion,
                    "cleanup " + _buildDir,
                    null,
                    CleanupDone,
                    true);
            }
            else
            {
                CleanupDone(null, null);
            }
        }

        private void CleanupDone(object sender, EventArgs e)
        {
            if (_runningTab == null)
                return;

            Invoke(new Action(() =>
            {
                Log(Environment.NewLine + "# Checking out Skyline source files..." + Environment.NewLine);
                StartProcess(
                    _subversion,
                    @"checkout https://svn.code.sf.net/p/proteowizard/code/trunk/pwiz " + _buildDir,
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
                StartProcess(
                    Path.Combine(_buildDir, @"pwiz_tools\build-apps.bat"),
                    @"32 --i-agree-to-the-vendor-licenses toolset=msvc-10.0 nolog",
                    _buildDir,
                    ProcessExit,
                    true);
            }));
        }
    }
}
