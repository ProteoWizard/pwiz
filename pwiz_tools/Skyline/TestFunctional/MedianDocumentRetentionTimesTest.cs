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
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MedianDocumentRetentionTimesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMedianDocumentRetentionTimes()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeManagerTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => { SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ThreeReplicates.sky")); });
            WaitForDocumentLoaded();
            Assert.IsNull(SkylineWindow.Document.Settings.DocumentRetentionTimes.MedianDocumentRetentionTimes);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.ChromatogramPeaks;
                peptideSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();
            var firstDocument = SkylineWindow.Document;
            var firstMedianRts = new AlignmentTarget.MedianDocumentRetentionTimes(firstDocument);
            Assert.AreEqual(firstMedianRts, firstDocument.Settings.DocumentRetentionTimes.MedianDocumentRetentionTimes);
            // Remove all the peaks for the first peptide. This should not impact the MedianDocumentRetentionTimes
            // because they are based on the original peak that Skyline chose, not the current peak
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.SelectedResultsIndex = 0;
                SkylineWindow.RemovePeak();
                SkylineWindow.SelectedResultsIndex = 1;
                SkylineWindow.RemovePeak();
                SkylineWindow.SelectedResultsIndex = 2;
                SkylineWindow.RemovePeak();
            });
            WaitForDocumentLoaded();
            var docWithRemovedPeaks = SkylineWindow.Document;
            Assert.AreEqual(firstMedianRts, new AlignmentTarget.MedianDocumentRetentionTimes(docWithRemovedPeaks));
            // Deleting the first peptide should change the MedianDocumentRetentionTimes
            RunUI(() => SkylineWindow.EditDelete());
            WaitForDocumentLoaded();
            var docWithDeletedPeptide = SkylineWindow.Document;
            var medianRtsDeletedPeptide = new AlignmentTarget.MedianDocumentRetentionTimes(docWithDeletedPeptide);
            Assert.AreNotEqual(firstMedianRts, medianRtsDeletedPeptide);
            Assert.AreEqual(medianRtsDeletedPeptide, docWithDeletedPeptide.Settings.DocumentRetentionTimes.MedianDocumentRetentionTimes);
        }
    }
}
