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
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ExplicitMissingPeakBoundsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExplicitMissingPeakBounds()
        {
            TestFilesZip = @"TestFunctional\ExplicitMissingPeakBoundsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ExplicitMissingPeakBoundsTest.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("Dora_10152022_DIA_HeLa_100pct_A02_27.mzML"));
            ImportResultsFile(TestFilesDir.GetTestPath("Dora_10152022_DIA_HeLa_50pct_B04_43.mzML"));
            var document = SkylineWindow.Document;
            Assert.AreEqual(2, document.PeptideCount);
            Assert.AreEqual(2, document.MeasuredResults.Chromatograms.Count);
            RunUI(()=>SkylineWindow.ShowCandidatePeaks());
            var candidatePeaksForm = WaitForOpenForm<CandidatePeakForm>();
            for (int iPeptide = 0; iPeptide < 2; iPeptide++)
            {
                var peptideIdentityPath = document.GetPathTo((int)SrmDocument.Level.Molecules, iPeptide);
                var peptideDocNode = (PeptideDocNode) document.FindNode(peptideIdentityPath);
                RunUI(()=>SkylineWindow.SelectedPath = peptideIdentityPath);
                for (int iReplicate = 0; iReplicate < 2; iReplicate++)
                {
                    var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[iReplicate];
                    bool expectedMissingPeak = iPeptide == 1 && iReplicate == 0;
                    int expectedRowCount = expectedMissingPeak ? 0 : 1;

                    string message = string.Format("Peptide: {0} Replicate: {1}", peptideDocNode.Peptide,
                        chromatogramSet.Name);
                    RunUI(()=>SkylineWindow.ComboResults.SelectedIndex = iReplicate);
                    WaitForGraphs();
                    TryWaitForCondition(() => candidatePeaksForm.IsComplete && candidatePeaksForm.RowCount == expectedRowCount);
                    RunUI(() => Assert.AreEqual(expectedRowCount, candidatePeaksForm.RowCount, message));
                    document.Settings.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode,
                        peptideDocNode.TransitionGroups.First(),
                        document.Settings.TransitionSettings.Instrument.IonMatchMzTolerance,
                        out ChromatogramGroupInfo[] chromatogramGroups);
                    Assert.AreEqual(1, chromatogramGroups.Length, message);
                    var chromatogramGroupInfo = chromatogramGroups[0];
                    // The .mzML files have spectra that go from 30 to 36 minutes, but the retention time filter
                    // should have resulted in a shorter extraction length.
                    Assert.IsTrue(chromatogramGroupInfo.TimeIntensitiesGroup.MinTime > 31,
                        "{0} should be greater than 31. {1}", chromatogramGroupInfo.TimeIntensitiesGroup.MinTime, message);
                    Assert.IsTrue(chromatogramGroupInfo.TimeIntensitiesGroup.MaxTime < 35,
                        "{0} should be less than 35. {1}", chromatogramGroupInfo.TimeIntensitiesGroup.MaxTime, message);
                }
            }
        }

    }
}
