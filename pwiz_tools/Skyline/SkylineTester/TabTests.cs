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
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SkylineTester
{
    public class TabTests : TabBase
    {
        public override void Enter()
        {
            MainWindow.DefaultButton = MainWindow.RunTests;
        }

        public override bool Run()
        {
            if (MainWindow.RunFullQualityPass.Checked)
            {
                MainWindow.QualityPassDefinite.Checked = true;
                MainWindow.QualityPassCount.Value = 1;
                MainWindow.Pass0.Checked = true;
                MainWindow.Pass1.Checked = true;
                MainWindow.QualtityTestSmallMolecules.Checked = MainWindow.TestsTestSmallMolecules.Checked;
                MainWindow.QualityChooseTests.Checked = true;
                MainWindow.RunQualityFromTestsTab();
                MainWindow.Tabs.SelectTab(MainWindow.QualityPage);
                return true;
            }

            StartLog("Tests", null, true);

            var args = new StringBuilder();

            args.Append("offscreen=");
            args.Append(MainWindow.Offscreen.Checked);

            if (!MainWindow.RunIndefinitely.Checked)
            {
                int loop;
                if (!Int32.TryParse(MainWindow.RunLoopsCount.Text, out loop))
                    loop = 1;
                args.Append(" loop=");
                args.Append(loop);
            }

            if (MainWindow.RunDemoMode.Checked)
                args.Append(" demo=on");

            if (MainWindow.TestsTestSmallMolecules.Checked)
                args.Append(" testsmallmolecules=on");

            var cultures = new List<CultureInfo>();
            if (MainWindow.TestsEnglish.Checked)
                cultures.Add(new CultureInfo("en-US"));
            if (MainWindow.TestsChinese.Checked)
                cultures.Add(new CultureInfo("zh-CHS"));
            if (MainWindow.TestsFrench.Checked)
                cultures.Add(new CultureInfo("fr-FR"));
            if (MainWindow.TestsJapanese.Checked)
                cultures.Add(new CultureInfo("ja"));
            if (MainWindow.TestsTurkish.Checked)
                cultures.Add(new CultureInfo("tr-TR"));

            args.Append(" language=");
            args.Append(String.Join(",", cultures));
            if (GetTestList().Length > 0)
                args.Append(" perftests=on"); // In case any perf tests were explicitly selected - no harm if they weren't

            args.Append(GetTestList());

            MainWindow.AddTestRunner(args.ToString());
            MainWindow.RunCommands();
            return true;
        }

        public override int Find(string text, int position)
        {
            return MainWindow.TestsTree.Find(text.Trim(), position);
        }

        public static string GetTestList()
        {
            var testList = new List<string>();
            foreach (TreeNode node in MainWindow.TestsTree.Nodes[0].Nodes)
                GetCheckedTests(node, testList, MainWindow.SkipCheckedTests.Checked);
            if (testList.Count == 0)
                return "";
            var testListFile = Path.Combine(MainWindow.RootDir, "SkylineTester test list.txt");
            File.WriteAllLines(testListFile, testList);
            return " test=\"@{0}\"".With(testListFile);
        }

        public static void GetCheckedTests(TreeNode node, List<string> testList, bool skipTests = false)
        {
            foreach (TreeNode childNode in node.Nodes)
            {
                if (childNode.Checked == !skipTests)
                    testList.Add(childNode.Text);
            }
        }

        public void CheckAll()
        {
            foreach (var node in MainWindow.TestsTree.Nodes)
            {
                ((TreeNode)node).Checked = true;
                CheckAllChildNodes((TreeNode)node, true);
            }
        }

        public void UncheckAll()
        {
            foreach (var node in MainWindow.TestsTree.Nodes)
            {
                ((TreeNode)node).Checked = false;
                CheckAllChildNodes((TreeNode)node, false);
            }
        }

        // Updates all child tree nodes recursively.
        public static void CheckAllChildNodes(TreeNode treeNode, bool nodeChecked)
        {
            foreach (TreeNode node in treeNode.Nodes)
            {
                node.Checked = nodeChecked;
                if (node.Nodes.Count > 0)
                {
                    // If the current node has child nodes, call the CheckAllChildsNodes method recursively.
                    CheckAllChildNodes(node, nodeChecked);
                }
            }
        }
    }
}
