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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakImputationImportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakImputationImport()
        {
            TestFilesZip = @"TestFunctional\PeakImputationImportTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PeakImputationImportTest.sky")));
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.None;
                peptideSettingsUi.MaxRtShift = 0.1;
                peptideSettingsUi.OkDialog();
            });
            ImportResultsFiles(GetDataFilePaths());
            AssertOriginalPeaks(SkylineWindow.Document.MoleculeTransitionGroups.First(), true, true, false);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.None;
                peptideSettingsUi.MaxPeakWidthVariation = 0.1;
                peptideSettingsUi.OkDialog();
            });
            AssertOriginalPeaks(SkylineWindow.Document.MoleculeTransitionGroups.First(), false, true, false);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.None;
                peptideSettingsUi.MaxRtShift = null;
                peptideSettingsUi.MaxPeakWidthVariation = null;
                peptideSettingsUi.OkDialog();
            });
            AssertOriginalPeaks(SkylineWindow.Document.MoleculeTransitionGroups.First(), true, true, true);
        }

        private IEnumerable<MsDataFileUri> GetDataFilePaths()
        {
            return new[]
            {
                "004_Phase2b_MSP-P01_TRX-TE-MSP-3001_A03.mzML",
                "045_Phase2b_MSP-P01_TRX-TE-MSP-3026_D05.mzML",
                "047_Phase2b_MSP-P01_TRX-TE-MSP-3028_D07.mzML"
            }.Select(fileName => new MsDataFilePath(TestFilesDir.GetTestPath(fileName)));
        }

        private void AssertOriginalPeaks(TransitionGroupDocNode transitionGroupDocNode, params bool[] expectedValues)
        {
            for (int i = 0; i < expectedValues.Length; i++)
            {
                var chromInfo = transitionGroupDocNode.Results[i][0];
                Assert.AreEqual(expectedValues[i], IsOriginalPeakBounds(chromInfo));
            }
        }

        private bool IsOriginalPeakBounds(TransitionGroupChromInfo transitionGroupChromInfo)
        {
            return Equals(transitionGroupChromInfo.StartRetentionTime,
                transitionGroupChromInfo.OriginalPeak?.StartTime) && Equals(transitionGroupChromInfo.EndRetentionTime,
                transitionGroupChromInfo.OriginalPeak?.EndTime);
        }
    }
}
