/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests chromatogram extraction when the same light peptide appears multiple times in a document,
    /// and has different heavy peptides which have different MS2 ID times.
    /// Validates that Skyline does not choke extracting chromatograms.
    /// </summary>
    [TestClass]
    public class ChromatogramExtractionRangeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChromatogramExtractionRange()
        {
            TestFilesZip = @"TestFunctional\ChromatogramExtractionRangeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ExtractionRangeTest.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("S_1.mzML"));
            var document = SkylineWindow.Document;
            Assert.AreEqual(2, document.MoleculeCount);
            foreach (var molecule in document.Molecules)
            {
                foreach (var transitionGroup in molecule.TransitionGroups)
                {
                    var measuredResults = document.MeasuredResults;
                    float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                    Assert.IsNotNull(measuredResults);
                    Assert.IsTrue(measuredResults.TryLoadChromatogram(measuredResults.Chromatograms[0], molecule, transitionGroup, tolerance, out var chromatogramGroupInfos));
                    Assert.AreEqual(1, chromatogramGroupInfos.Length);
                    Assert.IsNotNull(chromatogramGroupInfos[0]);
                }
            }
        }
    }
}
