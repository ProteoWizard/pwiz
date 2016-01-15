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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportPeptideSearchTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportPeptideSearch()
        {
            TestFilesZip = @"TestFunctional\ImportPeptideSearchTest.zip";
            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
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
            TestWizardBuildDocumentLibraryAndFinish();
            TestWizardCancel();
            TestWizardExcludeSpectrumSourceFiles();
        }

        /// <summary>
        /// Test that the "Match Modifications" page of the Import Peptide Search Wizard detects the correct matched/unmatched modifications and adds them.
        /// </summary>
        private void TestImportModifications()
        {
            PrepareDocument("ImportPeptideSearchTest.sky");

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
                var expectedMatched = new List<string> { "Acetyl (T)", "Carbamyl (K)", "GIST-Quat:2H(9) (N-term)" };
                var expectedUnmatched = new List<string> { "R[+114]" };
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
                importPeptideSearchDlg.MatchModificationsControl.ChangeAll(true);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            var docModified = WaitForDocumentChange(doc);

            // Cancel out of wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.ClickCancelButton();
            });

            WaitForClosedForm(importPeptideSearchDlg);

            // Check for modifications.
            var expectedStaticMods = new List<string> { "Carbamidomethyl (C)", "Acetyl (T)", "Carbamyl (K)" };
            var expectedHeavyMods = new List<string> { "Double Carbamidomethylation", "GIST-Quat:2H(9) (N-term)" };
            AssertEx.AreEqualDeep(expectedStaticMods, ModNames(docModified.Settings.PeptideSettings.Modifications.StaticModifications));
            AssertEx.AreEqualDeep(expectedHeavyMods, ModNames(docModified.Settings.PeptideSettings.Modifications.HeavyModifications));

            // Make sure that the proper modifications are variable.
            foreach (var mod in docModified.Settings.PeptideSettings.Modifications.StaticModifications.Union(docModified.Settings.PeptideSettings.Modifications.HeavyModifications))
            {
                switch (mod.Name)
                {
                    case "Carbamidomethyl (C)":
                    case "Double Carbamidomethylation":
                    case "GIST-Quat:2H(9) (N-term)":
                        Assert.IsFalse(mod.IsVariable);
                        break;
                    case "Acetyl (T)":
                    case "Carbamyl (K)":
                        Assert.IsTrue(mod.IsVariable);
                        break;
                    default:
                        Assert.Fail("Unexpected modification '{0}'", mod.Name);
                        break;
                }
            }

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
            PrepareDocument("ImportPeptideSearchTest2.sky");

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
            doc = WaitForDocumentChange(doc);

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We should have skipped past the "Match Modifications" page of the wizard onto the MS1 full scan settings page.
            // Click next
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.ClickNextButton();
            });
            WaitForDocumentChange(doc);

            // We're on the "Import FASTA" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("yeast-10.fasta"));
            });

            // Finish wizard and have empty proteins dialog come up. Only 1 out of the 10 proteins had a match.
            // Cancel the empty proteins dialog.
            RunDlg<EmptyProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck, emptyProteinsDlg =>
            {
                Assert.AreEqual(9, emptyProteinsDlg.EmptyProteins);
                emptyProteinsDlg.CancelDialog();
            });

            // Set empty protein discard notice to appear if there are > 5, and retry finishing the wizard.
            using (new EmptyProteinGroupSetter(5))
            {
                RunUI(() => importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("yeast-9.fasta")));
                RunDlg<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck, discardNotice =>
                {
                    Assert.AreEqual(
                        string.Format(Resources.SkylineWindow_ImportFasta_This_operation_discarded__0__proteins_with_no_peptides_matching_the_current_filter_settings_, 9),
                        discardNotice.Message);
                    discardNotice.OkDialog();
                });

                // An error will appear because the spectrum file was empty.
                var errorDlg = WaitForOpenForm<MessageDlg>();
                RunUI(() =>
                    {
                        Assert.AreEqual(Resources.ImportFastaControl_VerifyAtLeastOnePrecursorTransition_The_document_must_contain_at_least_one_precursor_transition_in_order_to_proceed_,
                            errorDlg.Message);
                        errorDlg.OkDialog();
                    });

                RunUI(importPeptideSearchDlg.CancelDialog);
                WaitForClosedForm(importPeptideSearchDlg);
            }

            RunUI(() => SkylineWindow.SaveDocument());
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

        private void TestWizardBuildDocumentLibraryAndFinish()
        {
            // Open the empty .sky file (has no peptides)
            const string documentFile = "ImportPeptideSearch-EarlyFinish.sky";
            PrepareDocument(documentFile);

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

            VerifyDocumentLibraryBuilt(documentFile);

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TestWizardCancel()
        {
            // Open the empty .sky file (has no peptides)
            PrepareDocument("ImportPeptideSearch-Cancel.sky");

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

        private void TestWizardExcludeSpectrumSourceFiles()
        {
            // Open the empty .sky file (has no peptides)
            PrepareDocument("ImportPeptideSearch-Exclude.sky");

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We should be on the "Build Spectral Library" page of the wizard.
            SrmDocument doc = SkylineWindow.Document;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new[] {GetTestPath("SpectrumSources.blib")});
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            WaitForDocumentChange(doc);

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                ImportResultsControl importResultsControl = importPeptideSearchDlg.ImportResultsControl as ImportResultsControl;
                Assert.IsNotNull(importResultsControl);
                
                // Exclude spectrum source files is unchecked, so the control should match
                // the two spectrum source files that match exactly: modless.mzXML and mods.mzXML
                List<ImportPeptideSearch.FoundResultsFile> foundResults = importResultsControl.FoundResultsFiles;
                string[] missingResults = importResultsControl.MissingResultsFiles.ToArray();
                Assert.AreEqual(2, foundResults.Count);
                Assert.AreEqual("modless", foundResults[0].Name);
                Assert.AreEqual("modless.mzXML", Path.GetFileName(foundResults[0].Path));
                Assert.AreEqual("mods", foundResults[1].Name);
                Assert.AreEqual("mods.mzXML", Path.GetFileName(foundResults[1].Path));
                Assert.IsFalse(importResultsControl.ResultsFilesMissing);
                Assert.AreEqual(0, missingResults.Count());

                // Check exclude spectrum source files
                Assert.IsTrue(importResultsControl.ExcluedSpectrumSourceFilesVisible);
                importResultsControl.ExcludeSpectrumSourceFiles = true;

                // The test files directory contains modless.raw, but no file with the same
                // filestem as mods.mzXML, so the control should match just modless.raw
                foundResults = importResultsControl.FoundResultsFiles;
                missingResults = importResultsControl.MissingResultsFiles.ToArray();
                Assert.AreEqual(1, foundResults.Count);
                Assert.AreEqual("modless", foundResults[0].Name);
                Assert.AreEqual("modless.raw", Path.GetFileName(foundResults[0].Path));
                Assert.IsTrue(importResultsControl.ResultsFilesMissing);
                Assert.AreEqual(1, missingResults.Count());
                Assert.AreEqual("mods.mzXML", Path.GetFileName(missingResults[0]));
                
                importPeptideSearchDlg.ClickCancelButton();
            });

            WaitForClosedForm(importPeptideSearchDlg);

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