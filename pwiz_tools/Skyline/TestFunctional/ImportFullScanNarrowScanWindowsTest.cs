/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that chromatogram extraction respects narrow scan windows in full-scan MS2 data.
    /// When a spectrum has a narrow scan window (e.g. 20 m/z), product ions outside that
    /// window should not contribute to chromatograms.
    /// </summary>
    [TestClass]
    public class ImportFullScanNarrowScanWindowsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportFullScanNarrowScanWindows()
        {
            TestFilesZip = @"TestFunctional\ImportFullScanNarrowScanWindows.data";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Open the document - has one peptide GDVTAQIALQPALK with light and heavy precursors,
            // each with y7 and y4 transitions. Full-scan settings: PRM, TOF.
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("narrow_scan_windows.sky")));

            // Import the mzMLb results file. It contains 17 MS2 spectra for the heavy precursor
            // (716.916 m/z), alternating between two narrow scan windows:
            //   Experiment 926: scan window 738.48-758.48 (covers heavy y7 at 748.48)
            //   Experiment 927: scan window 426.30-446.30 (covers heavy y4 at 436.30)
            ImportResultsFile(TestFilesDir.GetTestPath("narrow_scan_windows.mzMLb"));

            var document = SkylineWindow.Document;
            var measuredResults = document.Settings.MeasuredResults;
            Assert.IsNotNull(measuredResults);
            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var chromatogramSet = measuredResults.Chromatograms.Single();

            var peptide = document.Molecules.Single();

            // Test the heavy precursor - its spectra are in the data file
            var heavyGroup = peptide.TransitionGroups.First(g => !g.IsLight);
            Assert.IsTrue(measuredResults.TryLoadChromatogram(chromatogramSet, peptide, heavyGroup,
                tolerance, out var chromGroupInfos));
            Assert.IsNotNull(chromGroupInfos);
            Assert.AreEqual(1, chromGroupInfos.Length);
            var chromGroupInfo = chromGroupInfos[0];
            foreach (var transition in heavyGroup.Transitions)
            {
                var chromInfo = chromGroupInfo.GetTransitionInfo(transition, tolerance, TransformChrom.raw);
                Assert.IsNotNull(chromInfo,
                    $"Heavy {transition.FragmentIonName} should have chromatogram data");

                var intensities = chromInfo.TimeIntensities.Intensities;
                int totalCount = intensities.Count;
                int nonZeroCount = intensities.Count(i => i > 0);

                // Each transition should have fewer total points than the 17 raw spectra,
                // because time points from spectra whose scan window doesn't cover this
                // transition's product m/z should be filtered out.
                Assert.IsTrue(totalCount < 17,
                    string.Format("Heavy {0} should have fewer points than 17 raw spectra (got {1})",
                        transition.FragmentIonName, totalCount));

                // Both transitions should have real signal
                Assert.IsTrue(nonZeroCount > 0,
                    string.Format("Heavy {0} should have non-zero intensities",
                        transition.FragmentIonName));

                // The non-zero points should not be interleaved with zeros from wrong scan windows.
                // Find the first and last non-zero indices - within that range, most points should
                // have signal (allowing for some zero points at the edges of the elution peak).
                int firstNonZero = -1, lastNonZero = -1;
                for (int i = 0; i < totalCount; i++)
                {
                    if (intensities[i] > 0)
                    {
                        if (firstNonZero < 0)
                            firstNonZero = i;
                        lastNonZero = i;
                    }
                }
                int peakRange = lastNonZero - firstNonZero + 1;
                // Within the peak range, at least half of points should have signal
                // (no alternating 0/signal/0/signal pattern from wrong scan windows)
                Assert.IsTrue(nonZeroCount * 2 >= peakRange,
                    string.Format("Heavy {0}: peak range {1}-{2} ({3} points) but only {4} non-zero - " +
                                  "suggests interleaved zeros from wrong scan windows",
                        transition.FragmentIonName, firstNonZero, lastNonZero, peakRange, nonZeroCount));
            }
        }
    }
}
