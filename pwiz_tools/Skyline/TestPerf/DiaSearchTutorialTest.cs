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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class DiaSearchTutorialTest : AbstractFunctionalTest
    {
        /// <summary>
        /// Change to true to write new Assert statements instead of testing them.
        /// </summary>
        protected override bool IsRecordMode => false;

        private AnalysisValues _analysisValues;

        private class AnalysisValues
        {
            public string IsolationSchemeName;
            public string IsolationSchemeFile;
            public char IsolationSchemeFileSeparator;
            public bool IsolationSchemeHasGaps;
            public bool IsolationSchemeHasOverlaps;

            public int[] FinalTargetCounts;

            //public string FastaPathForSearch;
            public string FastaPath => "20220721-uniprot-sprot-human.fasta";

            public string ZipPath;
            public string[] DiaFiles;
            public bool IsGpfData;

            public string ProteinToSelect;
            public string PeptideToSelect;

            public MzTolerance PrecursorMzTolerance;
            public MzTolerance FragmentMzTolerance;
            public Dictionary<string, string> AdditionalSettings;
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDiaSearchStellarTutorialDraft()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;
            //IsCoverShotMode = true;
            //RunPerfTests = true;
            CoverShotName = "DIASearchStellar";

            //LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/DDASearch-22_2.pdf";


            _analysisValues = new AnalysisValues
            {
                IsolationSchemeName = "Stellar GPF",
                IsolationSchemeFile = "Stellar_GPF.csv",
                IsolationSchemeFileSeparator = TextUtil.SEPARATOR_CSV,
                IsolationSchemeHasGaps = true,
                IsolationSchemeHasOverlaps = false,

                FinalTargetCounts = new[] { 918, 3174, 3174, 25390 },

                ZipPath = "https://skyline.ms/tutorials/DiaSearchTutorial.zip",
                DiaFiles = new[] {
                    "P2_202405_neo_150uID_CSF_GPF_2ThDIA_500-600_30m_14.mzML",
                    "P2_202405_neo_150uID_CSF_GPF_2ThDIA_600-700_30m_15.mzML",
                },
                IsGpfData = true,

                ProteinToSelect = "sp|O15240|VGF_HUMAN",
                PeptideToSelect = "LADLASDLLLQYLLQGGAR",

                PrecursorMzTolerance = new MzTolerance(3.0),
                FragmentMzTolerance = new MzTolerance(0.2),
                AdditionalSettings = new Dictionary<string, string>
                {
                    //{ "data_type", "2" } // set MSFragger to GPF mode
                },
            };

            TestFilesZipPaths = new[]
            {
                _analysisValues.ZipPath,
                @"TestPerf/DdaTutorialViews.zip"
            };

            TestFilesPersistent = new [] { ".mzML" };

            RunFunctionalTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDiaSearchQeTutorialDraft()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;
            //IsCoverShotMode = true;
            //RunPerfTests = true;
            CoverShotName = "DIASearchQE";

            //LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/DDASearch-22_2.pdf";


            _analysisValues = new AnalysisValues
            {
                IsolationSchemeName = "QE wide",
                IsolationSchemeFile = "QE_wide.csv",
                IsolationSchemeFileSeparator = TextUtil.SEPARATOR_CSV,
                IsolationSchemeHasGaps = false,
                IsolationSchemeHasOverlaps = false,

                //FinalTargetCounts = new[] { 7220, 11153, 11184, 71518 },
                //FinalTargetCounts = new[] { 8424, 31857, 34527, 256880 }, // 10ppm 1 file
                //FinalTargetCounts = new[] { 7672, 29052, 31584, 235996 }, // 20ppm 1 file 730s
                //FinalTargetCounts = new[] { 6322, 22959, 24995, 188926 }, // 50ppm 1 file 730s
                //FinalTargetCounts = new[] { 8621, 31609, 34116, 254400 }, // 5ppm 1 file 730s
                //FinalTargetCounts = new[] { 3827, 8949, 9588, 72122 }, // 10ppm 1 file 200s, 60-80 min subset, wrong MS2 mode
                //FinalTargetCounts = new[] { 5056, 12186, 13032, 96602 }, // 10ppm 2 file 355s, 60-80 min subset, wrong MS2 mode
                //FinalTargetCounts = new[] { 5014, 12048, 12859, 95325 },
                FinalTargetCounts = new[] { 2652, 8774, 9568, 74239 },

                ZipPath = "https://skyline.ms/tutorials/DiaSearchTutorial.zip",
                DiaFiles = new[] {
                    "23aug2017_hela_serum_timecourse_wide_1a.mzML",
                    //"23aug2017_hela_serum_timecourse_wide_1b.mzML",
                },

                ProteinToSelect = "sp|P21333|FLNA_HUMAN",
                PeptideToSelect = "IANLQTDLSDGLR",

                PrecursorMzTolerance = new MzTolerance(10, MzTolerance.Units.ppm),
                FragmentMzTolerance = new MzTolerance(10, MzTolerance.Units.ppm),
            };

            TestFilesZipPaths = new[]
            {
                _analysisValues.ZipPath,
                @"TestPerf/DdaTutorialViews.zip"
            };

            TestFilesPersistent = new[] { ".mzML" };

            RunFunctionalTest();
        }

        private const string OXIDATION_M = "Oxidation (M)";

        private string GetTestPath(string path)
        {
            return TestFilesDirs[0].GetTestPath(path);
        }

        private IEnumerable<string> SearchFiles
        {
            get { return _analysisValues.DiaFiles.Select(GetTestPath); }
        }

        protected override void DoTest()
        {
            TestMsFraggerSearch();
        }

        private bool RedownloadTools => !IsRecordMode && !IsRecordAuditLogForTutorials;
        private bool HasMissingDependencies => !SearchSettingsControl.HasRequiredFilesDownloaded(SearchSettingsControl.SearchEngine.MSFragger);

        private Image _searchLogImage;

        protected override Bitmap ProcessCoverShot(Bitmap bmp)
        {
            using var graph = Graphics.FromImage(base.ProcessCoverShot(bmp));
            graph.DrawImageUnscaled(_searchLogImage, bmp.Width - _searchLogImage.Width - 10, bmp.Height - _searchLogImage.Height - 30);
            return bmp;
        }

        /// <summary>
        /// Test that the "Match Modifications" page of the Import Peptide Search wizard gets skipped.
        /// </summary>
        private void TestMsFraggerSearch()
        {
            PrepareDocument("TestDiaSearchTutorial.sky");

            // delete downloaded tools if not recording new counts or audit logs
            if (RedownloadTools)
                foreach (var requiredFile in MsFraggerSearchEngine.FilesToDownload)
                    if (requiredFile.Unzip)
                        DirectoryEx.SafeDelete(requiredFile.InstallPath);
                    else
                        FileEx.SafeDelete(Path.Combine(requiredFile.InstallPath, requiredFile.Filename));

            // Set standard type to None
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            RunUI(() => peptideSettingsUI.SelectedInternalStandardTypeName = Resources.LabelTypeComboDriver_LoadList_none);
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modifications tab");

            var docBeforePeptideSettings = SkylineWindow.Document;
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForDocumentChangeLoaded(docBeforePeptideSettings);

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Select DIA Files to Search page");

            // We're on the "Build Spectral Library" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => new MsDataFilePath(o)).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = false;
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - After Selecting DIA Files page");

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                importPeptideSearchDlg.ImportResultsDIAControl.IsGpf = _analysisValues.IsGpfData;
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Extract Chromatograms page");

            SkylineWindow.BeginInvoke(new Action(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton())));

            if (SearchFiles.Count() > 1)
            {
                // Remove prefix/suffix dialog pops up; accept default behavior
                var removeSuffix = WaitForOpenForm<ImportResultsNameDlg>();
                PauseForScreenShot<ImportResultsNameDlg>("Import Results - Common prefix form");
                OkDialog(removeSuffix, () => removeSuffix.YesDialog());
                WaitForDocumentLoaded();
            }

            // We're on the "Match Modifications" page. Add M+16
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
            PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Import Peptide Search - Add Modifications page");

            var editStructModListUI =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(importPeptideSearchDlg.MatchModificationsControl.ClickAddStructuralModification);
            RunDlg<EditStaticModDlg>(editStructModListUI.AddItem, editModDlg =>
            {
                editModDlg.SetModification(OXIDATION_M); // Not L10N
                editModDlg.OkDialog();
            });
            OkDialog(editStructModListUI, editStructModListUI.OkDialog);

            RunUI(() => importPeptideSearchDlg.MatchModificationsControl.ChangeAll(true));
            PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Import Peptide Search - After adding modifications page");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                importPeptideSearchDlg.TransitionSettingsControl.MinIonCount = 3;
                importPeptideSearchDlg.TransitionSettingsControl.IonCount = 5;
                if (_analysisValues.FragmentMzTolerance.Unit == MzTolerance.Units.mz)
                    importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance = _analysisValues.FragmentMzTolerance.Value;
                else
                    importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance = 0.005;
            });

            if (_analysisValues.IsGpfData)
            {
                var editSpectrumFilterDlg = ShowDialog<EditSpectrumFilterDlg>(importPeptideSearchDlg.TransitionSettingsControl.EditSpectrumFilter);
                RunUI(() =>
                {
                    editSpectrumFilterDlg.SelectPage(1);
                    var row = editSpectrumFilterDlg.RowBindingList.AddNew();
                    Assert.IsNotNull(row);
                    row.SetProperty(SpectrumClassColumn.IsolationWindowWidth);
                    row.SetOperation(FilterOperations.OP_IS_LESS_THAN);
                    row.SetValue(50);
                });

                PauseForScreenShot<EditSpectrumFilterDlg>("Import Peptide Search - Advanced spectrum filtering");
                OkDialog(editSpectrumFilterDlg, editSpectrumFilterDlg.OkDialog);
            }
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("Import Peptide Search - Configure Transition Settings page");

            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the MS1 full scan settings page.
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
            RunUI(() =>
            {
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 5;
                if (_analysisValues.FragmentMzTolerance.Unit == MzTolerance.Units.mz)
                {
                    importPeptideSearchDlg.FullScanSettingsControl.ProductMassAnalyzer = FullScanMassAnalyzerType.qit;
                    importPeptideSearchDlg.FullScanSettingsControl.ProductResMz = _analysisValues.FragmentMzTolerance.Value;
                }
            });

            var isolationScheme = ShowDialog<EditIsolationSchemeDlg>(importPeptideSearchDlg.FullScanSettingsControl.AddIsolationScheme);
            RunUI(() =>
            {
                isolationScheme.IsolationSchemeName = _analysisValues.IsolationSchemeName;
                isolationScheme.UseResults = false;
            });
            RunDlg<OpenDataSourceDialog>(isolationScheme.ImportRanges, importRangesDlg =>
            {
                importRangesDlg.CurrentDirectory = new MsDataFilePath(Path.GetDirectoryName(SearchFiles.First()));
                _analysisValues.DiaFiles.ForEach(importRangesDlg.SelectFile);
                importRangesDlg.Open();
            });
            var schemeLines = File.ReadAllLines(GetTestPath(_analysisValues.IsolationSchemeFile));
            string[][] windowFields = schemeLines.Select(l => TextUtil.ParseDsvFields(l, _analysisValues.IsolationSchemeFileSeparator)).ToArray();
            WaitForConditionUI(() => isolationScheme.GetIsolationWindows().Count > 10);
            bool hasMargin = windowFields[0].Length == 3;

            try
            {
                RunUI(() =>
                {
                    Assert.AreEqual(hasMargin, isolationScheme.SpecifyMargin);
                    int schemeRow = 0;
                    foreach (var isolationWindow in isolationScheme.GetIsolationWindows())
                    {
                        var fields = windowFields[schemeRow++];
                        Assert.AreEqual(double.Parse(fields[0], CultureInfo.InvariantCulture), isolationWindow.MethodStart, 0.01);
                        Assert.AreEqual(double.Parse(fields[1], CultureInfo.InvariantCulture), isolationWindow.MethodEnd, 0.01);
                        if (hasMargin)
                            Assert.AreEqual(double.Parse(fields[2], CultureInfo.InvariantCulture), isolationWindow.StartMargin ?? 0, 0.01);
                    }
                });
            }
            catch (Exception ex)
            {
                PauseForRecordModeInstruction("Copy new isolation scheme to file if old one is incorrect.", ex);
            }
            PauseForScreenShot<EditIsolationSchemeDlg>("Isolation scheme");
            var isolationGraph = ShowDialog<DiaIsolationWindowsGraphForm>(isolationScheme.OpenGraph);
            PauseForScreenShot<DiaIsolationWindowsGraphForm>("Isolation scheme graph");

            OkDialog(isolationGraph, isolationGraph.CloseButton);
            var okDlgAction = new Action(isolationScheme.OkDialog);
            if (_analysisValues.IsolationSchemeHasOverlaps)
            {
                var messageDlg = ShowDialog<MultiButtonMsgDlg>(okDlgAction);
                RunUI(() =>
                {
                    AssertEx.AreComparableStrings(SettingsUIResources.EditIsolationSchemeDlgOkDialogThereAreOverlapsContinue, messageDlg.Message);
                    messageDlg.BtnYesClick();
                    okDlgAction = () => { };
                });
            }
            if (_analysisValues.IsolationSchemeHasGaps)
            {
                var messageDlg = ShowDialog<MultiButtonMsgDlg>(okDlgAction);
                RunUI(() =>
                {
                    AssertEx.AreComparableStrings(Resources.EditIsolationSchemeDlg_OkDialog_There_are_gaps_in_a_single_cycle_of_your_extraction_windows__Do_you_want_to_continue_,
                        messageDlg.Message);
                    messageDlg.BtnYesClick();
                    okDlgAction = () => { };
                });
            }

            RunUI(okDlgAction);
            WaitForClosedForm(isolationScheme);

            PauseForScreenShot<ImportPeptideSearchDlg.Ms1FullScanPage>("Import Peptide Search - Configure Full-Scan Settings page");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Import FASTA" page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                //Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = 1;
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath(_analysisValues.FastaPath));
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the DIA-Umpire settings page
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.converter_settings_page);
                Assert.IsFalse(importPeptideSearchDlg.ConverterSettingsControl.UseDiaUmpire);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - DIA-Umpire Settings page");

            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Adjust Search Settings" page
            bool? searchSucceeded = null;
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page));

            RunUI(() =>
            {
                importPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = _analysisValues.PrecursorMzTolerance;
                importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = _analysisValues.FragmentMzTolerance;
                // Using the default q value of 0.01 (FDR 1%) is best for teaching and requires less explaining
                // importPeptideSearchDlg.SearchSettingsControl.CutoffScore = 0.05;
                importPeptideSearchDlg.SearchSettingsControl.SetAdditionalSetting("check_spectral_files", "0");
                //importPeptideSearchDlg.SearchSettingsControl.SetAdditionalSetting("keep-intermediate-files", "True");

                if (_analysisValues.AdditionalSettings != null)
                    foreach(var setting in _analysisValues.AdditionalSettings)
                        importPeptideSearchDlg.SearchSettingsControl.SetAdditionalSetting(setting.Key, setting.Value);

                importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
            });
            PauseForScreenShot<ImportPeptideSearchDlg.DDASearchSettingsPage>("Import Peptide Search - Search Settings page");

            WaitForConditionUI(() => _analysisValues.FragmentMzTolerance.Unit == importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance.Unit);

            // Run the search
            if (IsCoverShotMode)
            {
                // Resize the form before running or the output will not appear scrolled to the end
                RunUI(() => importPeptideSearchDlg.Size = new Size(404, 578));  // minimum height
            }

            SkylineWindow.BeginInvoke(new Action(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton())));

            // SearchEngine changed to MSFragger automatically due to changing to DIA workflow: handle download dialogs if necessary
            if (RedownloadTools || HasMissingDependencies)
            {
                var msfraggerDownloaderDlg = TryWaitForOpenForm<MsFraggerDownloadDlg>(2000);
                if (msfraggerDownloaderDlg != null)
                {
                    PauseForScreenShot<MsFraggerDownloadDlg>("Import Peptide Search - Download MSFragger"); // Maybe someday
                    RunUI(() => msfraggerDownloaderDlg.SetValues("Matt (testing download from Skyline)", "Chambers", "chambem2@uw.edu", "UW"));
                    OkDialog(msfraggerDownloaderDlg, msfraggerDownloaderDlg.ClickAccept);
                }

                var downloaderDlg = TryWaitForOpenForm<MultiButtonMsgDlg>(2000);
                if (downloaderDlg != null)
                {
                    PauseForScreenShot<MultiButtonMsgDlg>("Import Peptide Search - Download Java and Crux"); // Maybe someday
                    OkDialog(downloaderDlg, downloaderDlg.ClickYes);
                    var waitDlg = WaitForOpenForm<LongWaitDlg>();
                    WaitForClosedForm(waitDlg);
                }
            }

            try
            {

                WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_page);
                PauseForScreenShot<ImportPeptideSearchDlg.DDASearchPage>("Import Peptide Search - Search Progress page");

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
                _searchLogImage = ScreenshotManager.TakeShot(importPeptideSearchDlg);
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
                PauseForScreenShot<MessageDlg>("Import Peptide Search - Ambiguous Peptides dialog");
                RunUI(() => AssertEx.Contains(ambiguousDlg.Message,
                    Resources.BiblioSpecLiteBuilder_AmbiguousMatches_The_library_built_successfully__Spectra_matching_the_following_peptides_had_multiple_ambiguous_peptide_matches_and_were_excluded_));
                OkDialog(ambiguousDlg, ambiguousDlg.OkDialog);
                emptyProteinsDlg = WaitForOpenForm<AssociateProteinsDlg>(600 * 1000);
            }

            RunUI(() => emptyProteinsDlg.RemoveRepeatedPeptides = true);

            WaitForConditionUI(() => emptyProteinsDlg.DocumentFinalCalculated);

            // cleanup output files in the persistent directory whether test succeeds or fails
            foreach (var searchFile in SearchFiles)
            {
                var dir = Path.GetDirectoryName(searchFile);
                var pepxml = Path.GetFileNameWithoutExtension(searchFile) + "-percolator.pepXML";
                FileEx.SafeDelete(Path.Combine(dir!, pepxml));
            }

            RunUI(() =>
            {
                emptyProteinsDlg.NewTargetsFinalSync(out var proteinCount, out var peptideCount, out var precursorCount, out var transitionCount);
                if (!IsFullData)
                {
                    if (IsRecordMode)
                    {
                        Console.WriteLine();
                        Console.WriteLine(@"{0} = new[] {{ {1}, {2}, {3}, {4} }},", @"FinalTargetCounts", proteinCount, peptideCount, precursorCount, transitionCount);
                    }
                    else
                    {
                        Assert.AreEqual(_analysisValues.FinalTargetCounts[0], proteinCount);
                        Assert.AreEqual(_analysisValues.FinalTargetCounts[1], peptideCount);
                        Assert.AreEqual(_analysisValues.FinalTargetCounts[2], precursorCount);
                        Assert.AreEqual(_analysisValues.FinalTargetCounts[3], transitionCount);
                    }
                }
            });
            PauseForScreenShot("Import Peptide Search - Empty Proteins dialog");

            using (new WaitDocumentChange(null, true, 600 * 1000))
            {
                OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);
            }

            string proteinNameToSelect = _analysisValues.ProteinToSelect;
            string peptideToSelect = _analysisValues.PeptideToSelect;
            if (Equals(proteinNameToSelect, SkylineWindow.Document.MoleculeGroups.Skip(1).First().Name))
                SelectNode(SrmDocument.Level.MoleculeGroups, 1);
            else
                FindNode(proteinNameToSelect);

            RunUI(() =>
            {
                Assert.AreEqual(proteinNameToSelect, SkylineWindow.SelectedNode.Text);

                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.Size = new Size(1226, 900);
            });
            //RestoreViewOnScreenNoSelChange(18);
            WaitForGraphs();
            PauseForScreenShot("Manual review window layout with protein selected");

            try
            {
                FindNode(peptideToSelect);
                WaitForGraphs();
                PauseForScreenShot("Manual review window layout with peptide selected");
            }
            catch (AssertFailedException e)
            {
                PauseForRecordModeInstruction($"Clicking the peptide ({peptideToSelect}) failed.\r\n" +
                                              "Pick a new peptide to select.", e);
            }

            RunUI(SkylineWindow.AutoZoomBestPeak);
            WaitForGraphs();

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
