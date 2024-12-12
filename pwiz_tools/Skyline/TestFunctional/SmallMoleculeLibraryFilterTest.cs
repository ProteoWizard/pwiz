/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SmallMoleculeLibraryFilterTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestSmallMoleculeLibraryFilter()
        {
            TestFilesZip = @"TestFunctional\SmallMoleculeLibraryFilterTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Test the use of filters when adding small molecules from spectral library
        /// </summary>
        protected override void DoTest()
        {
            for (var pass = 0; pass < 5; pass++)
            {
                var docOrig = OpenDocument(TestFilesDir.GetTestPath("SmallMoleculeLibraryFilterTest.sky")); // For settings
                var transitionSettings = docOrig.Settings.TransitionSettings.ChangeLibraries(docOrig.Settings.TransitionSettings.Libraries.ChangeIonCount(8));
                var expectedTransitionsCount = 10;
                if (pass >= 1)
                {
                    // Remove low-mz filter
                    transitionSettings = transitionSettings.ChangeInstrument(transitionSettings.Instrument.ChangeMinMz(50));
                    expectedTransitionsCount += 8; // Adds a bunch of low mz transitions
                    if (pass >= 2)
                    {
                        // Allow unfragmented precursor transitions
                        var filter = transitionSettings.Filter.ChangePrecursorMzWindow(0); 
                        expectedTransitionsCount += 2; // Adds unfragmented precursor MS2 transitions
                        if (pass == 3)
                        {
                            // No precursor transition
                            filter = filter.ChangeSmallMoleculeIonTypes(new[] { IonType.custom });
                            expectedTransitionsCount -= 4; // Removes the M and M+1 precursors
                        }

                        if (pass == 4)
                        {
                            // Require +2 charge on fragments (of which there aren't any)
                            filter = filter.ChangeSmallMoleculeFragmentAdducts(new[] { Adduct.M_PLUS_2 });
                            expectedTransitionsCount = 4; // Removes all but the M and M+1 precursors
                        }

                        transitionSettings = transitionSettings.ChangeFilter(filter);
                    }

                    var settings = docOrig.Settings.ChangeTransitionSettings(transitionSettings);
                    RunUI(() => SkylineWindow.ChangeSettings(settings, true));
                    docOrig = WaitForDocumentChange(docOrig);
                }

                var docAfter =
                    AddToDocumentFromSpectralLibrary("test_tox_1", TestFilesDir.GetTestPath("test_tox_1.blib"));

                AssertEx.IsDocumentState(docAfter, null, 1, 2, expectedTransitionsCount);
                LoadNewDocument(true); // Reset
            }
        }
    }
}
