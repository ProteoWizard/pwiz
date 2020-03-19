/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RescoreRawChromatogramsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestRescoreRawChromatograms()
        {
            TestFilesZip = @"TestFunctional\RescoreRawChromatogramsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var docFileName = TestFilesDir.GetTestPath("ChromDataSetMatchingTest.sky");
            RunUI(()=>SkylineWindow.OpenFile(docFileName));
            ImportResults(TestFilesDir.GetTestPath("ChromDataSetMatchingTest.mzML"));
            WaitForDocumentLoaded();
            var originalDoc = SkylineWindow.Document;
            Assert.IsNotNull(originalDoc.Settings.MeasuredResults);
            Assert.IsTrue(originalDoc.Settings.MeasuredResults.IsLoaded);
            var originalChromatograms = ReadAllChromatograms(originalDoc);

            var manageResultsDialog = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            var rescoreResultsDlg = ShowDialog<RescoreResultsDlg>(manageResultsDialog.Rescore);
            OkDialog(rescoreResultsDlg, () => rescoreResultsDlg.Rescore(false));
            WaitForDocumentLoaded();
            RunUI(()=>SkylineWindow.OpenFile(docFileName));
            WaitForDocumentLoaded();
            var newDoc = SkylineWindow.Document;
            Assert.IsNotNull(newDoc.Settings.MeasuredResults);
            Assert.IsTrue(newDoc.Settings.MeasuredResults.IsLoaded);
            var newChromatograms = ReadAllChromatograms(newDoc);
            Assert.AreEqual(originalChromatograms.Count, newChromatograms.Count);
            for (int iChrom = 0; iChrom < originalChromatograms.Count; iChrom++) 
            {
                var originalGroup = originalChromatograms[iChrom];
                var newChromGroup = newChromatograms[iChrom];
                Assert.AreEqual(originalGroup.TransitionTimeIntensities.Count, newChromGroup.TransitionTimeIntensities.Count);
                for (int iTran = 0; iTran < originalGroup.TransitionTimeIntensities.Count; iTran++)
                {
                    var originalTimeIntensities = originalGroup.TransitionTimeIntensities[iTran];
                    var newTimeIntensities = newChromGroup.TransitionTimeIntensities[iTran];
                    Assert.AreEqual(originalTimeIntensities.Times, newTimeIntensities.Times);
                    Assert.AreEqual(originalTimeIntensities.Intensities, newTimeIntensities.Intensities);
                    Assert.AreEqual(originalTimeIntensities.MassErrors, newTimeIntensities.MassErrors);
                    Assert.AreEqual(originalTimeIntensities.ScanIds, newTimeIntensities.ScanIds);
                }
                Assert.AreEqual(originalGroup.NumInterpolatedPoints, newChromGroup.NumInterpolatedPoints);
            }
        }

        private List<TimeIntensitiesGroup> ReadAllChromatograms(SrmDocument document)
        {
            var list = new List<TimeIntensitiesGroup>();
            var measuredResults = document.Settings.MeasuredResults;
            var tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var molecule in document.Molecules)
            {
                foreach (var transitionGroup in molecule.TransitionGroups)
                {
                    ChromatogramGroupInfo[] chromatogramGroupInfos = null;
                    Helpers.TryTwice(() =>
                    {
                        Assert.IsTrue(measuredResults.TryLoadChromatogram(0, molecule, transitionGroup, tolerance, true, out chromatogramGroupInfos));
                    });
                    Assert.AreEqual(1, chromatogramGroupInfos.Length);
                    list.Add(chromatogramGroupInfos[0].TimeIntensitiesGroup);
                }
            }

            return list;
        }
    }
}
