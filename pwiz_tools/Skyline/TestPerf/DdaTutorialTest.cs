using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class DdaTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDdaSearch()
        {
            // Set true to look at tutorial screenshots.
            IsPauseForScreenShots = true;

            TestFilesZip = @"TestTutorial\DdaTutorialTest.zip";
            TestFilesPersistent = new[] { "34381_SGC_IP3_131219.raw", "34382_SGC_IP4_131219.raw" };
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
                    GetTestPath("34381_SGC_IP3_131219.raw"),
                    GetTestPath("34382_SGC_IP4_131219.raw")
                };
            }
        }

        protected override void DoTest()
        {
            TestAmandaSearch();
        }

        /// <summary>
        /// Test that the "Match Modifications" page of the Import Peptide Search wizard gets skipped.
        /// </summary>
        private void TestAmandaSearch()
        {
            PrepareDocument("TestDdaTutorial.sky");

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch = true;
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => new MsDataFilePath(o)).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;
                Assert.AreEqual(ImportPeptideSearchDlg.Workflow.dda, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Select DDA Files to Search page", 4);

            // We're on the "Match Modifications" page. Use document modifications.
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page));
            PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Import Peptide Search - Add Modifications page", 7);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the MS1 full scan settings page. Use document settings.
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page));
            PauseForScreenShot<ImportPeptideSearchDlg.Ms1FullScanPage>("Import Peptide Search - Configure MS1 Full-Scan Settings page", 8);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Import FASTA" page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("sequencedatabase_uniprot-proteome_AUP000005640_reviewed_Ayes.fasta"));
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page", 10);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Adjust Search Settings" page
            bool? searchSucceeded = null;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                importPeptideSearchDlg.SearchSettingsControl.SetPrecursorTolerance(new MzTolerance(5, MzTolerance.Units.ppm));
                importPeptideSearchDlg.SearchSettingsControl.SetFragmentTolerance(new MzTolerance(10, MzTolerance.Units.ppm));
                importPeptideSearchDlg.SearchSettingsControl.SetFragmentIons("b, y");

                importPeptideSearchDlg.SearchControl.OnSearchFinished += (success) => searchSucceeded = success;
            });
            PauseForScreenShot<ImportPeptideSearchDlg.DDASearchSettingsPage>("Import Peptide Search - DDA Search Settings page", 10);

            // Run the search
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // Wait for search to finish
            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            Assert.IsTrue(searchSucceeded.Value);

            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_page));
            PauseForScreenShot<ImportPeptideSearchDlg.DDASearchPage>("Import Peptide Search - DDA Search Progress page", 8);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            RunDlg<PeptidesPerProteinDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck, emptyProteinsDlg =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                emptyProteinsDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(1131, proteinCount);
                Assert.AreEqual(61, peptideCount);
                Assert.AreEqual(61, precursorCount);
                Assert.AreEqual(183, transitionCount);
                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(57, proteinCount);
                Assert.AreEqual(61, peptideCount);
                Assert.AreEqual(61, precursorCount);
                Assert.AreEqual(183, transitionCount);
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
