/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AutoTrainModelTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void AutoTrainModelFunctionalTest()
        {
            TestFilesZip = @"TestFunctional\AutoTrainModelTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip);
            SrmDocument doc = null;
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.OpenFile(testFilesDir.GetTestPath("test.sky")));
                doc = SkylineWindow.DocumentUI;
            });

            // Test that Skyline asks to generate decoys when importing results and we don't have any.
            Assert.AreEqual(FullScanAcquisitionMethod.DIA, doc.Settings.TransitionSettings.FullScan.AcquisitionMethod);
            Assert.IsTrue(!doc.PeptideGroups.Any(nodeGroup => nodeGroup.IsDecoy));
            var askDecoysDlg = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ImportResults);
            var generateDecoysDlg = ShowDialog<GenerateDecoysDlg>(askDecoysDlg.ClickYes);
            var numDecoys = doc.PeptideCount;
            RunUI(() =>
            {
                generateDecoysDlg.DecoysMethod = DecoyGeneration.SHUFFLE_SEQUENCE;
                generateDecoysDlg.NumDecoys = numDecoys;
            });
            var importResultsDlg = ShowDialog<ImportResultsDlg>(generateDecoysDlg.OkDialog);
            doc = WaitForDocumentChange(doc);
            Assert.AreEqual(doc.Peptides.Count(nodePep => nodePep.IsDecoy), numDecoys);
            OkDialog(importResultsDlg, importResultsDlg.CancelDialog);

            // Test that Skyline doesn't ask us to generate decoys when we already have them.
            importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunDlg<OpenDataSourceDialog>(() => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFile(null),
                openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectFile(testFilesDir.GetTestPath("CAexample.mzXML"));
                    openDataSourceDialog.Open();
                });
            WaitForConditionUI(() => importResultsDlg.NamedPathSets != null);
            OkDialog(importResultsDlg, importResultsDlg.OkDialog);

            // Test that the auto train property gets set on the document.
            doc = WaitForDocumentChange(doc);
            Assert.AreEqual(PeptideIntegration.AutoTrainType.default_model, doc.Settings.PeptideSettings.Integration.AutoTrain);
            WaitForDocumentChangeLoaded(doc, 30000);
            WaitForClosedAllChromatogramsGraph();

            // Test that the peak scoring model is trained.
            WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained);
            RunUI(() =>
            {
                // Test that all targets have z-scores and q-values.
                doc = SkylineWindow.DocumentUI;
                var precursors = doc.PeptideTransitionGroups.Where(nodeTranGroup => !nodeTranGroup.IsDecoy).ToArray();
                var chromInfos = precursors.SelectMany(nodeTranGroup => nodeTranGroup.Results)
                    .SelectMany(chromInfoList => chromInfoList).ToArray();
                Assert.AreEqual(precursors.Length, chromInfos.Count(chromInfo => chromInfo.ZScore.HasValue));
                Assert.AreEqual(precursors.Length, chromInfos.Count(chromInfo => chromInfo.QValue.HasValue));

                SkylineWindow.SaveDocument();
            });
        }
    }
}
