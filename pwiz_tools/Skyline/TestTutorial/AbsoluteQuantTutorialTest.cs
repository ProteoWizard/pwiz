/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;


namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class AbsoluteQuantTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAbsoluteQuantificationTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            ForceMzml = true;   // Mzml is ~8x faster for this test.

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/AbsoluteQuant-1_4.pdf";

            TestFilesZip = UseRawFiles
                               ? @"https://skyline.gs.washington.edu/tutorials/AbsoluteQuant.zip"
                               : @"https://skyline.gs.washington.edu/tutorials/AbsoluteQuantMzml.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var folderAbsoluteQuant = UseRawFiles ? "AbsoluteQuant" : "AbsoluteQuantMzml";
            // Generating a Transition List, p. 4
            {
                var doc = SkylineWindow.Document;
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                          {
                              // Predicition Settings
                              transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Prediction;
                              transitionSettingsUI.PrecursorMassType = MassType.Monoisotopic;
                              transitionSettingsUI.FragmentMassType = MassType.Monoisotopic;
                              transitionSettingsUI.RegressionCEName = "Thermo TSQ Vantage";
                              transitionSettingsUI.RegressionDPName = Resources.SettingsList_ELEMENT_NONE_None;
                          });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Prediction tab", 4);

                RunUI(() =>
                          {
                              // Filter Settings
                              transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                              transitionSettingsUI.PrecursorCharges = "2";
                              transitionSettingsUI.ProductCharges = "1";
                              transitionSettingsUI.FragmentTypes = "y";
                              transitionSettingsUI.RangeFrom = Resources.TransitionFilter_FragmentStartFinders_ion_3;
                              transitionSettingsUI.RangeTo = Resources.TransitionFilter_FragmentEndFinders_last_ion_minus_1;
                              transitionSettingsUI.SpecialIons = new string[0];
                          });
                PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition Settings - Filter tab", 4);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                WaitForDocumentChange(doc);
            }

            // Configuring Peptide settings p. 4
            PeptideSettingsUI peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modification tab", 5);

            var modHeavyK = new StaticMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUI, "Edit Isotope Modification over Transition Settings", 5);
            RunUI(() => peptideSettingsUI.PickedHeavyMods = new[] { modHeavyK.Name });
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modification tab with mod added", 5);

            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            // Inserting a peptide sequence p. 5
            using (new CheckDocumentState(1, 1, 2, 10))
            {
                RunUI(() => SetClipboardText("IEAIPQIDK\tGST-tag"));
                var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
                RunUI(pasteDlg.PastePeptides);
                WaitForProteinMetadataBackgroundLoaderCompletedUI();
                PauseForScreenShot<PasteDlg.PeptideListTab>("Insert Peptide List", 6);

                OkDialog(pasteDlg, pasteDlg.OkDialog);
            }

            RunUI(SkylineWindow.ExpandPrecursors);
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(folderAbsoluteQuant + @"test_file.sky")));
            WaitForCondition(() => File.Exists(TestFilesDir.GetTestPath(folderAbsoluteQuant + @"test_file.sky")));
            PauseForScreenShot("Main window with Targets view", 6);

            // Exporting a transition list p. 6
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                          {
                              exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO;
                              exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                              exportMethodDlg.OptimizeType = ExportOptimize.NONE;
                              exportMethodDlg.MethodType = ExportMethodType.Standard;
                          });
                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List", 7);

                OkDialog(exportMethodDlg, () =>
                    exportMethodDlg.OkDialog(TestFilesDir.GetTestPath("Quant_Abs_Thermo_TSQ_Vantage.csv")));
            }

            // Importing RAW files into Skyline p. 7
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            PauseForScreenShot<ImportResultsDlg>("Import Results - click OK to get shot of Import Results Files and then cancel", 8);

            RunUI(() =>
            {
                var rawFiles = DataSourceUtil.GetDataSources(TestFilesDirs[0].FullPath).First().Value.Skip(1);
                var namedPathSets = from rawFile in rawFiles
                                    select new KeyValuePair<string, MsDataFileUri[]>(
                                        rawFile.GetFileNameWithoutExtension(), new[] { rawFile });
                importResultsDlg.NamedPathSets = namedPathSets.ToArray();
            });
            RunDlg<ImportResultsNameDlg>(importResultsDlg.OkDialog,
               importResultsNameDlg => importResultsNameDlg.NoDialog());

            WaitForGraphs();

            RunUI(() =>
                      {
                          SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                          Settings.Default.ArrangeGraphsOrder = GroupGraphsOrder.Document.ToString();
                          Settings.Default.ArrangeGraphsReversed = false;
                          SkylineWindow.ArrangeGraphsTiled();
                          SkylineWindow.AutoZoomBestPeak();
                      });
            WaitForCondition(() => Equals(8, SkylineWindow.GraphChromatograms.Count(graphChrom => !graphChrom.IsHidden)),
                "unexpected visible graphChromatogram count");

            WaitForCondition(10 * 60 * 1000,    // ten minutes
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            PauseForScreenShot("Main window with imported data", 9);

            // Analyzing SRM Data from FOXN1-GST Sample p. 9
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults,
                importResultsDlg1 =>
                {
                    var rawFiles = DataSourceUtil.GetDataSources(TestFilesDirs[0].FullPath).First().Value.Take(1);
                    var namedPathSets = from rawFile in rawFiles
                                        select new KeyValuePair<string, MsDataFileUri[]>(
                                            rawFile.GetFileNameWithoutExtension(), new[] { rawFile });
                    importResultsDlg1.NamedPathSets = namedPathSets.ToArray();
                    importResultsDlg1.OkDialog();
                });
            WaitForGraphs();
            CheckReportCompatibility.CheckAll(SkylineWindow.Document);
            WaitForCondition(5 * 60 * 1000, // five minutes
                             () =>
                             SkylineWindow.Document.Settings.HasResults &&
                             SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);

            RunUI(() =>
            {
                SkylineWindow.ToggleIntegrateAll();
                SkylineWindow.ArrangeGraphsTabbed();
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.ShowPeakAreaReplicateComparison();
                // Total normalization
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
            });

            RunUI(() => SkylineWindow.ActivateReplicate("FOXN1-GST"));
            WaitForGraphs();
            RunUI(() => SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0));
            WaitForGraphs();
            RunUI(() =>
                      {
                          Assert.AreEqual(SkylineWindow.SelectedResultsIndex, SkylineWindow.GraphPeakArea.ResultsIndex);
                          Assert.AreEqual(SkylineWindow.SelectedResultsIndex, SkylineWindow.GraphRetentionTime.ResultsIndex);
                      });

            RunUI(() =>
            {
                int transitionCount = SkylineWindow.DocumentUI.PeptideTransitionGroups.First().TransitionCount;
                CheckGstGraphs(transitionCount, transitionCount);
            });
            PauseForScreenShot("Main window with Peak Areas, Retention Times and FOXN1-GST for light", 10);

            RunUI(() => SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.TransitionGroups, 1));
            WaitForGraphs();

            RunUI(() =>
            {
                int transitionCount = SkylineWindow.DocumentUI.PeptideTransitionGroups.ToArray()[1].TransitionCount;
                CheckGstGraphs(transitionCount, transitionCount);
            });
            PauseForScreenShot("Main window with Peak Areas, Retention Times and FOXN1-GST for heavy", 10);

            RunUI(() => SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.Molecules, 0));
            WaitForGraphs();
            // Heavy normalization
            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_ratio_view));
            WaitForGraphs();

            RunUI(() =>
            {
                int transitionGroupCount = SkylineWindow.DocumentUI.Peptides.First().TransitionGroupCount;
                CheckGstGraphs(transitionGroupCount, transitionGroupCount - 1);
            });
            PauseForScreenShot("Main window with totals graphs for light and heavy and FOXN1-GST", 11);
            const int columnsToAddCount = 4;
            var columnSeparator = TextUtil.CsvSeparator;
            // Generating a Calibration Curve p. 11
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            var editReportListDlg = ShowDialog<ManageViewsForm>(exportLiveReportDlg.EditList);
            const string reportName = "Peptide Ratio Results Test";
            var columnsToAdd = new[]
                                {
                                    PropertyPath.Parse("Proteins!*.Peptides!*.Sequence"),
                                    PropertyPath.Parse("Proteins!*.Name"),
                                    PropertyPath.Parse("Replicates!*.Name"),
                                    PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value.RatioToStandard"),
                                };
            Assert.AreEqual(columnsToAddCount, columnsToAdd.Length);
            {
                var viewEditor = ShowDialog<ViewEditor>(editReportListDlg.AddView);
                RunUI(() =>
                {
                    viewEditor.ViewName = reportName;
                    foreach (var id in columnsToAdd)
                    {
                        Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(id), "Unable to select {0}", id);
                        viewEditor.ChooseColumnsTab.AddSelectedColumn();
                    }
                    Assert.AreEqual(columnsToAdd.Length, viewEditor.ChooseColumnsTab.ColumnCount);
                });
                // TODO: MultiViewProvider not yet supported in Common
                PauseForScreenShot<ViewEditor>("Edit Report form", 12);

                OkDialog(viewEditor, viewEditor.OkDialog);
            }

            RunUI(editReportListDlg.OkDialog);
            WaitForClosedForm(editReportListDlg);
            RunUI(() =>
            {
                exportLiveReportDlg.ReportName = reportName;
                exportLiveReportDlg.OkDialog(TestFilesDir.GetTestPath("Calibration.csv"), columnSeparator);
            });

            // Check if export file is correct. 
            string filePath = TestFilesDir.GetTestPath("Calibration.csv");
            Assert.IsTrue(File.Exists(filePath));
            string[] lines = File.ReadAllLines(filePath);
            string[] line0 = lines[0].Split(columnSeparator);
            int count = line0.Length;
            Assert.IsTrue(lines.Count() == SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count + 1);
            Assert.AreEqual(columnsToAddCount, count);

            // Check export file data
            double ratio1 = Double.Parse(lines[1].Split(new[] { columnSeparator }, 4)[3]);
            double ratio2 = Double.Parse(lines[2].Split(new[] { columnSeparator }, 4)[3]);
            double ratio3 = Double.Parse(lines[3].Split(new[] { columnSeparator }, 4)[3]);
            double ratio4 = Double.Parse(lines[4].Split(new[] { columnSeparator }, 4)[3]);
            double ratio5 = Double.Parse(lines[5].Split(new[] { columnSeparator }, 4)[3]);
            double ratio6 = Double.Parse(lines[6].Split(new[] { columnSeparator }, 4)[3]);
            double ratio7 = Double.Parse(lines[7].Split(new[] { columnSeparator }, 4)[3]);
            double ratio8 = Double.Parse(lines[8].Split(new[] { columnSeparator }, 4)[3]);
            double ratio9 = Double.Parse(lines[9].Split(new[] { columnSeparator }, 4)[3]);

            Assert.AreEqual(21.4513, ratio1, 0.1);
            Assert.AreEqual(6.2568, ratio2, 0.1);
            Assert.AreEqual(2.0417, ratio3, 0.1);
            Assert.AreEqual(0.8244, ratio4, 0.1);
            Assert.AreEqual(0.2809, ratio5, 0.1);
            Assert.AreEqual(0.1156, ratio6, 0.1);
            Assert.AreEqual(0.0819, ratio7, 0.1);
            Assert.AreEqual(0.0248, ratio8, 0.1);
            Assert.AreEqual(0.7079, ratio9, 0.1);
            CheckReportCompatibility.CheckAll(SkylineWindow.Document);
        }

        private static void CheckGstGraphs(int rtCurveCount, int areaCurveCount)
        {
            var graphChrom = SkylineWindow.GetGraphChrom("FOXN1-GST");
            Assert.IsNotNull(graphChrom);
            Assert.IsTrue(graphChrom.BestPeakTime.HasValue);
            Assert.AreEqual(20.9, graphChrom.BestPeakTime.Value, 0.05);
            Assert.AreEqual(rtCurveCount, SkylineWindow.RTGraphController.GraphSummary.CurveCount);
            Assert.AreEqual(9, SkylineWindow.RTGraphController.GraphSummary.Categories.Count());
            Assert.AreEqual(areaCurveCount, SkylineWindow.GraphPeakArea.Controller.GraphSummary.CurveCount);
            Assert.AreEqual(9, SkylineWindow.GraphPeakArea.Controller.GraphSummary.Categories.Count());
        }
    }
}
