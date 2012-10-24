/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the Import Peptide Search Wizard for MS1 Full-Scan Filtering
    /// </summary>
    [TestClass]
    public class ImportPeptideSearchWizard : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportPeptideSearchWizard()
        {
            TestFilesZip = ExtensionTestContext.CanImportAbWiff
                ? @"https://skyline.gs.washington.edu/tutorials/MS1Filtering.zip" // Not L10N
                : @"https://skyline.gs.washington.edu/tutorials/MS1FilteringMzml.zip"; // Not L10N
            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            var folderMs1Filtering = ExtensionTestContext.CanImportAbWiff ? "Ms1Filtering" : "Ms1FilteringMzml"; // Not L10N
            return TestFilesDir.GetTestPath(folderMs1Filtering + '\\' + path);
        }

        private string DocumentFile
        {
            get { return GetTestPath("ImportPeptideSearchWizardTest.sky"); }
        }

        private const string REPLICATE_BASE_NAME = "100803_0005b_MCF7_TiTip3";

        private IEnumerable<string> SearchFiles
        {
            get { yield return GetTestPath(REPLICATE_BASE_NAME + BiblioSpecLiteBuilder.EXT_PILOT_XML); }
        }

        protected override void DoTest()
        {
            TestWizardBasicFunctionality();

            TestWizardBuildDocumentLibraryAndFinish();

            TestWizardCancel();
        }

        private void TestWizardBasicFunctionality()
        {
            EmptyDocument();

            SrmDocument doc = SkylineWindow.Document;
            // Configure the peptide settings for the document.
            var peptideSettingsUI = ShowPeptideSettings();
            const string carbamidomethylCysteineName = "Carbamidomethyl Cysteine";
            const string phosphoStName = "Phospho (ST)";
            const string phosphoYName = "Phospho (Y)";
            const string oxidationMName = "Oxidation (M)";
            AddStaticMod(phosphoStName, true, peptideSettingsUI);
            AddStaticMod(phosphoYName, true, peptideSettingsUI);
            AddStaticMod(oxidationMName, true, peptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUI.PickedStaticMods = new[] { carbamidomethylCysteineName, phosphoStName, phosphoYName, oxidationMName };
                peptideSettingsUI.MissedCleavages = 2;
                peptideSettingsUI.OkDialog();
            });
            WaitForDocumentChange(doc);

            doc = SkylineWindow.Document;
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.PrecursorCharges = "2,3,4";
                transitionSettingsUI.ProductCharges = "1,2,3";
                transitionSettingsUI.UseLibraryPick = false;
                Assert.AreEqual(MassType.Monoisotopic, transitionSettingsUI.PrecursorMassType);
                transitionSettingsUI.OkDialog();
            });
            WaitForDocumentChange(doc);

            RunUI(() => SkylineWindow.SaveDocument());

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            doc = SkylineWindow.Document;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                            ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(SearchFiles);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            
            WaitForDocumentChange(doc);

            VerifyDocumentLibraryBuilt();

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the "Configure MS1 Full-Scan Settings" page of the wizard.
            doc = SkylineWindow.Document;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.ms1_full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                importPeptideSearchDlg.FullScanSettingsControl.Peaks = "3";
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            WaitForDocumentChange(doc);
            
            // Last page of wizard - Import Fasta.
            doc = SkylineWindow.Document;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("12_proteins.062011.fasta"));
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            WaitForClosedForm(importPeptideSearchDlg);
            doc = WaitForDocumentChangeLoaded(doc, 5 * 60 * 1000); // 5 minutes
            
            AssertEx.IsDocumentState(doc, null, 11, 40, 40, 120);
            RunUI(SkylineWindow.IntegrateAll);
            // Only WIFF file contains all of the results.  The mzML file had to be reduced.
            if (ExtensionTestContext.CanImportAbWiff)
                AssertResult.IsDocumentResultsState(doc, REPLICATE_BASE_NAME, 38, 38, 0, 106, 0);
            else
                AssertResult.IsDocumentResultsState(doc, REPLICATE_BASE_NAME, 7, 7, 0, 19, 0);

            // Skip the rest of the MS1 filtering tutorial, since it is not pertinent to testing the wizard.

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TestWizardCancel()
        {
            // Open the empty .sky file (has no peptides)
            EmptyDocument();

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            
            // We should be on the "Build Spectral Library" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                            ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.ClickCancelButton();
            });

            WaitForClosedForm(importPeptideSearchDlg);

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TestWizardBuildDocumentLibraryAndFinish()
        {
            // Open the empty .sky file (has no peptides)
            EmptyDocument();

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                            ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(SearchFiles);
                Assert.IsTrue(importPeptideSearchDlg.ClickEarlyFinishButton());
            });
            WaitForClosedForm(importPeptideSearchDlg);

            VerifyDocumentLibraryBuilt();

            RunUI(() => SkylineWindow.SaveDocument());
        }
        
        private void EmptyDocument()
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                            doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(DocumentFile));
        }

        private void VerifyDocumentLibraryBuilt()
        {
            // Verify document library was built
            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(DocumentFile);
            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
            Assert.IsTrue(File.Exists(docLibPath) && File.Exists(redundantDocLibPath));
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(librarySettings.DocumentLibrary);
        }
    }
}
