/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Tools;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ToolServiceTest : AbstractFunctionalTestEx
    {
        private const string TOOL_NAME = "Test Interactive Tool";
        private const string FILE_NAME = "TestToolAPI.sky";

        [TestMethod]
        public void TestToolService()
        {
            Run(@"TestFunctional\ToolServiceTest.zip"); //Not L10N
        }

        protected override void DoTest()
        {
            OpenDocument(FILE_NAME);

            // Install and run the test tool.
            using (var tool = new Tool(
                TestFilesDir.GetTestPath(""),
                TOOL_NAME,
                "$(ToolDir)\\TestInteractiveTool.exe",
                "$(SkylineConnection)",
                string.Empty,
                true,
                "Peak Area"))
            {
                tool.Run();

                // Connect to a special server in the test tool that allows remote control.
                // NOTE: We manually Dispose instead of "using" so we can catch an expected exception.
                var toolClient = ConnectTool(TOOL_NAME, ToolConnection);

                // Select peptides.
                SelectPeptide(toolClient, 219, "VAQLPLSLK", "5600TT13-1070");
//                SelectPeptide(toolClient, 330, "ELSELSLLSLYGIHK", "5600TT13-1070");
//
//                // Select peptides in specific replicates.
//                SelectPeptideReplicate(toolClient, 83, "TDFGIFR", "5600TT13-1076");
//                SelectPeptideReplicate(toolClient, 313, "SAPLPNDSQAR", "5600TT13-1073");
//
                // Check document changes.
                CheckDocumentChanges(toolClient);

                // Check version and path.
                CheckVersion(toolClient);
                CheckPath(toolClient, FILE_NAME);

                // Kill the test tool.
                KillTool(toolClient);
                Thread.Sleep(500);

                // We expect a CommunicationException because we didn't shutdown the channel cleanly.
                try
                {
                    toolClient.Dispose();
                }
                catch (System.ServiceModel.CommunicationException)
                {
                }
            }

            // There is a race condition where undoing a change occasionally leaves the document in a dirty state.
            SkylineWindow.DiscardChanges = true;
        }

        private static void SelectPeptide(SkylineTool.SkylineToolClient tool, int index, string peptideSequence, string replicate)
        {
            tool.RunTest("select," + index);
            RunUI(() =>
            {
                Assert.AreEqual(peptideSequence, SkylineWindow.SelectedPeptideSequence);
                Assert.AreEqual(replicate, SkylineWindow.ResultNameCurrent);
            });
        }

        private static void SelectPeptideReplicate(SkylineTool.SkylineToolClient tool, int index, string peptideSequence, string replicate)
        {
            tool.RunTest("selectreplicate," + index);
            RunUI(() =>
            {
                Assert.AreEqual(peptideSequence, SkylineWindow.SelectedPeptideSequence);
                Assert.AreEqual(replicate, SkylineWindow.ResultNameCurrent);
            });
        }

        private static void CheckDocumentChanges(SkylineTool.SkylineToolClient tool)
        {
            Assert.AreEqual(0, tool.RunTest("documentchanges"));
            RunUI(SkylineWindow.EditDelete);
            Thread.Sleep(500);  // Wait for document change event to propagate
            Assert.AreEqual(1, tool.RunTest("documentchanges"));
            RunUI(SkylineWindow.Undo);
            Thread.Sleep(500);  // Wait for document change event to propagate
            Assert.AreEqual(2, tool.RunTest("documentchanges"));
        }

        private static void CheckVersion(SkylineTool.SkylineToolClient tool)
        {
            Assert.AreEqual(1, tool.RunTest("version"));
        }

        private static void CheckPath(SkylineTool.SkylineToolClient tool, string fileName)
        {
            Assert.AreEqual(1, tool.RunTest("path," + FILE_NAME));
        }

        private static void KillTool(SkylineTool.SkylineToolClient tool)
        {
            int toolProcessId = tool.RunTest("quit");
            var toolProcess = Process.GetProcessById(toolProcessId);
            toolProcess.Kill();
        }

        private static SkylineTool.SkylineToolClient ConnectTool(string toolName, string toolConnection)
        {
            while (true)
            {
                Thread.Sleep(500);
                try
                {
                    return new SkylineTool.SkylineToolClient(toolName, toolConnection);
                }
// ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                }
            }
        }

        private static string ToolConnection
        {
            get { return ToolMacros.GetSkylineConnection() + "-test"; }
        }
    }
}
