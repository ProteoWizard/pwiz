/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that "Apply Peak" on a removed peak removes the peak from all replicates and does not crash
    /// even if a precursor is missing chromatograms.
    /// </summary>
    [TestClass]
    public class RemovePeakFromAllTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRemovePeakFromAll()
        {
            TestFilesZip = @"TestFunctional\RemovePeakFromAllTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RemovePeakFromAllTest.sky"));
                // Select the second precursor and the second replicate.
                SkylineWindow.SelectedResultsIndex = 1;
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 1);
                // The first replicate is missing chromatograms for the second precursor
            });
            WaitForDocumentLoaded();
            WaitForGraphs();
            RunUI(() =>
            {
                SkylineWindow.RemovePeak();
                SkylineWindow.ApplyPeak(false, false);
            });
        }
    }
}
