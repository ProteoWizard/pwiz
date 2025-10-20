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
using pwiz.Common.PeakFinding;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{

    [TestClass]
    public class PeakImputationWithBatchesTest : AbstractFunctionalTest
    {
        private const string LIBRARY_NAME = "MyLibrary";
        private const string DOCUMENT_NAME = "SkylineDocument.sky";
        private const string SEQ_GSF = "GSFPVEAVK";
        private const string SEQ_DVK = "DVKPSNILLDER";
        [TestMethod]
        public void TestPeakImputationWithBatches()
        {
            TestFilesZip = @"TestFunctional\PeakImputationWithBatchesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(()=>peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library);
                RunLongDlg<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList,
                    libListDlg =>
                    {
                        RunDlg<EditLibraryDlg>(libListDlg.AddItem, addLibDlg =>
                        {
                            addLibDlg.LibraryName = LIBRARY_NAME;
                            addLibDlg.LibraryPath = TestFilesDir.GetTestPath("PeakImputationBatchTest.blib");
                            addLibDlg.OkDialog();
                        });
                    }, libListDlg=>libListDlg.OkDialog());
                RunUI(() =>
                {
                    peptideSettingsUi.PickedLibraries = new[] { LIBRARY_NAME };
                });
            }, peptideSettingsUi=>peptideSettingsUi.OkDialog());
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUi.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettingsUi.Peaks = 9;
                transitionSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();

            Assert.AreEqual(0, SkylineWindow.Document.PeptideCount);
            RunLongDlg<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries, viewLibraryDlg =>
                {
                    RunLongDlg<FilterMatchedPeptidesDlg>(viewLibraryDlg.AddAllPeptides, filterMatchedPeptidesDlg =>
                    {
                        var alertDlg = ShowDialog<MultiButtonMsgDlg>(filterMatchedPeptidesDlg.OkDialog);
                        OkDialog(alertDlg, alertDlg.OkDialog);
                    }, _ => { });
                },
                viewLibraryDlg => viewLibraryDlg.Close());
            Assert.AreNotEqual(0, SkylineWindow.Document.PeptideCount);
            RunUI(()=>SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(DOCUMENT_NAME)));
            ImportResultsFiles(new []
            {
                "TRX_Phase2_Pelt-P04_Ast_Neo_REF-DIRP2-025_005.mzML",
                "TRX_Phase2_Pelt-P04_Ast_Neo_TRX-TE-MSP-2284_105.mzML"
            }.Select(path=>new MsDataFilePath(path)));
            var peakBoundsGsf = GetPeakBounds(SEQ_GSF);
            CollectionAssert.AreEqual(new object[2], peakBoundsGsf);
            var peakBoundsDvk = GetPeakBounds(SEQ_DVK);
            Assert.IsNull(peakBoundsDvk[0]);
            Assert.IsNotNull(peakBoundsDvk[1]);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.ImputeMissingPeaks = true;
                peptideSettingsUi.OkDialog();
            });
            RunLongDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg=>dlg.Rescore(false));
            }, _=>{});
            WaitForDocumentLoaded();
            var peakBoundsGsfImputeMissing = GetPeakBounds(SEQ_GSF);
            Assert.IsNotNull(peakBoundsGsfImputeMissing[0]);
            Assert.IsNotNull(peakBoundsGsfImputeMissing[1]);
            var peakBoundsDvkImputeMissing = GetPeakBounds(SEQ_DVK);
            Assert.IsNotNull(peakBoundsDvkImputeMissing[0]);
            Assert.IsNotNull(peakBoundsDvkImputeMissing[1]);

            // TODO(nicksh): Set batch names and verify that imputed bounds come from same batch if possible
        }

        private List<PeakBounds> GetPeakBounds(string peptideSequence)
        {
            var document = SkylineWindow.Document;
            var peptideDocNode = document.Peptides.First(peptide => peptide.Peptide.Sequence == peptideSequence);
            var list = new List<PeakBounds>();
            for (int i = 0; i < document.MeasuredResults.Chromatograms.Count; i++)
            {
                var transitionGroupChromInfo =
                    peptideDocNode.TransitionGroups.First().GetSafeChromInfo(i).FirstOrDefault();
                if (transitionGroupChromInfo?.StartRetentionTime == null)
                {
                    list.Add(null);
                }
                else
                {
                    list.Add(new PeakBounds(transitionGroupChromInfo.StartRetentionTime.Value, transitionGroupChromInfo.EndRetentionTime.Value));
                }
            }

            return list;
        }
    }
}
