/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class InteractiveCommandLineToolTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestInteractiveCommandLineTool()
        {
            TestFilesZip = @"TestFunctional\InteractiveCommandLineToolTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, dlg=>
            {
                dlg.RemoveAllTools();
                dlg.OkDialog();
            });
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, dlg =>
            {
                dlg.InstallZipTool(TestFilesDir.GetTestPath("TestCommandLineInteractiveTool.zip"));
                dlg.OkDialog();
            });
            RunUI(()=>
            {
                SkylineWindow.PopulateToolsMenu();
                Assert.AreEqual("Delete Selected Node", SkylineWindow.GetToolText(0));
                Assert.AreEqual("Monitor Selection", SkylineWindow.GetToolText(1));
                Assert.AreEqual("Set Note On Selected Node", SkylineWindow.GetToolText(2));
                Assert.AreEqual("Shrink Peak Boundaries", SkylineWindow.GetToolText(3));
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MultiLabel.sky"));
            });
            IdentityPath idPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 3);
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = idPath;
                SkylineWindow.RunTool(2);
            });
            WaitForCondition(() =>
            {
                var transitionGroup = (TransitionGroupDocNode)SkylineWindow.Document.FindNode(idPath);
                return transitionGroup.Note == "Test Interactive Tool Note";
            });
            var originalPeakWidth = GetPeakWidth(idPath);
            Assert.AreNotEqual(0, originalPeakWidth);
            RunUI(()=>SkylineWindow.RunTool(3));
            WaitForCondition(() => originalPeakWidth != GetPeakWidth(idPath));
            var newPeakWidth = GetPeakWidth(idPath);
            Assert.AreEqual(originalPeakWidth / 2, newPeakWidth, .001);
        }

        private double GetPeakWidth(IdentityPath transitionGroupIdentityPath)
        {
            var transitionGroupDocNode = (TransitionGroupDocNode) SkylineWindow.Document.FindNode(transitionGroupIdentityPath);
            var transitionGroupChromInfo = transitionGroupDocNode.ChromInfos.FirstOrDefault();
            return transitionGroupChromInfo?.EndRetentionTime - transitionGroupChromInfo?.StartRetentionTime ?? 0;
        }
    }

}
