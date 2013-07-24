using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportPeptideSearchModificationsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportPeptideSearchModifications()
        {
            TestFilesZip = @"TestFunctional\ImportPeptideSearchModifications.zip";
            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        private string DocumentFile
        {
            get { return GetTestPath("ImportPeptideSearchModificationsTest.sky"); }
        }

        private const string MODS_BASE_NAME = "mods";
        private const string MODLESS_BASE_NAME = "modless";

        private IEnumerable<string> SearchFiles
        {
            get { yield return GetTestPath(MODS_BASE_NAME + BiblioSpecLiteBuilder.EXT_PRIDE_XML); }
        }

        private IEnumerable<string> SearchFilesModless
        {
            get { yield return GetTestPath(MODLESS_BASE_NAME + BiblioSpecLiteBuilder.EXT_PRIDE_XML); }
        }

        protected override void DoTest()
        {
            TestImportModifications();
            TestSkipWhenNoModifications();
        }

        /// <summary>
        /// Test that the "Match Modifications" page of the Import Peptide Search Wizard detects the correct matched/unmatched modifications and adds them.
        /// </summary>
        private void TestImportModifications()
        {
            EmptyDocument();

            RunUI(() => SkylineWindow.SaveDocument());

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            SrmDocument doc = SkylineWindow.Document;

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(SearchFiles);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            WaitForDocumentChange(doc);

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the "Match Modifications" page of the wizard.
            doc = SkylineWindow.Document;
            RunUI(() =>
            {
                // Define expected matched/unmatched modifications
                var expectedMatched = new List<string> { "Acetyl (T) = T[42]", "Carbamyl (K) = K[43]" };
                var expectedUnmatched = new List<string> { "R[114]" };
                // Verify matched/unmatched modifications
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                AssertEx.AreEqualDeep(expectedMatched, importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.ToList());
                AssertEx.AreEqualDeep(expectedUnmatched, importPeptideSearchDlg.MatchModificationsControl.UnmatchedModifications.ToList());
                // Add the unmatched modification R[114] as Double Carbamidomethylation
                StaticMod newMod = new StaticMod("Double Carbamidomethylation", "C,H,K,R", null, "H6C4N2O2");
                importPeptideSearchDlg.MatchModificationsControl.AddModification(newMod, MatchModificationsControl.ModType.heavy);
            });
            WaitForDocumentChange(doc);
            
            // Click Next
            doc = SkylineWindow.Document;
            RunUI(() =>
            {
                Assert.IsFalse(importPeptideSearchDlg.MatchModificationsControl.UnmatchedModifications.Any());
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            var docModified = WaitForDocumentChange(doc);

            // Cancel out of wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.ms1_full_scan_settings_page);
                importPeptideSearchDlg.ClickCancelButton();
            });

            WaitForClosedForm(importPeptideSearchDlg);

            // Check for modifications.
            var expectedStaticMods = new List<string> { "Carbamidomethyl (C)", "Acetyl (T)", "Carbamyl (K)" };
            var expectedHeavyMods = new List<string> { "Double Carbamidomethylation" };
            AssertEx.AreEqualDeep(expectedStaticMods, ModNames(Settings.Default.StaticModList));
            AssertEx.AreEqualDeep(expectedStaticMods, ModNames(docModified.Settings.PeptideSettings.Modifications.StaticModifications));
            AssertEx.AreEqualDeep(expectedHeavyMods, ModNames(Settings.Default.HeavyModList));
            AssertEx.AreEqualDeep(expectedHeavyMods, ModNames(docModified.Settings.PeptideSettings.Modifications.HeavyModifications));

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private IList<string> ModNames(IEnumerable<StaticMod> staticMods)
        {
            return staticMods.Select(m => m.Name).ToList();
        }

        /// <summary>
        /// Test that the "Match Modifications" page of the Import Peptide Search wizard gets skipped.
        /// </summary>
        private void TestSkipWhenNoModifications()
        {
            EmptyDocument();

            RunUI(() => SkylineWindow.SaveDocument());

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            SrmDocument doc = SkylineWindow.Document;

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(SearchFilesModless);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            WaitForDocumentChange(doc);

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We should have skipped past the "Match Modifications" page of the wizard.
            // Cancel out of wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.ms1_full_scan_settings_page);
                importPeptideSearchDlg.ClickCancelButton();
            });

            WaitForClosedForm(importPeptideSearchDlg);

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void EmptyDocument()
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                            doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(DocumentFile));
        }
    }
}
