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

using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ToolServiceTest : AbstractFunctionalTestEx
    {
        private const string TOOL_NAME = "Test Interactive Tool";
        private const string FILE_NAME = "TestToolAPI.sky";
        private TestToolClient _testToolClient;

        [TestMethod]
        public void TestToolService()
        {
            Run(@"TestFunctional\ToolServiceTest.zip"); //Not L10N
        }

        protected override void DoTest()
        {
            if (!IsEnableLiveReports)
                return;

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

                _testToolClient = new TestToolClient(ToolConnection + "-test");

                // Check inter-process communication.
                CheckCommunication();

                // Check version and path.
                CheckVersion();
                CheckPath(FILE_NAME);

                // Select peptides.
                SelectPeptide(219, "VAQLPLSLK", "5600TT13-1070");
                SelectPeptide(330, "ELSELSLLSLYGIHK", "5600TT13-1070");

                // Select peptides in specific replicates.
                SelectPeptideReplicate(83, "TDFGIFR", "5600TT13-1076");
                SelectPeptideReplicate(313, "SAPLPNDSQAR", "5600TT13-1073");

                // Check document changes.
                CheckDocumentChanges();

                // Exit the test tool.
                _testToolClient.Exit();
            }

            // There is a race condition where undoing a change occasionally leaves the document in a dirty state.
            SkylineWindow.DiscardChanges = true;
        }

        private void CheckCommunication()
        {
            _testToolClient.Timeout = -1;
            Assert.AreEqual(4.0f, _testToolClient.TestFloat(2.0f));
            var floatArray = _testToolClient.TestFloatArray();
            Assert.IsTrue(ArrayUtil.EqualsDeep(floatArray, new[] { 1.0f, 2.0f }));
            var stringArray = _testToolClient.TestStringArray();
            Assert.IsTrue(ArrayUtil.EqualsDeep(stringArray, new[] { "two", "strings" }));
            var versionArray = _testToolClient.TestVersionArray();
            Assert.IsTrue(ArrayUtil.EqualsDeep(versionArray, new[]
                {
                    new Version {Major = 1},
                    new Version {Minor = 2}
                }));
            var chromatogramArray = _testToolClient.TestChromatogramArray();
            Assert.IsTrue(ArrayUtil.EqualsDeep(chromatogramArray, new[]
                {
                    new Chromatogram
                    {
                        PrecursorMz = 1.0,
                        ProductMz = 2.0,
                        Times = new[] {1.0f, 2.0f},
                        Intensities = new[] {10.0f, 20.0f}
                    },
                    new Chromatogram
                    {
                        PrecursorMz = 2.0,
                        ProductMz = 4.0,
                        Times = new[] {3.0f, 4.0f, 5.0f},
                        Intensities = new[] {30.0f, 40.0f, 50.0f}
                    }
                }));
        }

        private void SelectPeptide(int index, string peptideSequence, string replicate)
        {
            _testToolClient.TestSelect(index.ToString("D"));
            RunUI(() =>
            {
                Assert.AreEqual(peptideSequence, SkylineWindow.SelectedPeptideSequence);
                Assert.AreEqual(replicate, SkylineWindow.ResultNameCurrent);
            });
        }

        private void SelectPeptideReplicate(int index, string peptideSequence, string replicate)
        {
            _testToolClient.TestSelectReplicate(index.ToString("D"));
            RunUI(() =>
            {
                Assert.AreEqual(peptideSequence, SkylineWindow.SelectedPeptideSequence);
                Assert.AreEqual(replicate, SkylineWindow.ResultNameCurrent);
            });
        }

        private void CheckDocumentChanges()
        {
            Assert.AreEqual(0, DocumentChangeCount);
            RunUI(SkylineWindow.EditDelete);
            Thread.Sleep(500);  // Wait for document change event to propagate
            Assert.AreEqual(1, DocumentChangeCount);
            RunUI(SkylineWindow.Undo);
            Thread.Sleep(500);  // Wait for document change event to propagate
            Assert.AreEqual(2, DocumentChangeCount);
        }

        private int DocumentChangeCount
        {
            get { return _testToolClient.GetDocumentChangeCount(); }
        }

        private void CheckVersion()
        {
            var version = _testToolClient.TestVersion();
            Assert.IsTrue(version.Major >= 0);
            Assert.IsTrue(version.Minor >= 0);
            Assert.IsTrue(version.Build >= 0);
            Assert.IsTrue(version.Revision >= 0);
        }

        private void CheckPath(string fileName)
        {
            var path = _testToolClient.TestDocumentPath();
            Assert.IsTrue(path.Contains(fileName));
        }

        private static string ToolConnection
        {
            get { return ToolMacros.GetSkylineConnection(); }
        }
    }
}
