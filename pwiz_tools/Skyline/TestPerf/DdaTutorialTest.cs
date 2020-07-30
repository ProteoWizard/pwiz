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

namespace TestPerf
{
    [TestClass]
    public class DdaTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        [Timeout(2 * 60 * 60 * 1000)]  // These can take a long time in code coverage mode (2 hours)
        public void TestDdaTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;
            //IsPauseForCoverShot = true;
            RunPerfTests = true;

            TestFilesZipPaths = new[]
            {
                @"https://skyline.gs.washington.edu/tutorials/DdaTutorialTest.zip",
                @"TestPerf/DdaTutorialViews.zip"
            };

            TestFilesPersistent = new[]
            {
                "QE_140221_01_UPS1_100fmolspiked.mz5",
                "QE_140221_02_UPS1_300fmolspiked.mz5",
                "QE_140221_03_UPS1_600fmolspiked.mz5"
            };
            RunFunctionalTest();
        }
        private const string HEAVY_R = "Label:13C(6)15N(4) (C-term R)";
        private const string HEAVY_K = "Label:13C(6)15N(2) (C-term K)";
        private const string OXIDATION_M = "Oxidation (M)";

        private string GetTestPath(string path)
        {
            return TestFilesDirs[0].GetTestPath(path);
        }

        private IEnumerable<string> SearchFiles
        {
            get { return TestFilesPersistent.Select(GetTestPath).Take(IsPauseForScreenShots ? 3 : 1); }
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

            int tutorialPage = 2;

            // Set standard type to None
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            RunUI(() => peptideSettingsUI.SelectedInternalStandardTypeName = Resources.LabelTypeComboDriver_LoadList_none);
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modifications tab", tutorialPage++);

            var docBeforePeptideSettings = SkylineWindow.Document;
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForDocumentChangeLoaded(docBeforePeptideSettings);

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Select DDA Files to Search page", tutorialPage++);

            // We're on the "Build Spectral Library" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch = true;
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => new MsDataFilePath(o)).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = false;
                Assert.AreEqual(ImportPeptideSearchDlg.Workflow.dda, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - After Selecting DDA Files page", tutorialPage++);

            if (IsPauseForScreenShots)
            {
                // Remove prefix/suffix dialog pops up; accept default behavior
                var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
                PauseForScreenShot<ImportResultsNameDlg>("Import Results - Common prefix form", tutorialPage++);
                OkDialog(removeSuffix, () => removeSuffix.YesDialog());
                WaitForDocumentLoaded();
            }
            else
                RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Match Modifications" page. Add SILAC mods and M+16
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
            PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Import Peptide Search - Add Modifications page", tutorialPage++);

            var editHeavyModListUI =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(importPeptideSearchDlg.MatchModificationsControl.ClickAddHeavyModification);
            RunDlg<EditStaticModDlg>(editHeavyModListUI.AddItem, editModDlg =>
            {
                editModDlg.SetModification(HEAVY_K, true); // Not L10N
                editModDlg.OkDialog();
            });
            RunDlg<EditStaticModDlg>(editHeavyModListUI.AddItem, editModDlg =>
            {
                editModDlg.SetModification(HEAVY_R, true); // Not L10N
                editModDlg.OkDialog();
            });
            OkDialog(editHeavyModListUI, editHeavyModListUI.OkDialog);

            /*var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var modHeavyK = new StaticMod(HEAVY_K, "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, RelativeRT.Matching, null, null, null);
            var modHeavyR = new StaticMod(HEAVY_R, "R", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUI, "Edit Isotope Modification form", tutorialPage++);
            AddHeavyMod(modHeavyR, peptideSettingsUI, "Edit Isotope Modification form", tutorialPage++);*/

            var editStructModListUI =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(importPeptideSearchDlg.MatchModificationsControl.ClickAddStructuralModification);
            RunDlg<EditStaticModDlg>(editStructModListUI.AddItem, editModDlg =>
            {
                editModDlg.SetModification(OXIDATION_M, true); // Not L10N
                editModDlg.OkDialog();
            });
            OkDialog(editStructModListUI, editStructModListUI.OkDialog);

            RunUI(() => importPeptideSearchDlg.MatchModificationsControl.ChangeAll(true));

            PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Import Peptide Search - After adding modifications page", tutorialPage++);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the MS1 full scan settings page. Set tolerance to 20ppm
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 20);
            PauseForScreenShot<ImportPeptideSearchDlg.Ms1FullScanPage>("Import Peptide Search - Configure MS1 Full-Scan Settings page", tutorialPage++);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Import FASTA" page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("2014_01_HUMAN_UPS.fasta"));
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page", tutorialPage++);
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
            PauseForScreenShot<ImportPeptideSearchDlg.DDASearchSettingsPage>("Import Peptide Search - DDA Search Settings page", tutorialPage++);

            // Run the search
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_page);
            PauseForScreenShot<ImportPeptideSearchDlg.DDASearchPage>("Import Peptide Search - DDA Search Progress page", tutorialPage++);

            // Wait for search to finish
            WaitForConditionUI(60000 * 60, () => searchSucceeded.HasValue);
            Assert.IsTrue(searchSucceeded.Value);

            // clicking 'Finish' (Next) will run ImportFasta
            var ambiguousDlg = ShowDialog<MessageDlg>(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            PauseForScreenShot<MessageDlg>("Import Peptide Search - Ambiguous Peptides dialog", tutorialPage++);
            RunUI(() => AssertEx.Contains(ambiguousDlg.Message,
                Resources.BiblioSpecLiteBuilder_AmbiguousMatches_The_library_built_successfully__Spectra_matching_the_following_peptides_had_multiple_ambiguous_peptide_matches_and_were_excluded_));

            var emptyProteinsDlg = ShowDialog<PeptidesPerProteinDlg>(() => ambiguousDlg.OkDialog());
            RunUI(() =>
            {
                emptyProteinsDlg.RemoveRepeatedPeptides = true;
                int proteinCount, peptideCount, precursorCount, transitionCount;
                emptyProteinsDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                if (!IsPauseForScreenShots)
                {
                    Assert.AreEqual(12343, proteinCount);
                    Assert.AreEqual(27859, peptideCount);
                    Assert.AreEqual(55327, precursorCount);
                    Assert.AreEqual(165981, transitionCount);
                }

                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                if (!IsPauseForScreenShots)
                {
                    Assert.AreEqual(3685, proteinCount);
                    Assert.AreEqual(7053, peptideCount);
                    Assert.AreEqual(13963, precursorCount);
                    Assert.AreEqual(41889, transitionCount);
                }
            });
            PauseForScreenShot("Import Peptide Search - Empty Proteins dialog", tutorialPage++);

            OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);
            WaitForDocumentLoaded();

            PauseForScreenShot("Main window with peptide search results", tutorialPage++);

            if (IsPauseForCoverShot)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 14;
                    Settings.Default.AreaFontSize = 14;
                    SkylineWindow.AutoZoomBestPeak();
                    SkylineWindow.ShowPeakAreaLegend(false);
                    SkylineWindow.ShowChromatogramLegends(false);
                });
                RestoreCoverViewOnScreen();
                
                RunUI(() => SkylineWindow.SequenceTree.SelectPath(SkylineWindow.Document.MoleculeGroups.Skip(2).First().GetPathTo(2)));
                WaitForGraphs();
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Nodes[0]);
                RunUI(SkylineWindow.FocusDocument);
                PauseForCoverShot();
                return;
            }

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
