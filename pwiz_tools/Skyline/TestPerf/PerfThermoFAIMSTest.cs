/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

//
// Test for proper handling of Thermo FAIMS ion mobility data in PDResult files
// 

namespace TestPerf // Tests in this namespace are skipped unless the RunPerfTests attribute is set true
{
    [TestClass]
    public class PerfThermoFAIMSTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestThermoFAIMS()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfThermoFAIMS.zip");
            TestFilesPersistent = new[] { "032818_Lumos_FAIMS_SILAC_3CVstep_fx_03.pdResult", "032818_Lumos_FAIMS_SILAC_3CVstep_fx_03.raw" }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        private IEnumerable<string> SearchFiles
        {
            get { yield return GetTestPath(TestFilesPersistent[0]); }
        }

        protected override void DoTest()
        {
            // Open the empty .sky file (has no peptides)
            const string documentFile = "ThermoFAIMSTest.sky";
            PrepareDocument(documentFile);

            // Prepare for full scan 
            RunUI(() => SkylineWindow.ModifyDocument("Set Orbitrap full-scan settings", doc => doc.ChangeSettings(
                doc.Settings.ChangeTransitionFullScan(fs => fs
                .ChangeAcquisitionMethod(FullScanAcquisitionMethod.Targeted, null)
                .ChangePrecursorResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400)
                .ChangeProductResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400))
                .ChangeTransitionSettings(ts => ts.ChangeFilter(ts.Filter
                .ChangePeptidePrecursorCharges(new[] { Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED })
                .ChangePeptideProductCharges(new[] { Adduct.SINGLY_PROTONATED, Adduct.DOUBLY_PROTONATED })
                .ChangePeptideIonTypes(new[] { IonType.y, IonType.precursor })))
                .ChangeTransitionSettings(ts => ts.ChangeIonMobilityFiltering(ts.IonMobilityFiltering.ChangeFilterWindowWidthCalculator(
                        new IonMobilityWindowWidthCalculator(IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power, 20, 0, 0, 0))
                        .ChangeUseSpectralLibraryIonMobilityValues(true))))));

            TestWizardBuildDocumentLibraryAndFinish(documentFile);

            TestPopulateDocumentFromLibrary();

            // Now import chromatograms, check that FAIMS filtering is working
            ImportResultsFile(GetTestPath(TestFilesPersistent[1]));

            // First precursor area will be less than 7613528 @ RT47.4 if we aren't treating CV data properly (397369 @ RT47.52 instead)
            Assume.IsTrue(SkylineWindow.Document.MoleculeTransitions.First().GetPeakArea(-1) >= 7600000);
        }

        private void TestPopulateDocumentFromLibrary()
        {
            // Launch the Library Explorer dialog
            var viewLibUI = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);

            // Ensure the appropriate default library is selected
            ComboBox libComboBox = null;
            ListBox pepList = null;
            RunUI(() =>
            {
                libComboBox = (ComboBox)viewLibUI.Controls.Find("comboLibrary", true)[0];
                Assert.IsNotNull(libComboBox);

                // Find the peptides list control
                pepList = (ListBox)viewLibUI.Controls.Find("listPeptide", true)[0];
                Assert.IsNotNull(pepList);
            });

            // Initially, peptide with index 0 should be selected
            WaitForConditionUI(() => pepList.SelectedIndex != -1);
            var modDlg = WaitForOpenForm<AddModificationsDlg>();
            viewLibUI.IsUpdateComplete = false;
            RunUI(modDlg.OkDialogAll);
            // Wait for the list update caused by adding all modifications to complete
            WaitForConditionUI(() => viewLibUI.IsUpdateComplete);

            // Add all peptides
            var filterMatchedPeptidesDlg = ShowDialog<FilterMatchedPeptidesDlg>(viewLibUI.AddAllPeptides);
            var docBefore = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            using (new CheckDocumentState(1, 8433, 10882, 43484))
            {
                RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg.OkDialog, addLibraryPepsDlg =>
                {
                    addLibraryPepsDlg.Btn1Click();
                });
                WaitForDocumentChange(docBefore);
            }

            // Close the Library Explorer dialog
            OkDialog(viewLibUI, viewLibUI.CancelDialog);

        }

        private void TestWizardBuildDocumentLibraryAndFinish(string documentFile)
        {

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(SearchFiles);
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsEarlyFinishButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickEarlyFinishButton()));
            WaitForClosedForm(importPeptideSearchDlg);

            VerifyDocumentLibraryBuilt(documentFile);

            RunUI(() => SkylineWindow.SaveDocument());
        }


        private void VerifyDocumentLibraryBuilt(string path)
        {
            // Verify document library was built
            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(GetTestPath(path));
            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
            Assert.IsTrue(File.Exists(docLibPath) && File.Exists(redundantDocLibPath));
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(librarySettings.HasDocumentLibrary);
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings", doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(documentFile)));
        }
    }
}