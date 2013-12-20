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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void OpenTests()
        {
            var languages = GetLanguageNames().ToList();

            checkBoxTestsEnglish.Enabled = languages.Contains("English");
            checkBoxTestsChinese.Enabled = languages.Contains("Chinese");
            checkBoxTestsJapanese.Enabled = languages.Contains("Japanese");
        }

        private void RunTests(object sender, EventArgs e)
        {
            if (!ToggleRunButtons(tabTests))
            {
                commandShell.Stop();
                return;
            }

            Tabs.SelectTab(tabOutput);

            var args = new StringBuilder();

            args.Append("offscreen=");
            args.Append(Offscreen.Checked);

            if (!RunIndefinitely.Checked)
            {
                int loop;
                if (!int.TryParse(RunLoopsCount.Text, out loop))
                    loop = 1;
                args.Append(" loop=");
                args.Append(loop);
            }

            var cultures = new List<CultureInfo>();
            if (CultureEnglish.Checked || checkBoxTestsEnglish.Checked)
                cultures.Add(new CultureInfo("en-US"));
            if (CultureFrench.Checked)
                cultures.Add(new CultureInfo("fr-FR"));
            if (checkBoxTestsChinese.Enabled && checkBoxTestsChinese.Checked)
                cultures.Add(new CultureInfo("zh"));
            if (checkBoxTestsJapanese.Enabled && checkBoxTestsJapanese.Checked)
                cultures.Add(new CultureInfo("ja"));

            args.Append(" culture=");
            args.Append(string.Join(",", cultures));
            if (PauseTestsScreenShots.Checked)
                args.Append(" pause=-1");

            args.Append(GetTestList());

            commandShell.LogFile = _defaultLogFile;
            Tabs.SelectTab(tabOutput);
            StartTestRunner(args.ToString());
        }

        private string GetTestList()
        {
            var testList = new List<string>();
            foreach (TreeNode node in TestsTree.Nodes)
                GetCheckedTests(node, testList, SkipCheckedTests.Checked);
            if (testList.Count == 0)
                return "";
            return " test=" + string.Join(",", testList);
        }

        private void checkAll_Click(object sender, EventArgs e)
        {
            foreach (var node in TestsTree.Nodes)
            {
                ((TreeNode)node).Checked = true;
                CheckAllChildNodes((TreeNode)node, true);
            }
        }

        private void uncheckAll_Click(object sender, EventArgs e)
        {
            foreach (var node in TestsTree.Nodes)
            {
                ((TreeNode)node).Checked = false;
                CheckAllChildNodes((TreeNode)node, false);
            }
        }

        private void pauseTestsForScreenShots_CheckedChanged(object sender, EventArgs e)
        {
            if (PauseTestsScreenShots.Checked)
                Offscreen.Checked = false;
        }

        private void offscreen_CheckedChanged(object sender, EventArgs e)
        {
            if (Offscreen.Checked)
                PauseTestsScreenShots.Checked = false;
        }
    }
}
