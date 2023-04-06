/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.SkylineTestUtil;

namespace TestPerf // This would be in tutorial tests if it didn't take about 10 minutes to run
{
    /// <summary>
    /// Verify measured drift time tutorial operation
    /// </summary>
    [TestClass]
    public class DriftTimePredictorTutorialTest : AbstractFunctionalTestEx
    {
        private const string BSA_Frag = "BSA_Frag_100nM_18May15_Fir_15-04-02.d";
        private const string Yeast_BSA = "Yeast_0pt1ug_BSA_100nM_18May15_Fir_15-04-01.d";

        private const string EXT_ZIP = ".zip";

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDriftTimePredictorTutorial()
        {
//            IsPauseForScreenShots = true;
//            RunPerfTests = true;
//            IsCoverShotMode = true;
            CoverShotName = "IMSFiltering";

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/IMSFiltering-20_2.pdf";

            const string dataRoot = "http://skyline.ms/tutorials/data-drift/";
            TestFilesZipPaths = new[]
            {
                @"http://skyline.ms/tutorials/IMSFiltering.zip",
                @"TestPerf\DriftTimePredictorExtra.zip",
                @"TestPerf\DriftTimePredictorViews.zip",
                dataRoot + BSA_Frag + EXT_ZIP,
                dataRoot + Yeast_BSA + EXT_ZIP,
            };

            TestFilesZipExtractHere = new[] {false, false, false, true, true};

            TestFilesPersistent = new[] { BSA_Frag, Yeast_BSA };

            RunFunctionalTest();            
        }

        private string DataPath { get { return TestFilesDirs.Last().PersistentFilesDir; } }

        protected override void DoTest()
        {
            // Check backward compatibility with 19.1.9.338 and 350 when combined IMS got written to MsDataFilePath
            string legacyFile_19_1_9 = TestFilesDirs[1].GetTestPath(@"BSA-Training.sky");
            RunUI(() => SkylineWindow.OpenFile(legacyFile_19_1_9));
            VerifyCombinedIonMobility(WaitForDocumentLoaded());
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.NewDocument();
                SkylineWindow.OpenFile(legacyFile_19_1_9);
            });
            VerifyCombinedIonMobility(WaitForDocumentLoaded());
            var oldDoc = SkylineWindow.Document;
            string skyFile = TestFilesDirs[0].GetTestPath(@"IMSFiltering\BSA-Training.sky");
            RunUI(() => SkylineWindow.OpenFile(skyFile));

