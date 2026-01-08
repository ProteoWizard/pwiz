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
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.CommonMsData;
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
            // Not yet translated
            if (IsTranslationRequired)
                return;

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
            PauseForScreenShot("Document open - full window");
            AssertEx.IsDocumentState(document, null, 1, 34, 38, 404);

            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(() =>
                    SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.FullScan));

                PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Full-scan settings");

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

                PauseForScreenShot<ImportResultsDlg>("Import results form");

                // Importing raw data from a sample which is a mixture of yeast and BSA

                var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResults.OkDialog);

                RunUI(() =>
                {
                    openDataSourceDialog.CurrentDirectory = new MsDataFilePath(DataPath);
                    openDataSourceDialog.SelectAllFileType(".d");
                });
                PauseForScreenShot<OpenDataSourceDialog>("Import results files");

                OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            }

            string yeastReplicateName = Path.GetFileNameWithoutExtension(Yeast_BSA);
            if (!PauseForAllChromatogramsGraphScreenShot("Importing Results form", 35, "00:01:10", 58f, 1.2e7f,
                new Dictionary<string, int>
                {
                    { "BSA_Frag_100nM_18", 44 },
                    { "Yeast_0pt1ug_BSA_1", 28 }
                }))
                return;
            WaitForDocumentChangeLoaded(document, 1000 * 60 * 60 * 10); // 10 minutes

            string BSAFragName = Path.GetFileNameWithoutExtension(BSA_Frag);
            string YeastName = Path.GetFileNameWithoutExtension(Yeast_BSA);

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
            PauseForScreenShot("Zoomed split graph panes only", null, ClipChromatograms);

            RunUI(() => SkylineWindow.AutoZoomNone());
            PauseForScreenShot("Unzoomed split graph panes only", null, ClipChromatograms);
            
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
            PauseForScreenShot("Full window");

            FindNode("TCVADESHAGCEK");
            var noteDlg = ShowDialog<EditNoteDlg>(SkylineWindow.EditNote);
            RunUI(() => noteDlg.NoteText = "Lost in yeast samples");

            PauseForScreenShot<EditNoteDlg>("Peptide note");
            OkDialog(noteDlg, noteDlg.OkDialog);

            FindNode("NECFLSHKDDSPDLPK");
            RestoreViewOnScreen(11);
            const int narrowWidth = 1350;
            RunUI(() => SkylineWindow.Width = narrowWidth);
            PauseForScreenShot("Yeast chromatograms and RT only - prtsc-paste-edit", null, bmp =>
            {
                bmp = ClipSkylineWindowShotWithForms(bmp, new DockableForm[]
                {
                    SkylineWindow.GetGraphChrom(YeastName),
                    SkylineWindow.GraphRetentionTime
                });
                bmp = bmp.DrawAnnotationTextOnBitmap(new PointF(0.15F, 0.1F), "no\nmonoisotopic\nprecursor");
                bmp = bmp.DrawArrowOnBitmap(new PointF(0.29F, 0.22F), new PointF(0.312F, 0.32F),3, 10);
                bmp = bmp.Expand(right: 0.15F);
                bmp = bmp.DrawAnnotationTextOnBitmap(new PointF(0.875F, 0.488F), "yeast\nbefore\nwater");
                bmp = bmp.DrawVerticalBackwardBracket(new PointF(0.855F, 0.488F), 0.12F, 0.008F, 3);
                return bmp;
            });
            const double clickTime1 = 41.06;
            const double clickIntensity = 1.62E+6;
            if (IsPauseForScreenShots)
                MouseOverChromatogram(BSAFragName, clickTime1, clickIntensity, PaneKey.PRECURSORS);

            var graphChrom = SkylineWindow.GetGraphChrom(BSAFragName);
            PauseForScreenShot(graphChrom,"Hover over BSA in water chromatogram - prtsc-paste-edit", null, bmp =>
                ClipBitmap(DrawHandCursorOnChromBitmap(bmp, graphChrom, true, clickTime1, clickIntensity, PaneKey.PRECURSORS), 
                    new Rectangle(0, 0, bmp.Width, (int)(bmp.Height * 0.515))));

            RunUI(() => SkylineWindow.Width = wideWidth);
            RestoreViewOnScreen(13);
            {
                ClickChromatogram(BSAFragName, clickTime1, clickIntensity, PaneKey.PRECURSORS);
                PauseForFullScanGraphScreenShot("Full scan 2D MS1 graph");
                var fullScanGraph = FindOpenForm<GraphFullScan>();
                RunUI(() => fullScanGraph.SetSpectrum(false));
                PauseForFullScanGraphScreenShot("Full scan 3D MS1 graph");
                ValidateClickTime(fullScanGraph, clickTime1);

                RunUI(() => fullScanGraph.SetZoom(false));
                PauseForFullScanGraphScreenShot("Full scan unzoomed 3D MS1 graph");

                const double clickTime2 = 41.02;
                RunUI(() => fullScanGraph.SetZoom(true));
                ClickChromatogram(clickTime2, 5.8E+4, PaneKey.PRODUCTS);
                PauseForFullScanGraphScreenShot("Full scan 3D MS/MS graph");
                ValidateClickTime(fullScanGraph, clickTime2);

                RunUI(() => fullScanGraph.SetZoom(false));
                PauseForFullScanGraphScreenShot("Full scan unzoomed 3D MS/MS graph");

                if (IsCoverShotMode)
                {
                    Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.none.ToString();

                    RunUI(() =>
                    {
                        Settings.Default.ChromatogramFontSize = 14;
                        Settings.Default.AreaFontSize = 14;
                        SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    });

                    RestoreCoverViewOnScreen();

                    var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                    RenameReplicate(manageResultsDlg, 0, "BSA");
                    RenameReplicate(manageResultsDlg, 1, "Yeast_BSA");
                    OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);

                    ClickChromatogram(clickTime2, 5.8E+4, PaneKey.PRODUCTS);

                    fullScanGraph = FindOpenForm<GraphFullScan>();
                    RunUI(() =>
                    {
                        var frame = fullScanGraph.Parent.Parent;
                        frame.Location = new Point(SkylineWindow.Left + 10, SkylineWindow.Bottom - frame.Height - 10);
                    });
                    FocusDocument();

                    TakeCoverShot();
                    return;
                }

                const double clickTime3 = 41.48;
                ClickChromatogram(yeastReplicateName, clickTime3, 3.14E+4, PaneKey.PRODUCTS);
                PauseForFullScanGraphScreenShot("Interference full scan unzoomed 3D MS/MS graph");
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
                PauseForScreenShot<TransitionSettingsUI.IonMobilityTab>("Setting ion mobility filter width calculation values");


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
                PauseForScreenShot<EditIonMobilityLibraryDlg>("Edit ion mobility library form");

                // Check that a new value was calculated for all precursors
                RunUI(() => Assert.AreEqual(SkylineWindow.Document.MoleculeTransitionGroupCount, editIonMobilityLibraryDlg.LibraryMobilitiesFlatCount));

                OkDialog(editIonMobilityLibraryDlg, () => editIonMobilityLibraryDlg.OkDialog());

                PauseForScreenShot<TransitionSettingsUI.IonMobilityTab>("Transition Settings - Ion Mobility");

                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.SetRetentionTimeFilter(RetentionTimeFilterType.scheduling_windows, 3);
                });

                PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan");

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction);
                PauseForScreenShot<PeptideSettingsUI.PredictionTab>("Peptide Settings - Prediction");
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
                PauseForScreenShot<ChooseSchedulingReplicatesDlg>("Choose Replicates form");

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
            if (IsPauseForScreenShots)
            {
                RunUI(() =>
                {
                    var graphChromYeast = SkylineWindow.GetGraphChrom(YeastName);
                    var panePrec = graphChromYeast.GetGraphPane(PaneKey.PRECURSORS);
                    var paneProd = graphChromYeast.GetGraphPane(PaneKey.PRODUCTS);
                    panePrec.XAxis.Scale.Min = paneProd.XAxis.Scale.Min = 39.8;
                    panePrec.XAxis.Scale.Max = paneProd.XAxis.Scale.Max = 44.2;
                });
            }
            PauseForScreenShot("Yeast chromatogram and RTs - prtsc-paste-edit", null, bmp =>
                ClipSkylineWindowShotWithForms(bmp, new DockableForm[]
                {
                    SkylineWindow.GetGraphChrom(Path.GetFileNameWithoutExtension(Yeast_BSA)),
                    SkylineWindow.GraphRetentionTime
                }));
            RunUI(() => SkylineWindow.Width = wideWidth);
            RestoreViewOnScreen(13);
            {
                const double clickTime = 42.20;
                ClickChromatogram(yeastReplicateName, clickTime, 2.904E+4, PaneKey.PRODUCTS);
                var fullScanGraph = FindOpenForm<GraphFullScan>();
                RunUI(() => fullScanGraph.SetZoom(true));
                RunUI(() => fullScanGraph.Parent.Parent.Size = new Size(671, 332));
                PauseForFullScanGraphScreenShot("Full-scan graph zoomed");
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
            PauseForScreenShot("Chromatograms and Peak Areas - prtsc-paste-edit", null, bmp =>
                ClipSkylineWindowShotWithForms(bmp, new DockableForm[]
                {
                    SkylineWindow.GetGraphChrom(BSAFragName),
                    SkylineWindow.GetGraphChrom(YeastName),
                    SkylineWindow.GraphPeakArea
                }));

            var docFiltered = SkylineWindow.Document;

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageDlg =>
            {
                manageDlg.SelectedChromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Take(1);
                manageDlg.ReimportResults();
                manageDlg.OkDialog();
            });

            WaitForDocumentChangeLoaded(docFiltered, 1000 * 60 * 60 * 5); // 5 minutes

            // TODO: Check peak ranks before and after
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
            WaitForConditionUI(() => fullScanGraph.TitleText.Contains(clickTimeText),
                String.Format("Full-scan graph title '{0}' does not contain '{1}'", fullScanGraph.TitleText, clickTimeText));
        }

        protected override bool IsRecordMode => false;

        private void TestReports(string msg = null)
        {
            // Verify reports working for CCS
            var documentGrid = EnableDocumentGridIonMobilityResultsColumns();

            var expectedIM = new[,]
            {
                // Values recorded with IsRecordMode = true - imMS1, imFragment, imWindow
                // These utilize the isotope envelope matching code (new as of Jan 2024)
                {26.47, 26.3, 1.06},
                {25.65, 25.65, 1.03},
                {28.75, 28.75, 1.15},
                {28.26, 28.1, 1.13},
                {22.87, 22.87, 0.91},
                {27.77, 27.77, 1.11},
                {24.51, 24.51, 0.98},
                {29.41, 29.24, 1.18},
                {22.22, 22.22, 0.89},
                {25.81, 25.81, 1.03},
                {23.04, 22.55, 0.92},
                {23.36, 23.36, 0.93},
                {27.77, 27.28, 1.11},
                {28.92, 28.43, 1.16},
                {29.41, 27.61, 1.18},
                {24.02, 23.85, 0.96},
                {27.61, 26.63, 1.1},
                {25, 24.83, 1},
                {30.39, 30.39, 1.22}
            };
            double lastMz = -1;
            var colMz = FindDocumentGridColumn(documentGrid, "Precursor.Mz");
            var colFragment = FindDocumentGridColumn(documentGrid, "FragmentIon");
            var precursorIndex = -1;
            var precursorIndexLastRecorded = -1;
            for (var row = 0; row < SkylineWindow.Document.MoleculeTransitions.Count(); row++)
            {
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
                var recordPrecursorValues = IsRecordMode && precursorIndex != precursorIndexLastRecorded && isFragment && !unfilteredReplicate;
                if (recordPrecursorValues || !IsRecordMode)
                {
                    if (recordPrecursorValues)
                    {
                        Console.Write(@"{");
                    }
                    CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityMS1", row, expectedPrecursorIM, msg, recordPrecursorValues);
                    CheckDocumentResultsGridFieldByName(documentGrid, "TransitionResult.IonMobilityFragment", row, expectedFragmentIM, msg, recordPrecursorValues);
                    CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityWindow", row, expectedWindow, msg, recordPrecursorValues);
                    if (recordPrecursorValues)
                    {
                        Console.WriteLine(@"},");
                        CheckDocumentResultsGridValuesRecordedCount = 0; // We're managing our own newlines
                        precursorIndexLastRecorded = precursorIndex;
                    }
                }
                if (!IsRecordMode)
                {
                    CheckDocumentResultsGridFieldByName(documentGrid, "Chromatogram.ChromatogramIonMobility", row, expectedFragmentIM);
                    CheckDocumentResultsGridFieldByName(documentGrid, "Chromatogram.ChromatogramIonMobilityExtractionWidth", row, expectedWindow);
                    CheckDocumentResultsGridFieldByName(documentGrid, "Chromatogram.ChromatogramIonMobilityUnits", row, expectedUnits);
                }
            }
            if (IsRecordMode)
                PauseForManualTutorialStep("see console for new values");
            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }
    }
}
