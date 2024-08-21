/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that a warning is displayed in the Immediate Window when Skyline discards chromatograms because
    /// the extracted chromatogram does not overlap with the Explicit Retention Time
    /// </summary>
    [TestClass]
    public class ImmediateWindowWarningsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImmediateWindowWarnings()
        {
            TestFilesZip = @"TestFunctional\ImmediateWindowWarningsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("WarningsTest.sky")));
            var peptideDocNode =
                SkylineWindow.Document.Peptides.FirstOrDefault(pep => 100 == pep.ExplicitRetentionTime?.RetentionTime);
            Assert.IsNotNull(peptideDocNode, "Unable to find peptide with explicit retention time 100");
            WaitForDocumentLoaded();
            Assert.IsNull(FindOpenForm<ImmediateWindow>(), "Immediate window should not be visible yet");
            ImportResultsFile(TestFilesDir.GetTestPath("S_1.mzML"));
            TryWaitForOpenForm<ImmediateWindow>();

            var immediateWindow = FindOpenForm<ImmediateWindow>();
            Assert.IsNotNull(immediateWindow, "Immediate window should be shown");

            RunUI(() =>
            {
                string warningMessage = GetExpectedWarningMessage();
                string immediateWindowText = immediateWindow.TextContent;
                StringAssert.Contains(immediateWindowText, warningMessage);
            });
        }

        /// <summary>
        /// Verifies that the warning message about explicit retention time is output to the console
        /// when running from the commandline.
        /// </summary>
        [TestMethod]
        public void TestCommandLineWarnings()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestFunctional\ImmediateWindowWarningsTest.zip");
            var output = RunCommand(
                "--in=" + TestFilesDir.GetTestPath("WarningsTest.sky"),
                "--import-file=" + TestFilesDir.GetTestPath("S_1.mzML"));
            StringAssert.Contains(output, GetExpectedWarningMessage());
        }

        private string GetExpectedWarningMessage()
        {
            return string.Format(
                ResultsResources
                    .PeptideChromDataSets_FilterByRetentionTime_Discarding_chromatograms_for___0___because_the_explicit_retention_time__1__is_not_between__2__and__3_,
                "TIAQYAR", 100, 0.717615, 89.89957);
        }
    }
}
