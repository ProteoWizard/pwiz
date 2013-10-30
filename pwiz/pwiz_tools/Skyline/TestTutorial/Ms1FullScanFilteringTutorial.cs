/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for MS1 Full-Scan Filtering
    /// </summary>
    [TestClass]
    public class Ms1FullScanFilteringTutorial : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMs1Tutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            TestFilesZipPaths = new[]
                {
                    ExtensionTestContext.CanImportAbWiff
                        ? @"https://skyline.gs.washington.edu/tutorials/MS1Filtering_2.zip" // Not L10N
                        : @"https://skyline.gs.washington.edu/tutorials/MS1FilteringMzml_2.zip", // Not L10N
                    @"TestTutorial\Ms1FullScanFilteringViews.zip"
                };
            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            var folderMs1Filtering = ExtensionTestContext.CanImportAbWiff ? "Ms1Filtering" : "Ms1FilteringMzml"; // Not L10N
            return TestFilesDirs[0].GetTestPath(folderMs1Filtering + '\\' + path);
        }

        protected override void DoTest()
        {
            // Clean-up before running the test
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                            d => d.ChangeSettings(SrmSettingsList.GetDefault())));

            SrmDocument doc = SkylineWindow.Document;

            const string documentBaseName = "Ms1FilterTutorial";
            string documentFile = GetTestPath(documentBaseName + SrmDocument.EXT);
            RunUI(() => SkylineWindow.SaveDocument(documentFile));

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            PauseForScreenShot("page 2 - empty wizard");

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            string[] searchFiles = new[]
                {
                    GetTestPath("100803_0001_MCF7_TiB_L.group.xml"),  // Not L10N
                    GetTestPath("100803_0005b_MCF7_TiTip3.group.xml")  // Not L10N
                };
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                            ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
            });
            PauseForScreenShot("page 3 - library page");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            doc = WaitForDocumentChange(doc);

            // Verify document library was built
            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(documentFile);
            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
            Assert.IsTrue(File.Exists(docLibPath) && File.Exists(redundantDocLibPath));
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(librarySettings.HasDocumentLibrary);

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page));
            PauseForScreenShot("page 4 - results page");

            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            PauseForScreenShot("page 5 - remove prefix form");

            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            // We're on the "Match Modifications" page of the wizard.
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);

            List<string> modsToCheck = new List<string> { "Phospho (ST)", "Phospho (Y)", "Oxidation (M)" }; // Not L10N
            RunUI(() =>
            {
                importPeptideSearchDlg.MatchModificationsControl.CheckedModifications = modsToCheck;
            });
            PauseForScreenShot("page 6 - modifications");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            doc = WaitForDocumentChange(doc);

            // We're on the "Configure MS1 Full-Scan Settings" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.ms1_full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3, 4 };
                Assert.AreEqual(importPeptideSearchDlg.FullScanSettingsControl.PrecursorIsotopesCurrent, FullScanPrecursorIsotopes.Count);
                Assert.AreEqual(3, importPeptideSearchDlg.FullScanSettingsControl.Peaks);
                Assert.AreEqual(RetentionTimeFilterType.ms2_ids, importPeptideSearchDlg.FullScanSettingsControl.RetentionTimeFilterType);
                Assert.AreEqual(5, importPeptideSearchDlg.FullScanSettingsControl.TimeAroundMs2Ids);
            });
            PauseForScreenShot("page 7 - full-scan settings");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            doc = WaitForDocumentChange(doc);

            // Last page of wizard - Import Fasta.
            string fastaPath = GetTestPath("12_proteins.062011.fasta");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = 2;
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastaPath);
            });
            PauseForScreenShot("page 9 - import fasta page");

            OkDialog(importPeptideSearchDlg, () => importPeptideSearchDlg.ClickNextButton());
            WaitForDocumentChangeLoaded(doc, 8 * 60 * 1000); // 10 minutes

            var libraryExplorer = ShowDialog<ViewLibraryDlg>(() => SkylineWindow.OpenLibraryExplorer(documentBaseName));
            var matchedPepModsDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            PauseForScreenShot("page 11 - add mods alert");
            RunUI(() =>
                {
                    Assert.IsTrue(matchedPepModsDlg.Message.StartsWith(Resources.ViewLibraryDlg_MatchModifications_This_library_appears_to_contain_the_following_modifications));
                    Assert.IsTrue(matchedPepModsDlg.Message.Split('\n').Length > 16);
                    matchedPepModsDlg.BtnCancelClick();
                });
            RunUI(() =>
                {
                    libraryExplorer.GraphSettings.ShowBIons = true;
                    libraryExplorer.GraphSettings.ShowYIons = true;
                    libraryExplorer.GraphSettings.ShowCharge1 = true;
                    libraryExplorer.GraphSettings.ShowCharge2 = true;
                    libraryExplorer.GraphSettings.ShowPrecursorIon = true;
                });

            PauseForScreenShot("page 12 - spectral library explorer");
            RunUI(() =>
                {
                    const string sourceFirst = "100803_0005b_MCF7_TiTip3.wiff";
                    const double timeFirst = 35.2128;
                    Assert.AreEqual(sourceFirst, libraryExplorer.SourceFile);
                    Assert.AreEqual(timeFirst, libraryExplorer.RetentionTime, 0.0001);
                    libraryExplorer.SelectedIndex++;
                    Assert.AreNotEqual(sourceFirst, libraryExplorer.SourceFile);
                    Assert.AreNotEqual(timeFirst, libraryExplorer.RetentionTime, 0.0001);
                });
            OkDialog(libraryExplorer, libraryExplorer.CancelDialog);

            AssertEx.IsDocumentState(SkylineWindow.Document, null, 11, 51, 52, 156);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, GetFileNameWithoutExtension(searchFiles[0]), 48, 49, 0, 143, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, GetFileNameWithoutExtension(searchFiles[1]), 49, 50, 0, 143, 0);

            // Select the first transition group.
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Peptides, 0);
                SkylineWindow.GraphSpectrumSettings.ShowAIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowBIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowYIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowPrecursorIon = true;
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
            });
            RunDlg<SpectrumChartPropertyDlg>(SkylineWindow.ShowSpectrumProperties, dlg =>
            {
                dlg.FontSize = 12;
                dlg.OkDialog();
            });
            RunDlg<ChromChartPropertyDlg>(SkylineWindow.ShowChromatogramProperties, dlg =>
            {
                dlg.FontSize = 12;
                dlg.OkDialog();
            });
            RunUI(() =>
                {
                    // Make window screenshot size
                    if (IsPauseForScreenShots && SkylineWindow.WindowState != FormWindowState.Maximized)
                    {
                        SkylineWindow.Width = 1160;
                        SkylineWindow.Height = 792;
                    }
                });
            RunUI(() => SkylineWindow.LoadLayout(new FileStream(TestFilesDirs[1].GetTestPath(@"p13.view"), FileMode.Open)));
            PauseForScreenShot("page 13 - imported data");   // p. 12

            doc = SkylineWindow.Document;
            RunUI(() =>
            {
                SkylineWindow.IntegrateAll();
                SkylineWindow.ShowGraphPeakArea(true);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none);
                Settings.Default.ShowDotProductPeakArea = true;
                Settings.Default.ShowLibraryPeakArea = true;
            });
            WaitForDocumentChange(doc);
            PauseForScreenShot("page 14 - peak are view");   // p. 14

            RunUI(() => SkylineWindow.LoadLayout(new FileStream(TestFilesDirs[1].GetTestPath(@"p15.view"), FileMode.Open)));
            RunUI(() =>
            {
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.ShowChromatogramLegends(false);
            });

            PauseForScreenShot("page 15 - main window layout");   // p. 15

            RunUI(() =>
                {
                    SkylineWindow.CollapsePeptides();
                    var pathPep = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.Peptides, 3);
                    SkylineWindow.SelectedPath = pathPep;
                    SkylineWindow.ShowAlignedPeptideIDTimes(true);

                    var nodeGroup = ((PeptideDocNode) SkylineWindow.DocumentUI.FindNode(pathPep)).TransitionGroups.First();
                    var graphChrom = SkylineWindow.GraphChromatograms.First();
                    var listChanges = new List<ChangedPeakBoundsEventArgs>
                        {
                            new ChangedPeakBoundsEventArgs(new IdentityPath(pathPep, nodeGroup.TransitionGroup),
                                null,
                                graphChrom.NameSet,
                                graphChrom.ChromGroupInfos[0].FilePath,
                                new ScaledRetentionTime(38.8),
                                new ScaledRetentionTime(39.4),
                                PeakIdentification.ALIGNED,
                                PeakBoundsChangeType.both)
                        };
                    graphChrom.SimulateChangedPeakBounds(listChanges);
                });
            WaitForGraphs();

            PauseForScreenShot("page 17 - chromatogram graphs");    // p. 17

            var alignmentForm = ShowDialog<AlignmentForm>(() => SkylineWindow.ShowRetentionTimeAlignmentForm());

            RunUI(() =>
                {
                    alignmentForm.Width = 711;
                    alignmentForm.Height = 561;
                });

            PauseForScreenShot("page 18 - retention time alginement form");

            OkDialog(alignmentForm, alignmentForm.Close);