            var document = WaitForDocumentChangeLoaded(oldDoc,240*1000); // 4 minutes
            RunUI(() => SkylineWindow.Size = new Size(880, 560));
            RestoreViewOnScreen(2);
            PauseForScreenShot("Document open - full window", 2);
            AssertEx.IsDocumentState(document, null, 1, 34, 38, 404);

            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(() =>
                    SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.FullScan));

                PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Full-scan settings", 3);

                RunUI(() =>
                {
                    Assert.AreEqual(FullScanPrecursorIsotopes.Count, transitionSettingsUI.PrecursorIsotopesCurrent);
                    Assert.AreEqual(20*1000, transitionSettingsUI.PrecursorRes);
                    Assert.AreEqual(FullScanMassAnalyzerType.tof, transitionSettingsUI.PrecursorMassAnalyzer);
                    Assert.AreEqual(IsolationScheme.SpecialHandlingType.ALL_IONS, transitionSettingsUI.IsolationSchemeName);
                    Assert.AreEqual(20*1000, transitionSettingsUI.ProductRes);
                    Assert.AreEqual(FullScanMassAnalyzerType.tof, transitionSettingsUI.ProductMassAnalyzer);
                });

                OkDialog(transitionSettingsUI, transitionSettingsUI.CancelDialog);
            }

            {
                var askDecoysDlg = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ImportResults);
                var importResults = ShowDialog<ImportResultsDlg>(askDecoysDlg.ClickNo);
                RunUI(() => importResults.ImportSimultaneousIndex = 2);

                PauseForScreenShot<ImportResultsDlg>("Import results form", 4);

                // Importing raw data from a sample which is a mixture of yeast and BSA

                var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResults.OkDialog);

                RunUI(() =>
                {
                    openDataSourceDialog.CurrentDirectory = new MsDataFilePath(DataPath);
                    openDataSourceDialog.SelectAllFileType(".d");
                });
                PauseForScreenShot<OpenDataSourceDialog>("Import results files", 5);

                OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            }

            string yeastReplicateName = Path.GetFileNameWithoutExtension(Yeast_BSA);

            PauseForScreenShot<AllChromatogramsGraph>("Importing results form", 6);
            
            WaitForDocumentChangeLoaded(document, 1000 * 60 * 60 * 10); // 10 minutes

            // Arrange graphs tiled
            RunUI(() =>
            {
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ShowSplitChromatogramGraph(true);
            });
            FindNode("R.FKDLGEEHFK.G");

            RunUI(() => SkylineWindow.Size = new Size(1075, 799));
            RestoreViewOnScreen(7);
            PauseForScreenShot("Zoomed split graph panes only", 7);

            RunUI(() => SkylineWindow.AutoZoomNone());
            PauseForScreenShot("Unzoomed split graph panes only", 8);
            
            const int wideWidth = 1547;
            RunUI(() =>
            {
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.ShowGraphPeakArea(true);
                SkylineWindow.ShowChromatogramLegends(false);
                SkylineWindow.ShowPeakAreaLegend(false);
                SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.TOTAL);
                SkylineWindow.SynchronizeZooming(true);
                SkylineWindow.Size = new Size(wideWidth, 855);
            });
            RestoreViewOnScreen(9);
            PauseForScreenShot("Full window", 9);

            FindNode("TCVADESHAGCEK");
            var noteDlg = ShowDialog<EditNoteDlg>(SkylineWindow.EditNote);
            RunUI(() => noteDlg.NoteText = "Lost in yeast samples");

            PauseForScreenShot("Peptide note", 10);
            OkDialog(noteDlg, noteDlg.OkDialog);

            FindNode("NECFLSHKDDSPDLPK");
            RestoreViewOnScreen(11);
            const int narrowWidth = 1350;
            RunUI(() => SkylineWindow.Width = narrowWidth);
            PauseForScreenShot("Yeast chromatograms and RT only - prtsc-paste-edit", 11);
            PauseForScreenShot("Hover over BSA in water chromatogram - prtsc-paste-edit", 12);
            RunUI(() => SkylineWindow.Width = wideWidth);
            RestoreViewOnScreen(13);
            {
                const double clickTime1 = 41.06;
                ClickChromatogram(clickTime1, 1.62E+6, PaneKey.PRECURSORS);
                PauseForScreenShot("Full scan 2D MS1 graph", 13);
                var fullScanGraph = FindOpenForm<GraphFullScan>();
                RunUI(() => fullScanGraph.SetSpectrum(false));
                PauseForScreenShot("Full scan 3D MS1 graph", 13);
                ValidateClickTime(fullScanGraph, clickTime1);

                RunUI(() => fullScanGraph.SetZoom(false));
                PauseForScreenShot("Full scan unzoomed 3D MS1 graph", 14);

                const double clickTime2 = 41.02;
                RunUI(() => fullScanGraph.SetZoom(true));
                ClickChromatogram(clickTime2, 5.8E+4, PaneKey.PRODUCTS);
                PauseForScreenShot("Full scan 3D MS/MS graph", 15);
                ValidateClickTime(fullScanGraph, clickTime2);

                RunUI(() => fullScanGraph.SetZoom(false));
                PauseForScreenShot("Full scan unzoomed 3D MS/MS graph", 15);

                if (IsCoverShotMode)
                {
                    RunUI(() =>
                    {
                        Settings.Default.ChromatogramFontSize = 14;
                        Settings.Default.AreaFontSize = 14;
                        SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    });

                    RestoreCoverViewOnScreen();

                    ClickChromatogram(clickTime2, 5.8E+4, PaneKey.PRODUCTS);

                    var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                    RenameReplicate(manageResultsDlg, 0, "BSA");
                    RenameReplicate(manageResultsDlg, 1, "Yeast_BSA");
                    OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);

                    RunUI(SkylineWindow.FocusDocument);

                    TakeCoverShot();
                    return;
                }

                const double clickTime3 = 41.48;
                ClickChromatogram(yeastReplicateName, clickTime3, 3.14E+4, PaneKey.PRODUCTS);
                PauseForScreenShot("Interference full scan unzoomed 3D MS/MS graph", 16);
                ValidateClickTime(fullScanGraph, clickTime3);

                RunUI(SkylineWindow.HideFullScanGraph);
            }

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.SelectedChromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Skip(1);
                dlg.RemoveReplicates();
                dlg.OkDialog();
            });
            RunUI(() => SkylineWindow.SaveDocument());

            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.IonMobility;
                    transitionSettingsUI.IonMobilityControl.WindowWidthType = IonMobilityWindowWidthCalculator
                        .IonMobilityWindowWidthType.resolving_power;
                    transitionSettingsUI.IonMobilityControl.IonMobilityFilterResolvingPower = 50;
                });
                PauseForScreenShot("Setting ion mobility filter width calculation values", 17);


                var editIonMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsUI.IonMobilityControl.AddIonMobilityLibrary);
                const string libraryName = "BSA";
                var databasePath = TestFilesDirs[1].GetTestPath(libraryName + IonMobilityDb.EXT);

                RunUI(() =>
                {
                    editIonMobilityLibraryDlg.LibraryName = libraryName;
                    editIonMobilityLibraryDlg.CreateDatabaseFile(databasePath); // Simulate user click on Create button
                    editIonMobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(true);
                    editIonMobilityLibraryDlg.GetIonMobilitiesFromResults();

                });
                PauseForScreenShot("Edit ion mobility library form", 18);

                // Check that a new value was calculated for all precursors
                RunUI(() => Assert.AreEqual(SkylineWindow.Document.MoleculeTransitionGroupCount, editIonMobilityLibraryDlg.LibraryMobilitiesFlatCount));

                OkDialog(editIonMobilityLibraryDlg, () => editIonMobilityLibraryDlg.OkDialog());

                PauseForScreenShot<TransitionSettingsUI.IonMobilityTab>("Transition Settings - Ion Mobility", 19);

                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.SetRetentionTimeFilter(RetentionTimeFilterType.scheduling_windows, 3);
                });

                PauseForScreenShot("Transition Settings - Full-Scan", 20);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction);
                PauseForScreenShot("Peptide Settings - Prediction", 21);
                RunUI(() =>
                {
                    Assert.IsTrue(peptideSettingsUI.IsUseMeasuredRT);
                    Assert.AreEqual(6, peptideSettingsUI.TimeWindow);
                });

                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            }

            using (new WaitDocumentChange(1, true, 1000 * 60 * 60 * 5))
            {
                var choosePredictionReplicates = ShowDialog<ChooseSchedulingReplicatesDlg>(SkylineWindow.ImportResults);
                PauseForScreenShot("Choose Replicates form", 22);

                RunUI(() => choosePredictionReplicates.SelectOrDeselectAll(true));
                var importResults = ShowDialog<ImportResultsDlg>(choosePredictionReplicates.OkDialog);
                RunDlg<OpenDataSourceDialog>(importResults.OkDialog, openDataSourceDialog =>
                {
                    openDataSourceDialog.CurrentDirectory = new MsDataFilePath(DataPath);
                    openDataSourceDialog.SelectAllFileType(Yeast_BSA);
                    openDataSourceDialog.Open();
                });
            }
            WaitForGraphs();

            // Test to ensure the filtering happened
            if (!IsPauseForScreenShots) // Don't bring up unexpected UI in a screenshot run
                TestReports();

            RestoreViewOnScreen(11);
            RunUI(() => SkylineWindow.Width = narrowWidth);
            PauseForScreenShot("Yeast chromatogram and RTs - prtsc-paste-edit", 23);
            RunUI(() => SkylineWindow.Width = wideWidth);
            RestoreViewOnScreen(13);
            {
                const double clickTime = 42.20;
                ClickChromatogram(yeastReplicateName, clickTime, 2.904E+4, PaneKey.PRODUCTS);
                var fullScanGraph = FindOpenForm<GraphFullScan>();
                RunUI(() => fullScanGraph.SetZoom(true));
                RunUI(() => fullScanGraph.Parent.Parent.Size = new Size(671, 332));
                PauseForScreenShot("Full-scan graph zoomed", 24);
                RunUI(() => Assert.IsTrue(fullScanGraph.TitleText.Contains(clickTime.ToString(CultureInfo.CurrentCulture))));
                RunUI(SkylineWindow.HideFullScanGraph);
            }

            FindNode("FKDLGEEHFK");

            RunUI(() =>
            {
                SkylineWindow.Size = new Size(1547, 689);
                SkylineWindow.ShowProductTransitions();
                SkylineWindow.ShowPeakAreaLegend(true);
            });
            RestoreViewOnScreen(25);
            PauseForScreenShot("Chromatograms and Peak Areas - prtsc-paste-edit", 25);

            var docFiltered = SkylineWindow.Document;

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageDlg =>
            {
                manageDlg.SelectedChromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Take(1);
                manageDlg.ReimportResults();
                manageDlg.OkDialog();
            });

            WaitForDocumentChangeLoaded(docFiltered, 1000 * 60 * 60 * 5); // 5 minutes

            // TODO: Check peak ranks before and after

            AssertEx.IsFalse(IsRecordMode); // Make sure we turn this off before commit!
        }

        private void VerifyCombinedIonMobility(SrmDocument doc)
        {
            // Check ChromCachedFile
            var cachedFile = doc.MeasuredResults.CachedFileInfos.First();
            VerifyCombinedIonMobilityMoved(cachedFile.FilePath, cachedFile.HasCombinedIonMobility);
            // Check ChromFileInfo.FilePath
            var chromFileInfo = doc.MeasuredResults.Chromatograms[0].MSDataFileInfos[0];
            VerifyCombinedIonMobilityMoved(chromFileInfo.FilePath, true);
        }

        private void VerifyCombinedIonMobilityMoved(MsDataFileUri fileUri, bool hasCombinedIonMobility)
        {
            Assert.IsFalse(((MsDataFilePath)fileUri).LegacyCombineIonMobilitySpectra);
            Assert.IsTrue(hasCombinedIonMobility);
        }

        private static void ValidateClickTime(GraphFullScan fullScanGraph, double clickTime)
        {
            string clickTimeText = clickTime.ToString(CultureInfo.CurrentCulture);
            RunUI(() => Assert.IsTrue(fullScanGraph.TitleText.Contains(clickTimeText),
                String.Format("Full-scan graph title '{0}' does not contain '{1}'", fullScanGraph.TitleText, clickTimeText)));
        }

        private bool IsRecordMode { get { return false; } }

        private void TestReports(string msg = null)
        {
            // Verify reports working for CCS
            var documentGrid = EnableDocumentGridIonMobilityResultsColumns();

            var expectedIM = new[,]
            {
                // Values recorded from master branch - imMS1, imFragment, imWindow
                {26.47, 26.63, 1.06},
                {25.65, 25.65, 1.03},
                {28.75, 28.92, 1.15},
                {28.26, 28.26, 1.13},
                {22.87, 22.87, 0.91},
                {27.77, 27.77, 1.11},
                {24.51, 24.67, 0.98},
                {29.41, 29.57, 1.18},
                {22.22, 22.38, 0.89},
                {25.81, 26.14, 1.03},
                {22.71, 22.87, 0.91},
                {23.36, 23.36, 0.93},
                {27.77, 27.77, 1.11},
                {28.43, 28.59, 1.14},
                {29.41, 27.94, 1.18},
                {24.02, 24.18, 0.96},
                {27.77, 27.94, 1.11},
                {25, 24.83, 1},
                {30.39, 30.55, 1.22}
            };
            double lastMz = -1;
            var colMz = FindDocumentGridColumn(documentGrid, "Precursor.Mz");
            var colFragment = FindDocumentGridColumn(documentGrid, "FragmentIon");
            var precursorIndex = -1;
            for (var row = 0; row < SkylineWindow.Document.MoleculeTransitions.Count(); row++)
            {
                if (IsRecordMode)
                    Console.Write(@"{");
                var isFragment = false;
                RunUI(() =>
                {
                    var mz = (double)documentGrid.DataGridView.Rows[row].Cells[colMz.Index].Value;
                    if (mz != lastMz)
                    {
                        lastMz = mz;
                        precursorIndex++;
                    }
                    var fragmentName = documentGrid.DataGridView.Rows[row].Cells[colFragment.Index].Value.ToString();
                    isFragment = fragmentName.StartsWith("y") || fragmentName.StartsWith("b");
                });

                var unfilteredReplicate = row % 2 == 0;
                var expectedPrecursorIM = unfilteredReplicate ? null : (double?)expectedIM[precursorIndex, 0];
                var expectedFragmentIM = unfilteredReplicate ? null : (double?)expectedIM[precursorIndex, isFragment ? 1 : 0];
                var expectedWindow = unfilteredReplicate ? null : (double?)expectedIM[precursorIndex, 2];
                var expectedUnits = IonMobilityFilter.IonMobilityUnitsL10NString(unfilteredReplicate ? eIonMobilityUnits.none : eIonMobilityUnits.drift_time_msec);
                CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityMS1", row, expectedPrecursorIM, msg, IsRecordMode);
                CheckDocumentResultsGridFieldByName(documentGrid, "TransitionResult.IonMobilityFragment", row, expectedFragmentIM, msg, IsRecordMode); 
                CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityWindow", row, expectedWindow);
                CheckDocumentResultsGridFieldByName(documentGrid, "Chromatogram.ChromatogramIonMobility", row, expectedFragmentIM);
                CheckDocumentResultsGridFieldByName(documentGrid, "Chromatogram.ChromatogramIonMobilityExtractionWidth", row, expectedWindow);
                CheckDocumentResultsGridFieldByName(documentGrid, "Chromatogram.ChromatogramIonMobilityUnits", row, expectedUnits);
                if (IsRecordMode)
                {
                    CheckDocumentResultsGridValuesRecordedCount = 0; // We're managing our own newlines
                    Console.WriteLine(@"},");
                }
            }

            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }
    }
}
