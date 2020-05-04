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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
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

        [TestMethod]
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDriftTimePredictorTutorial()
        {
//            IsPauseForScreenShots = true;
//            RunPerfTests = true;

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/DriftTraining-3_1_1.pdf";
//            LinkPdf = "file:///C:/Users/brend/Downloads/DriftTraining-3_1_1.pdf";

            const string dataRoot = "http://skyline.ms/tutorials/data-drift/";
            TestFilesZipPaths = new[]
            {
                @"http://skyline.ms/tutorials/DriftTimePrediction.zip",
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

            string skyFile = TestFilesDirs[0].GetTestPath(@"DriftTimePrediction\BSA-Training.sky");
            RunUI(() => SkylineWindow.OpenFile(skyFile));

            var document = WaitForDocumentLoaded(240*1000); // 4 minutes
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
                var importResults = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
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
            PauseForScreenShot("Zoomed split graph panes onely", 7);

            RunUI(() => SkylineWindow.AutoZoomNone());
            PauseForScreenShot("Unzoomed split graph panes onely", 8);

            RunUI(() =>
            {
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.ShowGraphPeakArea(true);
                SkylineWindow.ShowChromatogramLegends(false);
                SkylineWindow.ShowPeakAreaLegend(false);
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
                SkylineWindow.SynchronizeZooming(true);
                SkylineWindow.Size = new Size(1547, 855);
            });
            RestoreViewOnScreen(9);
            PauseForScreenShot("Full window", 9);

            FindNode("TCVADESHAGCEK");
            var noteDlg = ShowDialog<EditNoteDlg>(SkylineWindow.EditNote);
            RunUI(() => noteDlg.NoteText = "Lost in yeast samples");

            PauseForScreenShot("Peptide note", 10);
            OkDialog(noteDlg, noteDlg.OkDialog);

            FindNode("NECFLSHKDDSPDLPK");
            PauseForScreenShot("Yeast chromatograms and RT only - prtsc-paste-edit", 11);

            PauseForScreenShot("Hover over BSA in water chromatogram - prtsc-paste-edit", 12);

            RestoreViewOnScreen(13);
            PauseForScreenShot("Full scan 2D MS1 graph", 13);
            {
                const double clickTime1 = 41.06;
                ClickChromatogram(clickTime1, 1.62E+6, PaneKey.PRECURSORS);
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
                PauseForScreenShot("Full scan unzoomed 3D MS/MS graph", 14);

                const double clickTime3 = 41.48;
                ClickChromatogram(yeastReplicateName, clickTime3, 3.14E+4, PaneKey.PRODUCTS);
                PauseForScreenShot("Interference full scan unzoomed 3D MS/MS graph", 15);
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
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction);
                var driftPredictor = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsUI.AddDriftTimePredictor);
                const string predictorName = "BSA";
                RunUI(() =>
                {
                    driftPredictor.SetPredictorName(predictorName);
                    driftPredictor.SetResolvingPower(50);
                    driftPredictor.SetOffsetHighEnergySpectraCheckbox(true);
                    driftPredictor.GetDriftTimesFromResults();
                });
                PauseForScreenShot("Edit predictor form", 18);

                // Check that a new value was calculated for all precursors
                RunUI(() => Assert.AreEqual(SkylineWindow.Document.MoleculeTransitionGroupCount, driftPredictor.Predictor.IonMobilityRows.Count));

                OkDialog(driftPredictor, () => driftPredictor.OkDialog());

                PauseForScreenShot("Peptide Settings - Prediction", 19);

                RunUI(() =>
                {
                    Assert.IsTrue(peptideSettingsUI.IsUseMeasuredRT);
                    Assert.AreEqual(6, peptideSettingsUI.TimeWindow);
                    Assert.AreEqual(predictorName, peptideSettingsUI.SelectedDriftTimePredictor);
                });

                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            }

            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.SetRetentionTimeFilter(RetentionTimeFilterType.scheduling_windows, 3);
                });

                PauseForScreenShot("Transition Settings - Full-Scan", 20);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }

            using (new WaitDocumentChange(1, true, 1000 * 60 * 60 * 5))
            {
                var choosePredictionReplicates = ShowDialog<ChooseSchedulingReplicatesDlg>(SkylineWindow.ImportResults);
                PauseForScreenShot("Choose Replicates form", 21);

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

            // CONSIDER: Test the peak annotations to ensure the filtering happened

            PauseForScreenShot("Yeast chromatogram and RTs - prtsc-paste-edit", 22);

            {
                const double clickTime = 42.20;
                ClickChromatogram(yeastReplicateName, clickTime, 2.904E+4, PaneKey.PRODUCTS);
                var fullScanGraph = FindOpenForm<GraphFullScan>();
                RunUI(() => fullScanGraph.SetZoom(true));
                PauseForScreenShot("Full-scan graph zoomed", 23);
                RunUI(() => Assert.IsTrue(fullScanGraph.TitleText.Contains(clickTime.ToString(CultureInfo.CurrentCulture))));
                RunUI(SkylineWindow.HideFullScanGraph);
            }

            FindNode("FKDLGEEHFK");

            PauseForScreenShot("Chromatograms (copy metafile) and legend - prtsc-paste-edit", 23);

            PauseForScreenShot("Peak area percentages (copy metafile)", 24);

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
            RunUI(() => Assert.IsTrue(fullScanGraph.TitleText.Contains(clickTimeText),
                String.Format("Full-scan graph title '{0}' does not contain '{1}'", fullScanGraph.TitleText, clickTimeText)));
        }
    }
}