/*
            RunUI(SkylineWindow.AutoZoomNone);
            PauseForScreenShot();   // p. 16

            // Jump to another peptide.
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findDlg =>
            {
                findDlg.FindOptions = new FindOptions().ChangeText("YGP"); // Not L10N
                findDlg.FindNext();
                findDlg.Close();
            });
            PauseForScreenShot();   // p. 17, figure 1

            RunUI(SkylineWindow.AutoZoomBestPeak);
            PauseForScreenShot();   // p. 17, figure 2; p. 18, figure 1 & 2

            // Limiting the chromatogram extraction time range.
            doc = SkylineWindow.Document;
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                    transitionSettingsUI.MinTime = 10;
                    transitionSettingsUI.MaxTime = 100;
                });
                PauseForScreenShot();   // p. 20

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }
            WaitForDocumentChange(doc);

            // Re-importing raw data.
            doc = SkylineWindow.Document;
            {
                var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                RunUI(manageResultsDlg.ReimportResults);
                PauseForScreenShot();

                OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);
            }
            SrmDocument docAfter = WaitForDocumentChangeLoaded(doc, 8*60*1000); // 8 minutes
            PauseAndContinue();
            AssertEx.IsDocumentState(docAfter, null, 11, 51, 52, 156);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, Path.GetFileNameWithoutExtension(searchFiles[0]), 51, 52, 0, 156, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, Path.GetFileNameWithoutExtension(searchFiles[1]), 51, 52, 0, 156, 0);

            RunUI(SkylineWindow.AutoZoomNone);
*/
            var docAfter = SkylineWindow.Document;

            // Minimizing a chromatogram cache file.
            RunUI(SkylineWindow.CollapsePeptides);
            for (int i = 0; i < 5; i++) // just do the first 5
            {
                int iPeptide = i;
                var path = docAfter.GetPathTo((int) SrmDocument.Level.Peptides, iPeptide);
                RunUI(() =>
                {
                    SkylineWindow.SelectedPath = path;
                });
                WaitForGraphs();
            }

            // Eliminate extraneous chromatogram data.
            doc = SkylineWindow.Document;
            var minimizedFile = GetTestPath("Ms1FilteringTutorial-2min.sky"); // Not L10N
            var cacheFile = minimizedFile + "d"; // Not L10N
            {
                var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                var minimizeResultsDlg = ShowDialog<MinimizeResultsDlg>(manageResultsDlg.MinimizeResults);
                RunUI(() =>
                {
                    minimizeResultsDlg.LimitNoiseTime = true;
                    minimizeResultsDlg.NoiseTimeRange = 2; // Not L10N
                });
                PauseForScreenShot();   // p. 23

                OkDialog(minimizeResultsDlg, () => minimizeResultsDlg.MinimizeToFile(minimizedFile));
                WaitForCondition(() => File.Exists(cacheFile));
                WaitForClosedForm(manageResultsDlg);
            }
            WaitForDocumentChange(doc);

            // Inclusion list method export for MS1 filtering
            doc = SkylineWindow.Document;
            RunDlg<PeptideSettingsUI>(() => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction), dlg =>
            {
                dlg.IsUseMeasuredRT = true;
                dlg.TimeWindow = 10;
                dlg.OkDialog();
            });
            WaitForDocumentChangeLoaded(doc);

            // Now deviating from the tutorial script for a moment to make sure we can choose a Scheduled export method.
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, dlg =>
            {
                dlg.MinPeptides = 1; // Not L10N
                const double minPeakFoundRatio = 0.1;
                dlg.MinPeakFoundRatio = minPeakFoundRatio;
                dlg.OkDialog();
            });

            // Ready to export, although we will just cancel out of the dialog.
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = ExportInstrumentType.ABI_TOF; // Not L10N
                exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                exportMethodDlg.CancelButton.PerformClick();
            });
            WaitForClosedForm(exportMethodDlg);

            RunUI(() => SkylineWindow.SaveDocument());
            RunUI(SkylineWindow.NewDocument);
        }

        private string GetFileNameWithoutExtension(string searchFile)
        {
            searchFile = Path.GetFileName(searchFile) ?? "";
            // Remove the shared prefix and everything after the first period
            const int prefixLen = 10;
            return searchFile.Substring(prefixLen, searchFile.IndexOf('.') - prefixLen);
        }
    }
}
