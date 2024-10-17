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
using pwiz.Skyline.EditUI;
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
            TestUniqueness();
            TestImportModifications();
            TestSkipWhenNoModifications();
            TestWizardBuildDocumentLibraryAndFinish();
            TestWizardCancel();
            TestWizardExcludeSpectrumSourceFiles();
            TestWizardDecoysAndMinPeptides();
            TestIrts();
            TestMinIonCount();
        }

        /// <summary>
        /// A quick unit test for the name->path name uniqueness enforcement code.
        /// </summary>
        private void TestUniqueness()
        {
            const string PREFIX = "prefix1";
            var names = new[] { PREFIX + "a", PREFIX + "b", PREFIX };
            Assert.AreEqual(PREFIX, ImportResultsDlg.GetCommonPrefix(names)); // Check common prefix code while we're at it
            var list = new List<ImportPeptideSearch.FoundResultsFile>
            {
                new ImportPeptideSearch.FoundResultsFile(PREFIX + "a", "path1"),
                new ImportPeptideSearch.FoundResultsFile(PREFIX + "b", "path2"),
                new ImportPeptideSearch.FoundResultsFile(PREFIX + "a", "path3")
            };
            var result = ImportPeptideSearch.EnsureUniqueNames(list).ToArray();
            Assert.AreEqual(list[0].Name, result[0].Name);
            Assert.AreEqual(list[1].Name, result[1].Name);
            Assert.AreEqual(list[2].Name + "2", result[2].Name);
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
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
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
                var expectedMatched = new List<string> { "Acetyl (T)", "Carbamyl (K)", "Dicarbamidomethyl (R)", "GIST-Quat:2H(9) (N-term)" };
                var expectedUnmatched = new List<string>();
                
                // Verify matched/unmatched modifications
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                AssertEx.AreEqualDeep(expectedMatched, importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.ToList());
                AssertEx.AreEqualDeep(expectedUnmatched, importPeptideSearchDlg.MatchModificationsControl.UnmatchedModifications.ToList());

                // Add the unmatched modification R[114] as Double Carbamidomethylation
                StaticMod newMod = new StaticMod("Double Carbamidomethylation", "C,H,K,R", null, "H6C4N2O2");
                importPeptideSearchDlg.MatchModificationsControl.AddModification(newMod, MatchModificationsControl.ModType.heavy);
            });

            Assert.AreSame(doc, SkylineWindow.Document);    // Wizard should not change the document here

            // Click Next
            RunUI(() =>
            {
                Assert.IsFalse(importPeptideSearchDlg.MatchModificationsControl.UnmatchedModifications.Any());
                importPeptideSearchDlg.MatchModificationsControl.ChangeAll(true);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            Assert.AreSame(doc, SkylineWindow.Document);    // Wizard should not change the document here

            SrmDocument docModified = null;
            RunUI(() =>
            {
                docModified = importPeptideSearchDlg.Document;
                Assert.AreNotSame(doc, docModified);
            });
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
            AssertEx.AreEqualDeep(expectedHeavyMods, ModNames(docModified.Settings.PeptideSettings.Modifications.AllHeavyModifications));

            // Make sure that the proper modifications are variable.
            foreach (var mod in docModified.Settings.PeptideSettings.Modifications.StaticModifications.Union(docModified.Settings.PeptideSettings.Modifications.AllHeavyModifications))
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
                Assert.AreEqual(ImportPeptideSearchDlg.Workflow.dda, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType);
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
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

            // We're on the "Match Modifications" page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                importPeptideSearchDlg.ClickNextButton();
            });

            // We're on the MS1 full scan settings page.
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
                Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("yeast-10.fasta"));
            });

            // Finish wizard and have empty proteins dialog come up. Only 1 out of the 10 proteins had a match.
            // Cancel the empty proteins dialog.
            var emptyProteinsDlg = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => emptyProteinsDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                /*emptyProteinsDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(10, proteinCount);
                Assert.AreEqual(1, peptideCount);
                Assert.AreEqual(1, precursorCount);
                Assert.AreEqual(3, transitionCount);*/
                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(1, proteinCount);
                Assert.AreEqual(1, peptideCount);
                Assert.AreEqual(1, precursorCount);
                Assert.AreEqual(3, transitionCount);
                emptyProteinsDlg.CancelDialog();
            });

            // Set empty protein discard notice to appear if there are > 5, and retry finishing the wizard.
            using (new EmptyProteinGroupSetter(5))
            {
                RunUI(() => importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("yeast-9.fasta")));
                RunDlg<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck, discardNotice =>
                {
                    Assert.AreEqual(Resources.ImportFastaControl_ImportFasta_Importing_the_FASTA_did_not_create_any_target_proteins_, discardNotice.Message);
                    discardNotice.OkDialog();
                });
            }

            OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.CancelDialog);

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
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(SearchFiles);
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsEarlyFinishButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickEarlyFinishButton()));
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
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            WaitForDocumentChange(doc);

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                ImportResultsControl importResultsControl = importPeptideSearchDlg.ImportResultsControl as ImportResultsControl;
                Assert.IsNotNull(importResultsControl);
                
                // Exclude spectrum source files is unchecked, so the control should match
                // the two spectrum source files that match exactly: modless.mzXML and mods.mzXML
                var foundResults = importResultsControl.FoundResultsFiles;
                string[] missingResults = importResultsControl.MissingResultsFiles.ToArray();
                Assert.AreEqual(2, foundResults.Count);
                Assert.AreEqual("modless", foundResults[0].Name);
                Assert.AreEqual("modless.mzXML", Path.GetFileName(foundResults[0].Path));
                Assert.AreEqual("mods", foundResults[1].Name);
                Assert.AreEqual("mods.mzXML", Path.GetFileName(foundResults[1].Path));
                Assert.IsFalse(importResultsControl.ResultsFilesMissing);
                Assert.AreEqual(0, missingResults.Length);

                // Check exclude spectrum source files
                Assert.IsTrue(importResultsControl.ExcludeSpectrumSourceFilesVisible);
                importResultsControl.ExcludeSpectrumSourceFiles = true;

                // The test files directory contains modless.raw, but no file with the same
                // filestem as mods.mzXML, so the control should match just modless.raw
                foundResults = importResultsControl.FoundResultsFiles;
                missingResults = importResultsControl.MissingResultsFiles.ToArray();
                Assert.AreEqual(1, foundResults.Count);
                Assert.AreEqual("modless", foundResults[0].Name);
                Assert.AreEqual("modless.raw", Path.GetFileName(foundResults[0].Path));
                Assert.IsTrue(importResultsControl.ResultsFilesMissing);
                Assert.AreEqual(1, missingResults.Length);
                Assert.AreEqual("mods.mzXML", Path.GetFileName(missingResults[0]));
                
                importPeptideSearchDlg.ClickCancelButton();
            });

            WaitForClosedForm(importPeptideSearchDlg);

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TestWizardDecoysAndMinPeptides()
        {
            PrepareDocument("ImportPeptideSearch-Decoys.sky");
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            var doc = SkylineWindow.Document;

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                Assert.IsFalse(importPeptideSearchDlg.IsBackButtonVisible);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(SearchFilesModless);
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            doc = WaitForDocumentChange(doc);

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                Assert.IsFalse(importPeptideSearchDlg.IsBackButtonVisible);
                importPeptideSearchDlg.ImportResultsControl.FoundResultsFiles = new List<ImportPeptideSearch.FoundResultsFile>
                {
                    new ImportPeptideSearch.FoundResultsFile(MODLESS_BASE_NAME, SearchFilesModless.First())
                };
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                importPeptideSearchDlg.TransitionSettingsControl.IonCount = 3;  // DIA will now default to 6 and 6 minimum
                importPeptideSearchDlg.TransitionSettingsControl.MinIonCount = 0;
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            Assert.AreSame(doc, SkylineWindow.Document);    // Wizard should not change the document here

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.IsBackButtonVisible);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            Assert.AreSame(doc, SkylineWindow.Document);    // Wizard should not change the document here

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.IsTrue(importPeptideSearchDlg.IsBackButtonVisible);
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                Assert.AreEqual(Settings.Default.GetEnzymeByName("Trypsin"), importPeptideSearchDlg.ImportFastaControl.Enzyme);
                Assert.AreEqual(0, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("yeast-10.fasta"));
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod = DecoyGeneration.REVERSE_SEQUENCE;
                importPeptideSearchDlg.ImportFastaControl.NumDecoys = 1.1;
            });
            var errMaxDecoys = ShowDialog<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            Assert.AreEqual(Resources.ImportFastaControl_ImportFasta_A_maximum_of_one_decoy_per_target_may_be_generated_when_using_reversed_decoys_, errMaxDecoys.Message);
            OkDialog(errMaxDecoys, errMaxDecoys.OkDialog);
            RunUI(() => { importPeptideSearchDlg.ImportFastaControl.NumDecoys = -1; });
            var errValidNumDecoys = ShowDialog<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            Assert.AreEqual(Resources.ImportFastaControl_ImportFasta_Please_enter_a_valid_number_of_decoys_per_target_greater_than_0_, errValidNumDecoys.Message);
            OkDialog(errValidNumDecoys, errValidNumDecoys.OkDialog);
            RunUI(() =>
            {
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod = DecoyGeneration.SHUFFLE_SEQUENCE;
                importPeptideSearchDlg.ImportFastaControl.NumDecoys = 1;
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("yeast-10-duplicate.fasta"));
            });
            var peptidesPerProteinDlg2 = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => peptidesPerProteinDlg2.DocumentFinalCalculated);
            int proteins, peptides, precursors, transitions, unmappedOrRemoved;
            RunUI(() =>
            {
                //Assert.IsTrue(peptidesPerProteinDlg2.DuplicateControlsVisible);
                peptidesPerProteinDlg2.MinPeptides = 1;
                peptidesPerProteinDlg2.RemoveRepeatedPeptides = peptidesPerProteinDlg2.RemoveDuplicatePeptides = false;
                /*peptidesPerProteinDlg2.NewTargetsAll(out proteins, out peptides, out precursors, out transitions);
                Assert.AreEqual(12, proteins);
                Assert.AreEqual(4, peptides);
                Assert.AreEqual(4, precursors);
                Assert.AreEqual(12, transitions);*/
            });
            WaitForConditionUI(() => peptidesPerProteinDlg2.DocumentFinalCalculated);
            RunUI(() =>
            {
                peptidesPerProteinDlg2.NewTargetsFinalSync(out proteins, out peptides, out precursors, out transitions, out unmappedOrRemoved);
                Assert.AreEqual(3, proteins);
                Assert.AreEqual(4, peptides);
                Assert.AreEqual(4, precursors);
                Assert.AreEqual(12, transitions);
                Assert.AreEqual(1, unmappedOrRemoved);
                peptidesPerProteinDlg2.RemoveRepeatedPeptides = true;
            });
            WaitForConditionUI(() => peptidesPerProteinDlg2.DocumentFinalCalculated);
            RunUI(() =>
            {
                peptidesPerProteinDlg2.NewTargetsFinalSync(out proteins, out peptides, out precursors, out transitions, out unmappedOrRemoved);
                Assert.AreEqual(2, proteins);
                Assert.AreEqual(2, peptides);
                Assert.AreEqual(2, precursors);
                Assert.AreEqual(6, transitions);
                Assert.AreEqual(1, unmappedOrRemoved);
                peptidesPerProteinDlg2.RemoveRepeatedPeptides = false;
                peptidesPerProteinDlg2.RemoveDuplicatePeptides = true;
            });
            WaitForConditionUI(() => peptidesPerProteinDlg2.DocumentFinalCalculated);
            RunUI(() =>
            {
                peptidesPerProteinDlg2.NewTargetsFinalSync(out proteins, out peptides, out precursors, out transitions, out unmappedOrRemoved);
                Assert.AreEqual(0, proteins);
                Assert.AreEqual(0, peptides);
                Assert.AreEqual(0, precursors);
                Assert.AreEqual(0, transitions);
                Assert.AreEqual(1, unmappedOrRemoved);
            });
            OkDialog(peptidesPerProteinDlg2, peptidesPerProteinDlg2.CancelDialog);
            RunUI(() => importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("yeast-10.fasta")));

            var peptidesPerProteinDlg = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            RunUI(() =>
            {
                peptidesPerProteinDlg.MinPeptides = 1;
                peptidesPerProteinDlg.NewTargetsFinalSync(out proteins, out peptides, out precursors, out transitions, out unmappedOrRemoved);
                Assert.AreEqual(2, proteins);
                Assert.AreEqual(2, peptides);
                Assert.AreEqual(2, precursors);
                Assert.AreEqual(6, transitions);
                Assert.AreEqual(1, unmappedOrRemoved);
                peptidesPerProteinDlg.MinPeptides = 2;
            });
            WaitForConditionUI(() => peptidesPerProteinDlg2.DocumentFinalCalculated);
            RunUI(() =>
            {
                peptidesPerProteinDlg.NewTargetsFinalSync(out proteins, out peptides, out precursors, out transitions, out unmappedOrRemoved);
                Assert.AreEqual(0, proteins);
                Assert.AreEqual(0, peptides);
                Assert.AreEqual(0, precursors);
                Assert.AreEqual(0, transitions);
                Assert.AreEqual(1, unmappedOrRemoved);
                peptidesPerProteinDlg.MinPeptides = 1;
            });
            WaitForConditionUI(() => peptidesPerProteinDlg2.DocumentFinalCalculated);
            RunUI(() =>
            {
                peptidesPerProteinDlg.NewTargetsFinalSync(out proteins, out peptides, out precursors, out transitions, out unmappedOrRemoved);
                Assert.AreEqual(2, proteins);
                Assert.AreEqual(2, peptides);
                Assert.AreEqual(2, precursors);
                Assert.AreEqual(6, transitions);
                Assert.AreEqual(1, unmappedOrRemoved);
            });
            // The AllChromatogramsGraph will immediately show an error because the file being imported is bogus.
            var importResultsDlg = ShowDialog<AllChromatogramsGraph>(peptidesPerProteinDlg.OkDialog);
            doc = WaitForDocumentChangeLoaded(doc);
            WaitForConditionUI(5000, () => importResultsDlg.Finished && importResultsDlg.Files.Any(f => !string.IsNullOrEmpty(f.Error)));
            OkDialog(importResultsDlg, importResultsDlg.ClickClose);
            AssertEx.IsDocumentState(doc, null, 2, 2, 6);

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TestIrts()
        {
            PrepareDocument("ImportPeptideSearch-irts.sky");
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            var doc = SkylineWindow.Document;

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new[] {GetTestPath("biognosystiny.blib")});
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.BIOGNOSYS_11;
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            var addIrtDlg = ShowDialog<AddIrtPeptidesDlg>(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            var recalibrateDlg = ShowDialog<MultiButtonMsgDlg>(addIrtDlg.OkDialog);
            OkDialog(recalibrateDlg, recalibrateDlg.ClickNo);

            doc = WaitForDocumentChange(doc);

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                importPeptideSearchDlg.ImportResultsControl.FoundResultsFiles = new List<ImportPeptideSearch.FoundResultsFile>
                {
                    new ImportPeptideSearch.FoundResultsFile(MODLESS_BASE_NAME, SearchFilesModless.First())
                };
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                var oldTolerance = importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance;
                importPeptideSearchDlg.TransitionSettingsControl.IonMatchToleranceUnits = MzTolerance.Units.ppm;
                Assert.IsTrue(
                    Math.Abs(importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance / oldTolerance - 1000.0d) < 0.001d);
                importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance = 10;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("yeast-10.fasta"));
            });
            var peptidesPerProteinDlg = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            //RunUI(() => peptidesPerProteinDlg.KeepAll = true);
            WaitForConditionUI(() => peptidesPerProteinDlg.IsOkEnabled);
            // The AllChromatogramsGraph will immediately show an error because the file being imported is bogus.
            var importResultsDlg = ShowDialog<AllChromatogramsGraph>(peptidesPerProteinDlg.OkDialog);
            doc = WaitForDocumentChangeLoaded(doc);
            WaitForConditionUI(5000, () => importResultsDlg.Finished && importResultsDlg.Files.Any(f => !string.IsNullOrEmpty(f.Error)));
            OkDialog(importResultsDlg, importResultsDlg.ClickClose);

            // The document should have the 11 Biognosys standard peptides in the first peptide group
            var irt = doc.PeptideGroups.First();
            Assert.AreEqual(11, irt.PeptideCount);
            var irtMap = new TargetMap<bool>(IrtStandard.BIOGNOSYS_11.Peptides.Select(pep => new KeyValuePair<Target, bool>(pep.ModifiedTarget, true)));
            foreach (var nodePep in irt.Peptides)
                Assert.IsTrue(irtMap.ContainsKey(nodePep.ModifiedTarget));

            Assert.AreEqual(new MzTolerance(10, MzTolerance.Units.ppm), doc.Settings.TransitionSettings.Libraries.IonMatchMzTolerance); 

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TestMinIonCount()
        {
            const string protein = ">sp|P62258|1433E_HUMAN 14-3-3 protein epsilon OS=Homo sapiens GN=YWHAE PE=1 SV=1\r\n" +
                                   "MDDREDLVYQAKLAEQAERYDEMVESMKKVAGMDVELTVEERNLLSVAYKNVIGARRASW\r\n" +
                                   "RIISSIEQKEENKGGEDKLKMIREYRQMVETELKLICCDILDVLDKHLIPAANTGESKVF\r\n" +
                                   "YYKMKGDYHRYLAEFATGNDRKEAAENSLVAYKAASDIAMTELPPTHPIRLGLALNFSVF\r\n" +
                                   "YYEILNSPDRACRLAKAAFDDAIAELDTLSEESYKDSTLIMQLLRDNLTLWTSDMQGDGE\r\n" +
                                   "EQNKEALQDVEDENQ*";
            SrmDocument doc = null;
            RunUI(() =>
            {
                doc = SkylineWindow.DocumentUI;
                Assert.IsTrue(SkylineWindow.LoadFile(GetTestPath("MinIonCount.sky")));
                doc = WaitForDocumentChangeLoaded(doc);
                SkylineWindow.Paste(protein);
            });
            doc = WaitForDocumentChange(doc);
            var minIonCount = 6;
            Assert.AreEqual(minIonCount, doc.Settings.TransitionSettings.Libraries.MinIonCount);
            Assert.AreEqual(11, doc.PeptideCount);
            foreach (var nodePepGroup in doc.MoleculeGroups)
            {
                foreach (var nodePep in nodePepGroup.Peptides)
                {
                    Assert.AreNotEqual(0, nodePep.TransitionGroupCount);
                    foreach (var nodeTranGroup in nodePep.TransitionGroups)
                    {
                        Assert.IsTrue(nodeTranGroup.TransitionCount >= minIonCount);
                    }
                }
            }

            minIonCount = 5;
            RunUI(() => Assert.IsTrue(SkylineWindow.SetDocument(
                doc.ChangeSettings(doc.Settings.ChangeTransitionLibraries(libraries => libraries.ChangeMinIonCount(minIonCount))), doc)));
            doc = WaitForDocumentChange(doc);
            Assert.AreEqual(minIonCount, doc.Settings.TransitionSettings.Libraries.MinIonCount);
            Assert.AreEqual(12, doc.PeptideCount);
            foreach (var nodePepGroup in doc.MoleculeGroups)
            {
                foreach (var nodePep in nodePepGroup.Peptides)
                {
                    Assert.AreNotEqual(0, nodePep.TransitionGroupCount);
                    foreach (var nodeTranGroup in nodePep.TransitionGroups)
                    {
                        Assert.IsTrue(nodeTranGroup.TransitionCount >= minIonCount);
                    }
                }
            }

            // Check scores
            var precursorScores = new Dictionary<string, List<Tuple<double, double>>>
            {
                {"LAEQAER", new List<Tuple<double, double>> {new Tuple<double, double>(408.7141, 0.999999)}},
                {"YDEMVESMK", new List<Tuple<double, double>> {new Tuple<double, double>(566.2385, 0.999998)}},
                {"VAGMDVELTVEER", new List<Tuple<double, double>> {new Tuple<double, double>(724.3585, 0.999998)}},
                {"NLLSVAYK", new List<Tuple<double, double>> {new Tuple<double, double>(454.2660, 0.999999)}},
                {"IISSIEQK", new List<Tuple<double, double>> {new Tuple<double, double>(459.2687, 0.997624)}},
                {"QMVETELK", new List<Tuple<double, double>> {new Tuple<double, double>(489.2522, 0.999995)}},
                {"HLIPAANTGESK", new List<Tuple<double, double>> {new Tuple<double, double>(413.2227, 0.999994)}},
                {"YLAEFATGNDR", new List<Tuple<double, double>> {new Tuple<double, double>(628.7989, 0.999999)}},
                {"EAAENSLVAYK", new List<Tuple<double, double>> {new Tuple<double, double>(597.8037, 0.999999)}},
                {"AASDIAMTELPPTHPIR", new List<Tuple<double, double>> {new Tuple<double, double>(607.3172, 0.999999)}},
                {"AAFDDAIAELDTLSEESYK", new List<Tuple<double, double>> {new Tuple<double, double>(696.6600, 0.999313)}},
                {"DSTLIMQLLR", new List<Tuple<double, double>> {new Tuple<double, double>(595.3341, 0.999999)}},
            };
            foreach (var nodeTranGroup in doc.MoleculeTransitionGroups)
            {
                Assert.IsTrue(nodeTranGroup.HasLibInfo);
                var bibliospecInfo = nodeTranGroup.LibInfo as BiblioSpecSpectrumHeaderInfo;
                Assert.IsNotNull(bibliospecInfo);
                if (!precursorScores.TryGetValue(nodeTranGroup.Peptide.Sequence, out var precursorList))
                {
                    Assert.Fail("peptide {0} not in score dictionary", nodeTranGroup.Peptide.Sequence);
                }
                var knownPrecursor = precursorList.FirstOrDefault(tuple => Math.Abs(tuple.Item1 - nodeTranGroup.PrecursorMz.Value) < 0.001);
                if (knownPrecursor == null)
                {
                    Assert.Fail("precursor {0} not in score dictionary", nodeTranGroup.PrecursorMz);
                }
                Assert.AreEqual(knownPrecursor.Item2, bibliospecInfo.Score.GetValueOrDefault(), 0.000001);
            }

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