/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
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

            // test error parsing an MSAmanda installed file (enzymes.xml)
            //var skylineDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //File.Move(System.IO.Path.Combine(skylineDir, "enzymes.xml"), System.IO.Path.Combine(skylineDir, "not-the-enzymes-you-are-looking-for.xml"));

            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        private IEnumerable<string> SearchFiles
        {
            get
            {
                return new[]
                {
                    GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_02.mzML"),
                    GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_03.mzML")
                };
            }
        }

        protected override void DoTest()
        {
            TestAmandaSearch();
        }

        /// <summary>
        /// Quick test that DDA search works with MSAmanda.
        /// </summary>
        private void TestAmandaSearch()
        {
            PrepareDocument("TestDdaSearch.sky");

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch = true;
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => new MsDataFilePath(o)).ToArray();
                Assert.AreEqual(ImportPeptideSearchDlg.Workflow.dda, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType);
            });

            // Remove prefix/suffix dialog pops up; accept default behavior
            var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(removeSuffix, () => removeSuffix.YesDialog());
            WaitForDocumentLoaded();

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            // TODO: DDA searches should not look for result files (for DDA, the result files are the same as the search input, and for DIA, I'm not sure yet)
            /*
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            */

            // We're on the "Match Modifications" page. Add M+16 mod.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
            });

            // In PerformDDASearch mode, ClickAddStructuralModification launches edit list dialog
            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(importPeptideSearchDlg.MatchModificationsControl.ClickAddStructuralModification);
            RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
            {
                editModDlg.SetModification("Oxidation (M)", true); // Not L10N
                editModDlg.OkDialog();
            });
            OkDialog(editListUI, editListUI.OkDialog);

            // Test back/next buttons
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                // We're on the "Match Modifications" page again.
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(0, false); // uncheck C+57
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(1, true); // check M=16
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the MS1 full scan settings page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

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
                if (Environment.Is64BitProcess)
                {
                    // TODO: reenable these checks for 32 bit once intermittent failures are debugged
                    Assert.AreEqual(1131, proteinCount);
                    Assert.AreEqual(61, peptideCount);
                    Assert.AreEqual(61, precursorCount);
                    Assert.AreEqual(183, transitionCount);
                }
                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                if (Environment.Is64BitProcess)
                {
                    Assert.AreEqual(57, proteinCount);
                    Assert.AreEqual(61, peptideCount);
                    Assert.AreEqual(61, precursorCount);
                    Assert.AreEqual(183, transitionCount);
                }
                emptyProteinsDlg.OkDialog();
            });

            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings", 
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(documentFile)));
        }
    }
}
