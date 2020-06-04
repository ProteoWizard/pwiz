/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DdaSearchTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDdaSearch()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";
            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        /*private const string MODS_BASE_NAME = "mods";
        private const string MODLESS_BASE_NAME = "modless";

        private IEnumerable<string> SearchFilesModless
        {
            get { yield return GetTestPath(MODLESS_BASE_NAME + BiblioSpecLiteBuilder.EXT_PRIDE_XML); }
        }*/

        private IEnumerable<string> SearchFiles
        {
            get { return new [] {GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_02.mz5"), GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_03.mz5")}; }
        }

        protected override void DoTest()
        {
            TestAmandaSearch();
        }

        private class EmptyProteinGroupSetter : IDisposable
        {
            public EmptyProteinGroupSetter(int emptyCount)
            {
                FastaImporter.TestMaxEmptyPeptideGroupCount = emptyCount;
            }

            public void Dispose()
            {
                FastaImporter.TestMaxEmptyPeptideGroupCount = null;
            }
        }

        /// <summary>
        /// Test that the "Match Modifications" page of the Import Peptide Search wizard gets skipped.
        /// </summary>
        private void TestAmandaSearch()
        {
            PrepareDocument("TestDdaSearch.sky");

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            SrmDocument doc = SkylineWindow.Document;

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch = true;
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(SearchFiles);
                Assert.AreEqual(ImportPeptideSearchDlg.Workflow.dda, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            //WaitForDocumentChange(doc);

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            // TODO: DDA searches should not look for result files (for DDA, the result files are the same as the search input, and for DIA, I'm not sure yet)
            /*RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });*/

            // We're on the "Match Modifications" page. Select C57 and M16
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                importPeptideSearchDlg.MatchModificationsControl.ChangeAll(true);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            //WaitForDocumentChange(doc);

            //PauseTest();

            // We're on the MS1 full scan settings page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            //WaitForDocumentChange(doc);

            // We're on the "Import FASTA" page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("rpal-subset.fasta"));
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the "Adjust Search Settings" page
            bool? searchSucceeded = null;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                importPeptideSearchDlg.SearchSettingsControl.SetPrecursorTolerance(new MzTolerance(10, MzTolerance.Units.ppm));
                importPeptideSearchDlg.SearchSettingsControl.SetFragmentTolerance(new MzTolerance(25, MzTolerance.Units.ppm));
                importPeptideSearchDlg.SearchSettingsControl.SetFragmentIons("b, y");

                // Run the search
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                importPeptideSearchDlg.SearchControl.OnSearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;
            });

            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            Assert.IsTrue(searchSucceeded.Value);

            RunDlg<PeptidesPerProteinDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck, emptyProteinsDlg =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                emptyProteinsDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(1131, proteinCount);
                Assert.AreEqual(52, peptideCount);
                Assert.AreEqual(52, precursorCount);
                Assert.AreEqual(156, transitionCount);
                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(49, proteinCount);
                Assert.AreEqual(52, peptideCount);
                Assert.AreEqual(52, precursorCount);
                Assert.AreEqual(156, transitionCount);
                emptyProteinsDlg.OkDialog();
            });


            //RunUI(importPeptideSearchDlg.CancelDialog);

            //WaitForClosedForm(importPeptideSearchDlg);
            WaitForDocumentLoaded();
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
