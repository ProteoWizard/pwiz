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
            if (!ToggleRunButtons(tabBuild))
                return;

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var subversion = Path.Combine(programFiles, @"Subversion\bin\svn.exe");
            if (!File.Exists(subversion))
            {
                subversion = Path.Combine(programFiles, @"VisualSVN\bin\svn.exe");
                if (!File.Exists(subversion))
                {
                    // TODO: Offer to install for the user.
                    MessageBox.Show("Must install Subversion");
                    return;
                }
            }

            Tabs.SelectTab(tabOutput);

            _buildDir = Path.GetDirectoryName(Environment.CurrentDirectory) ?? "";
            _buildDir = Path.Combine(_buildDir, "Build");
            _appendToLog = true;

            StartProcess(
                subversion,
                @"checkout https://svn.code.sf.net/p/proteowizard/code/trunk/pwiz " + _buildDir,
                null,
                SubversionExit,
                true);
        }

        private void SubversionExit(object sender, EventArgs e)
        {
            if (!buttonStopLog.Enabled)
                return;

            StartProcess(
                Path.Combine(_buildDir, @"pwiz_tools\build-apps.bat"),
                @"32 --i-agree-to-the-vendor-licenses toolset=msvc-10.0 nolog",
                _buildDir,
                ProcessExit,
                true);
        }
    }
}
