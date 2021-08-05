﻿/*
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
using System.Drawing;
using System.IO;
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
            //IsCoverShotMode = true;
            //RunPerfTests = true;

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/DDASearch-20_2.pdf";

            TestFilesZipPaths = new[]
            {
                @"https://skyline.ms/tutorials/DdaSearchMs1Filtering.zip",
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
            get { return TestFilesPersistent.Select(f => GetTestPath(Path.Combine("DdaSearchMs1Filtering", f))).Take(IsFullData ? 3 : 1); }
        }

        protected override void DoTest()
        {
            TestAmandaSearch();

            Assert.IsFalse(IsRecordMode);   // Make sure this doesn't get committed as true
        }

        /// <summary>
        /// Change to true to write new Assert statements instead of testing them.
        /// </summary>
        private bool IsRecordMode { get { return false; } }

        private Image _searchLogImage;

        protected override void ProcessCoverShot(Bitmap bmp)
        {
            var graph = Graphics.FromImage(bmp);
            graph.DrawImageUnscaled(_searchLogImage, bmp.Width - _searchLogImage.Width - 10, bmp.Height - _searchLogImage.Height - 30);
        }

        /// <summary>
        /// Test that the "Match Modifications" page of the Import Peptide Search wizard gets skipped.
        /// </summary>
        private void TestAmandaSearch()
        {
            PrepareDocument("TestDdaTutorial.sky");

            int tutorialPage = 3;

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

            if (IsFullData)
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
            var heavyKDlg = ShowDialog<EditStaticModDlg>(editHeavyModListUI.AddItem);
            RunUI(() => heavyKDlg.SetModification(HEAVY_K, true));
            PauseForScreenShot<EditStaticModDlg.IsotopeModView>("Edit Isotope Modification form - K", tutorialPage++);
            OkDialog(heavyKDlg, heavyKDlg.OkDialog);

            var heavyRDlg = ShowDialog<EditStaticModDlg>(editHeavyModListUI.AddItem);
            RunUI(() => heavyRDlg.SetModification(HEAVY_R, true));
            PauseForScreenShot<EditStaticModDlg.IsotopeModView>("Edit Isotope Modification form - R", tutorialPage++);
            OkDialog(heavyRDlg, heavyRDlg.OkDialog);
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
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("DdaSearchMs1Filtering\\2014_01_HUMAN_UPS.fasta"));
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page", tutorialPage++);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Adjust Search Settings" page
            bool? searchSucceeded = null;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                importPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = new MzTolerance(5, MzTolerance.Units.ppm);
                importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = new MzTolerance(10, MzTolerance.Units.ppm);
                importPeptideSearchDlg.SearchSettingsControl.FragmentIons = "b, y";

                importPeptideSearchDlg.SearchControl.OnSearchFinished += (success) => searchSucceeded = success;
            });
            PauseForScreenShot<ImportPeptideSearchDlg.DDASearchSettingsPage>("Import Peptide Search - DDA Search Settings page", tutorialPage++);

            // Run the search
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_page);
            PauseForScreenShot<ImportPeptideSearchDlg.DDASearchPage>("Import Peptide Search - DDA Search Progress page", tutorialPage++);

            // Wait for search to finish
            WaitForConditionUI(60000 * 60, () => searchSucceeded.HasValue);
            RunUI(() => Assert.IsTrue(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText));
            if (IsCoverShotMode)
            {
                _searchLogImage = ScreenshotManager.TakeNextShot(importPeptideSearchDlg);
                Assert.IsNotNull(_searchLogImage);
            }

            // clicking 'Finish' (Next) will run ImportFasta
            var ambiguousDlg = ShowDialog<MessageDlg>(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()), 60000 * 10);
            PauseForScreenShot<MessageDlg>("Import Peptide Search - Ambiguous Peptides dialog", tutorialPage++);
            RunUI(() => AssertEx.Contains(ambiguousDlg.Message,
                Resources.BiblioSpecLiteBuilder_AmbiguousMatches_The_library_built_successfully__Spectra_matching_the_following_peptides_had_multiple_ambiguous_peptide_matches_and_were_excluded_));

            var emptyProteinsDlg = ShowDialog<PeptidesPerProteinDlg>(() => ambiguousDlg.OkDialog());
            RunUI(() =>
            {
                emptyProteinsDlg.RemoveRepeatedPeptides = true;
                int proteinCount, peptideCount, precursorCount, transitionCount;
                emptyProteinsDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                if (!IsFullData)
                {
                    if (IsRecordMode)
                    {
                        Console.WriteLine();
                        Console.WriteLine($@"Assert.AreEqual({proteinCount}, proteinCount);");
                        Console.WriteLine($@"Assert.AreEqual({peptideCount}, peptideCount);");
                        Console.WriteLine($@"Assert.AreEqual({precursorCount}, precursorCount);");
                        Console.WriteLine($@"Assert.AreEqual({transitionCount}, transitionCount);");
                    }
                    else
                    {
                        Assert.AreEqual(12590, proteinCount);
                        Assert.AreEqual(28251, peptideCount);
                        Assert.AreEqual(56111, precursorCount);
                        Assert.AreEqual(168333, transitionCount);
                    }
                }

                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                if (!IsFullData)
                {
                    if (IsRecordMode)
                    {
                        Console.WriteLine();
                        Console.WriteLine($@"Assert.AreEqual({proteinCount}, proteinCount);");
                        Console.WriteLine($@"Assert.AreEqual({peptideCount}, peptideCount);");
                        Console.WriteLine($@"Assert.AreEqual({precursorCount}, precursorCount);");
                        Console.WriteLine($@"Assert.AreEqual({transitionCount}, transitionCount);");
                    }
                    else
                    {
                        Assert.AreEqual(3772, proteinCount);
                        Assert.AreEqual(7173, peptideCount);
                        Assert.AreEqual(14203, precursorCount);
                        Assert.AreEqual(42609, transitionCount);
                    }
                }
            });
            PauseForScreenShot("Import Peptide Search - Empty Proteins dialog", tutorialPage++);

            using (new WaitDocumentChange(null, true))
            {
            OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);
            }

            FindNode(string.Format("{0:F04}++", IsFullData ? 835.914 : 699.3566));
            RunUI(() =>
            {
                SkylineWindow.GraphSpectrumSettings.ShowBIons = true;
                SkylineWindow.ShowAlignedPeptideIDTimes(true);
            });
            WaitForGraphs();
            RestoreViewOnScreen(17);
            RunUI(() =>
            {
                SkylineWindow.Size = new Size(1137, 714);
                // Set horizontal scroll position
            });
            WaitForGraphs();
            PauseForScreenShot("Main window with peptide search results", tutorialPage++);

            RunUI(() => SkylineWindow.ShowPeakAreaReplicateComparison());
            RefreshGraphs();
            PauseForScreenShot("Peak Areas - Replicate Comparison", tutorialPage++);

            RunUI(() =>
            {
                SkylineWindow.ShowChromatogramLegends(false);
                SkylineWindow.AutoZoomBestPeak();
            });
            RestoreViewOnScreen(19);
            RefreshGraphs();
            RefreshGraphs();    // For some reason the first time doesn't get the idotp values in the are graph right
            PauseForScreenShot("Main window arranged", tutorialPage);

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 14;
                    Settings.Default.AreaFontSize = 14;
                });
                RestoreCoverViewOnScreen(false);
                
                RunUI(SkylineWindow.FocusDocument);
                RefreshGraphs();
                TakeCoverShot();
                return;
            }

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void RefreshGraphs()
        {
            WaitForGraphs();
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Parent);
            WaitForGraphs();
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Nodes[0]);
            WaitForGraphs();
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
