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
using pwiz.Skyline.Alerts;
using TestRunnerLib;

namespace SkylineTester
{
    public class TabTests : TabBase
    {
        private static int parallelInvocationsCount; // Number of times we've launched parallel tests from here
        public override void Enter()
        {
            MainWindow.DefaultButton = MainWindow.RunTests;
        }

        public override bool Run()
        {
            if (Equals(MainWindow.RunTestMode.SelectedItem.ToString(), "Quality"))
            {
                MainWindow.QualityPassDefinite.Checked = true;
                MainWindow.QualityPassCount.Value = 1;
                MainWindow.Pass0.Checked = true;
                MainWindow.Pass1.Checked = true;
                MainWindow.QualityRunSmallMoleculeVersions.Checked = MainWindow.TestsRunSmallMoleculeVersions.Checked;
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

            if (Equals(MainWindow.RunTestMode.SelectedItem.ToString(), "Demo"))
                args.Append(" demo=on");
            if (Equals(MainWindow.RunTestMode.SelectedItem.ToString(), "Screenshots"))
                args.Append(" pause=-1"); // Magic number that tells TestRunner to pause and show UI for a manual screenshot
            if (Equals(MainWindow.RunTestMode.SelectedItem.ToString(), "Covershot"))
                args.Append(" pause=-2"); // Magic number that tells TestRunner to grab tutorial cover shot then move on to next test

            if (MainWindow.TestsRunSmallMoleculeVersions.Checked)
                args.Append(" runsmallmoleculeversions=on");

            if (MainWindow.TestsRandomize.Checked)
                args.Append(" random=on");

            if (MainWindow.TestsRecordAuditLogs.Checked)
                args.Append(" recordauditlogs=on");

            int count;
            if (!int.TryParse(MainWindow.TestsRepeatCount.Text, out count))
                count = 0;
            if (count > 0)
                args.Append(" repeat=" + MainWindow.TestsRepeatCount.Text);

            var cultures = new List<CultureInfo>();
            if (MainWindow.TestsChinese.Checked)
                cultures.Add(new CultureInfo("zh-CHS"));
            if (MainWindow.TestsFrench.Checked)
                cultures.Add(new CultureInfo("fr-FR"));
            if (MainWindow.TestsJapanese.Checked)
                cultures.Add(new CultureInfo("ja"));
            if (MainWindow.TestsTurkish.Checked)
                cultures.Add(new CultureInfo("tr-TR"));
            if (MainWindow.TestsEnglish.Checked || cultures.Count == 0)
                cultures.Insert(0, new CultureInfo("en-US"));

            args.Append(" language=");
            args.Append(String.Join(",", cultures));
            if (GetTestList().Length > 0)
                args.Append(" perftests=on"); // In case any perf tests were explicitly selected - no harm if they weren't

            if (MainWindow.RunParallel.Checked)
            {
                // CONSIDER: Should we add a checkbox for this?
                // args.Append(" keepworkerlogs=1"); // For debugging startup issues. Look for TestRunner-docker-worker_#-docker.log files in pwiz root
                args.AppendFormat(" parallelmode=server workercount={0}", MainWindow.RunParallelWorkerCount.Value);
                if (parallelInvocationsCount++ > 0)
                {
                    // Allows unique naming of workers between runs, helpful if a run is canceled then quickly restarted and
                    // workers haven't shut down, as often happens when you realize you've forgotten to select certain tests etc
                    args.AppendFormat(" invocation={0}", parallelInvocationsCount-1); 
                }
                try
                {
                    var dockerImagesOutput = RunTests.RunCommand("docker", $"images {RunTests.DOCKER_IMAGE_NAME}", RunTests.IS_DOCKER_RUNNING_MESSAGE);
                    if (!dockerImagesOutput.Contains(RunTests.DOCKER_IMAGE_NAME))
                    {
                        MainWindow.CommandShell.Log($"'{RunTests.DOCKER_IMAGE_NAME}' is missing; building it now.");
                        if (!File.Exists(RunTests.ALWAYS_UP_SERVICE_EXE))
                        {
                            MainWindow.CommandShell.Log("Prompting for AlwaysUpCLT password. " +
                                                        "Get it from https://skyline.ms/parallelmode.url");
                            MainWindow.CommandShell.UpdateLog();
                            var passwordDictionary = new Dictionary<string, string[]> { { "Password", new[] { "" } } };
                            KeyValueGridDlg.Show(MainWindow, "Enter password for AlwaysUpCLT", passwordDictionary, v => v[0], (s, v) => v[0] = s);
                            args.AppendFormat(" alwaysupcltpassword=\"{0}\"", passwordDictionary["Password"][0]);
                        }
                    }
                }
                catch (InvalidOperationException e)
                {
                    MainWindow.CommandShell.Log(e.Message);
                    MainWindow.CommandShell.UpdateLog();
                    return false;
                }
            }

            args.Append(GetTestList());

            MainWindow.AddTestRunner(args.ToString());
            MainWindow.RunCommands();
            return true;
        }

        public override void Cancel()
        {
            base.Cancel();

            if (MainWindow.RunParallel.Checked)
                RunTests.SendDockerKill();
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

        public void SetTests(HashSet<string> testSet)
        {
            UncheckAll();
            foreach (TreeNode node in MainWindow.TestsTree.Nodes[0].Nodes)
            {
                foreach (TreeNode childNode in node.Nodes)
                {
                    childNode.Checked = testSet.Contains(childNode.Text);
                }
            }            
            GetTestList(); // Updates the test list file contents
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
