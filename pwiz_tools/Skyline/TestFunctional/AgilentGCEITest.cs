/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
    /// Load a small agilent results file with GC (Gas Chromatography) EI (Electron Ionization) data
    /// and check against curated results.
    /// Actually it's an mzML file of the middle 6 seconds in a larger Agilent file  but it still tests
    /// the GC/EI code for MSP library input, and chromatogram extraction.
    /// </summary>
    [TestClass]
    public class AgilentGCEITest : AbstractFunctionalTestEx
    {

        [TestMethod]
        public void AgilentGCEIChromatogramTest()
        {
            TestFilesZip = @"TestFunctional\AgilentGCEITest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var docPath = TestFilesDir.GetTestPath(@"AgilentGCEITest.sky");
            OpenDocument(docPath);

            // Read in a small .MSP file describing GC EI data as MS1
            AddToDocumentFromSpectralLibrary("GC EI test", TestFilesDir.GetTestPath(@"test.MSP"));

            // Extract chromatograms from an Agilent file describing GC EI data as MS1
            ImportResults(new[] { TestFilesDir.GetTestPath(@"cmsptc_00000_20230106_SCFA_1.mzML") });

            // If data has not been properly understood as GC EI all-ions, some peaks won't be found
            var nPeaksFound = 0;
            foreach (var tg in SkylineWindow.Document.MoleculeTransitions)
            {
                foreach (var r in tg.Results)
                {
                    foreach (var peak in r)
                    {
                        if (peak.Area > 0) 
                        {
                            nPeaksFound++;
                            AssertEx.IsTrue((peak.PointsAcrossPeak ?? 0) > 20);
                        }
                    }
                }
            }
            AssertEx.AreEqual(23, nPeaksFound);
        }
    }
}