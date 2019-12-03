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

using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
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
            // IsPauseForScreenShots = true;

            ForceMzml = (Program.PauseSeconds == 0);   // Mzml is ~8x faster for this test.
                                                    
            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/AbsoluteQuant-1_4.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.gs.washington.edu/tutorials/AbsoluteQuant.zip"
                    : @"https://skyline.gs.washington.edu/tutorials/AbsoluteQuantMzml.zip",
                @"TestTutorial\AbsoluteQuantViews.zip"
            };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            var dataFolder = UseRawFiles ? "AbsoluteQuant" : "AbsoluteQuantMzml"; // Not L10N
            return TestFilesDirs[0].GetTestPath(Path.Combine(dataFolder, relativePath));
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
            PeptideSettingsUI peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            //PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modification tab", 5);

            var modHeavyK = new StaticMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUi, "Edit Isotope Modification over Transition Settings", 5);
            RunUI(() => peptideSettingsUi.PickedHeavyMods = new[] { modHeavyK.Name });
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modification tab with mod added", 5);

            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            // Inserting a peptide sequence p. 5
            using (new CheckDocumentState(1, 1, 2, 10))
            {
                RunUI(() => SetClipboardText("IEAIPQIDK\tGST-tag"));
                var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
                RunUI(pasteDlg.PastePeptides);
                WaitForProteinMetadataBackgroundLoaderCompletedUI();
                RunUI(() => pasteDlg.Size = new Size(700, 210));
                PauseForScreenShot<PasteDlg.PeptideListTab>("Insert Peptide List", 6);

                OkDialog(pasteDlg, pasteDlg.OkDialog);
            }

            RunUI(SkylineWindow.ExpandPrecursors);
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(folderAbsoluteQuant + @"test_file.sky")));
            WaitForCondition(() => File.Exists(GetTestPath(folderAbsoluteQuant + @"test_file.sky")));
            RunUI( () => SkylineWindow.Size = new Size(840, 410));

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
                    exportMethodDlg.OkDialog(GetTestPath("Quant_Abs_Thermo_TSQ_Vantage.csv")));
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

            RunUI( () => 
                        {   //resize the window and activate the first standard chromatogram pane.
                            RunUI(() => SkylineWindow.Size = new Size(1330, 720));
                            var chrom = SkylineWindow.GraphChromatograms.First();
                            chrom.Select();
                        });

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
            RestoreViewOnScreen(10);
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
            RestoreViewOnScreen(11);
            PauseForScreenShot("Main window with totals graphs for light and heavy and FOXN1-GST", 11);
            
            // Peptide Quantitification Settings p. 11
            peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = (PeptideSettingsUI.TABS)5);
            const string quantUnits = "fmol/ul";
            RunUI(() =>
            {
                peptideSettingsUi.QuantRegressionFit = RegressionFit.LINEAR;
                peptideSettingsUi.QuantNormalizationMethod = new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy);
                peptideSettingsUi.QuantUnits = quantUnits;
            });
            PauseForScreenShot("Peptide Settings Quantification Tab", 12);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            // Specify analyte concentrations of external standards
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGridForm = FindOpenForm<DocumentGridForm>();
            RunUI(() =>
            {
                documentGridForm.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates);
            });
            WaitForConditionUI(() => documentGridForm.IsComplete);
            var concentrations = new[] {40, 12.5, 5, 2.5, 1, .5, .25, .1};
            for (int iRow = 0; iRow < concentrations.Length; iRow++)
            {
                // ReSharper disable AccessToModifiedClosure
                RunUI(() =>
                {
                    var colSampleType = documentGridForm.FindColumn(PropertyPath.Root.Property("SampleType"));
                    documentGridForm.DataGridView.Rows[iRow].Cells[colSampleType.Index].Value = SampleType.STANDARD;
                });
                WaitForConditionUI(() => documentGridForm.IsComplete);
                RunUI(() =>
                {
                    var colAnalyteConcentration =
                        documentGridForm.FindColumn(PropertyPath.Root.Property("AnalyteConcentration"));
                    var cell = documentGridForm.DataGridView.Rows[iRow].Cells[colAnalyteConcentration.Index];
                    documentGridForm.DataGridView.CurrentCell = cell;
                    cell.Value = concentrations[iRow];
                });
                // ReSharper restore AccessToModifiedClosure
                WaitForConditionUI(() => documentGridForm.IsComplete);
            }
            PauseForScreenShot("Document grid with concentrations filled in", 13);

            // View the calibration curve p. 13
            RunUI(()=>SkylineWindow.ShowCalibrationForm());
            var calibrationForm = FindOpenForm<CalibrationForm>();
            PauseForScreenShot("View calibration curve", 14);

            Assert.AreEqual(CalibrationCurveFitter.AppendUnits(QuantificationStrings.Analyte_Concentration, quantUnits), calibrationForm.ZedGraphControl.GraphPane.XAxis.Title.Text);
            Assert.AreEqual(string.Format(QuantificationStrings.CalibrationCurveFitter_PeakAreaRatioText__0___1__Peak_Area_Ratio, IsotopeLabelType.light.Title, IsotopeLabelType.heavy.Title),
                calibrationForm.ZedGraphControl.GraphPane.YAxis.Title.Text);
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
