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
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class DdaTutorialTest : AbstractFunctionalTest
    {
        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE), NoUnicodeTesting(TestExclusionReason.MSFRAGGER_UNICODE_ISSUES)]
        public void TestDdaTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;
            //IsCoverShotMode = true;
            //RunPerfTests = true;
            CoverShotName = "DDASearch";

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/DDASearch-22_2.pdf";

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
            TestMsFraggerSearch();

            Assert.IsFalse(IsRecordMode);   // Make sure this doesn't get committed as true
        }

        /// <summary>
        /// Change to true to write new Assert statements instead of testing them.
        /// </summary>
        private bool IsRecordMode => false;

        private bool RedownloadTools => !IsRecordMode && !IsRecordAuditLogForTutorials && IsPass0;
        private bool HasMissingDependencies => !SearchSettingsControl.HasRequiredFilesDownloaded(SearchSettingsControl.SearchEngine.MSFragger);

        private Image _searchLogImage;

        protected override void ProcessCoverShot(Bitmap bmp)
        {
            var graph = Graphics.FromImage(bmp);
            graph.DrawImageUnscaled(_searchLogImage, bmp.Width - _searchLogImage.Width - 10, bmp.Height - _searchLogImage.Height - 30);
        }

        /// <summary>
        /// Test that the "Match Modifications" page of the Import Peptide Search wizard gets skipped.
        /// </summary>
        private void TestMsFraggerSearch()
        {
            PrepareDocument("TestDdaTutorial.sky");

            // delete downloaded tools if not recording new counts or audit logs
            if (RedownloadTools)
                foreach (var requiredFile in MsFraggerSearchEngine.FilesToDownload)
                    if (requiredFile.Unzip)
                        DirectoryEx.SafeDelete(requiredFile.InstallPath);
                    else
                        FileEx.SafeDelete(Path.Combine(requiredFile.InstallPath, requiredFile.Filename));

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
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Select DDA Files to Search page", tutorialPage++);

            // We're on the "Build Spectral Library" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => new MsDataFilePath(o)).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = false;
                Assert.AreEqual(ImportPeptideSearchDlg.Workflow.dda, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - After Selecting DDA Files page", tutorialPage++);

            if (SearchFiles.Count() > 1)
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
            RunUI(() => heavyKDlg.SetModification(HEAVY_K));
            PauseForScreenShot<EditStaticModDlg.IsotopeModView>("Edit Isotope Modification form - K", tutorialPage++);
            OkDialog(heavyKDlg, heavyKDlg.OkDialog);

            var heavyRDlg = ShowDialog<EditStaticModDlg>(editHeavyModListUI.AddItem);
            RunUI(() => heavyRDlg.SetModification(HEAVY_R));
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
                editModDlg.SetModification(OXIDATION_M); // Not L10N
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
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = 0;
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("DdaSearchMs1Filtering\\2014_01_HUMAN_UPS.fasta"));
                //importPeptideSearchDlg.ImportFastaControl.SetFastaContent(@"D:\test\Skyline\downloads\Tutorials\DdaSearchMs1Filtering\DdaSearchMS1Filtering\2021-11-09-decoys-2014_01_HUMAN_UPS.fasta");
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page", tutorialPage++);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Adjust Search Settings" page
            bool? searchSucceeded = null;
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page));

            // switch SearchEngine and handle download dialogs if necessary
            bool useMsFragger = !IsPauseForScreenShots; // Tutorial still uses MSAmanda
            if (useMsFragger)
            {
                SkylineWindow.BeginInvoke(new Action(() => importPeptideSearchDlg.SearchSettingsControl.SelectedSearchEngine = SearchSettingsControl.SearchEngine.MSFragger));
                if (RedownloadTools || HasMissingDependencies)
                {
                    var msfraggerDownloaderDlg = TryWaitForOpenForm<MsFraggerDownloadDlg>(2000);
                    if (msfraggerDownloaderDlg != null)
                    {
                        PauseForScreenShot<MsFraggerDownloadDlg>("Import Peptide Search - Download MSFragger", tutorialPage++); // Maybe someday
                        RunUI(() => msfraggerDownloaderDlg.SetValues("Matt Chambers (testing download from Skyline)", "matt.chambers42@gmail.com", "UW"));
                        OkDialog(msfraggerDownloaderDlg, msfraggerDownloaderDlg.ClickAccept);
                    }

                    var downloaderDlg = TryWaitForOpenForm<MultiButtonMsgDlg>(2000);
                    if (downloaderDlg != null)
                    {
                        PauseForScreenShot<MultiButtonMsgDlg>("Import Peptide Search - Download Java and Crux", tutorialPage++); // Maybe someday
                        OkDialog(downloaderDlg, downloaderDlg.ClickYes);
                        var waitDlg = WaitForOpenForm<LongWaitDlg>();
                        WaitForClosedForm(waitDlg);
                    }
                }
            }

            RunUI(() =>
            {
                importPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = new MzTolerance(5, MzTolerance.Units.ppm);
                importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = new MzTolerance(10, MzTolerance.Units.ppm);
                // Using the default q value of 0.01 (FDR 1%) is best for teaching and requires less explaining
                // importPeptideSearchDlg.SearchSettingsControl.CutoffScore = 0.05;
                if (useMsFragger)
                {
                    importPeptideSearchDlg.SearchSettingsControl.SetAdditionalSetting("check_spectral_files", "0");
                    //importPeptideSearchDlg.SearchSettingsControl.SetAdditionalSetting("keep-intermediate-files", "True");
                }

                importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
            });
            PauseForScreenShot<ImportPeptideSearchDlg.DDASearchSettingsPage>("Import Peptide Search - DDA Search Settings page", tutorialPage++);


            // Run the search
            try
            {
                RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

                WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_page);
                PauseForScreenShot<ImportPeptideSearchDlg.DDASearchPage>("Import Peptide Search - DDA Search Progress page", tutorialPage++);

                // Wait for search to finish
                WaitForConditionUI(60000 * 60, () => searchSucceeded.HasValue);
                RunUI(() => Assert.IsTrue(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("SearchControlLog.txt", importPeptideSearchDlg.SearchControl.LogText);
            }

            if (IsCoverShotMode)
            {
                RunUI(() => importPeptideSearchDlg.Width = 404);
                _searchLogImage = ScreenshotManager.TakeNextShot(importPeptideSearchDlg);
                Assert.IsNotNull(_searchLogImage);
            }

            bool isNotAmanda = false;
            RunUI(() => isNotAmanda = importPeptideSearchDlg.SearchSettingsControl.SelectedSearchEngine != SearchSettingsControl.SearchEngine.MSAmanda);

            // clicking 'Finish' (Next) will run ImportFasta
            AssociateProteinsDlg emptyProteinsDlg;
            if (isNotAmanda)
            {
                emptyProteinsDlg = ShowDialog<AssociateProteinsDlg>(() => importPeptideSearchDlg.ClickNextButton());
            }
            else
            {
                var ambiguousDlg = ShowDialog<MessageDlg>(() => importPeptideSearchDlg.ClickNextButton());
                PauseForScreenShot<MessageDlg>("Import Peptide Search - Ambiguous Peptides dialog", tutorialPage++);
                RunUI(() => AssertEx.Contains(ambiguousDlg.Message,
                    Resources.BiblioSpecLiteBuilder_AmbiguousMatches_The_library_built_successfully__Spectra_matching_the_following_peptides_had_multiple_ambiguous_peptide_matches_and_were_excluded_));
                OkDialog(ambiguousDlg, ambiguousDlg.OkDialog);
                emptyProteinsDlg = WaitForOpenForm<AssociateProteinsDlg>(600 * 1000);
            }

            RunUI(() => emptyProteinsDlg.RemoveRepeatedPeptides = true);

            WaitForConditionUI(() => emptyProteinsDlg.DocumentFinalCalculated);

            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                /*emptyProteinsDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
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
                        Assert.AreEqual(11050, proteinCount);
                        Assert.AreEqual(25784, peptideCount);
                        Assert.AreEqual(51162, precursorCount);
                        Assert.AreEqual(153486, transitionCount);
                    }
                }*/

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
                        Assert.AreEqual(2637, proteinCount);
                        Assert.AreEqual(5299, peptideCount);
                        Assert.AreEqual(10461, precursorCount);
                        Assert.AreEqual(31383, transitionCount);
                    }
                }
            });
            PauseForScreenShot("Import Peptide Search - Empty Proteins dialog", tutorialPage++);

            using (new WaitDocumentChange(null, true, 600 * 1000))
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

            DirectoryEx.SafeDelete(Path.Combine(Path.GetDirectoryName(SearchFiles.First())!, "converted"));
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
