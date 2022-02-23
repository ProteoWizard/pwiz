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
using pwiz.Skyline.Controls;
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
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "AbsoluteQuant";

            ForceMzml = true;   // Mzml is ~8x faster for this test.
                                                    
            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/AbsoluteQuant-20_1.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.ms/tutorials/AbsoluteQuant.zip"
                    : @"https://skyline.ms/tutorials/AbsoluteQuantMzml.zip",
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
            Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.none.ToString();
            // Generating a Transition List, p. 5, 6
            {
                var doc = SkylineWindow.Document;
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                          {
                              // Prediction Settings
                              transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Prediction;
                              transitionSettingsUI.PrecursorMassType = MassType.Monoisotopic;
                              transitionSettingsUI.FragmentMassType = MassType.Monoisotopic;
                              transitionSettingsUI.RegressionCEName = "Thermo TSQ Vantage";
                              transitionSettingsUI.RegressionDPName = Resources.SettingsList_ELEMENT_NONE_None;
                          });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Prediction tab", 5);

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
                PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition Settings - Filter tab", 6);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                WaitForDocumentChange(doc);
            }

            // Configuring Peptide settings p. 7, 8
            PeptideSettingsUI peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications);

            var modHeavyK = new StaticMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, false, null,
                LabelAtoms.C13 | LabelAtoms.N15, RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUi, "Edit Isotope Modification over Transition Settings", 7);
            RunUI(() => peptideSettingsUi.PickedHeavyMods = new[] { modHeavyK.Name });
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modification tab with mod added", 8);

            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            // Inserting a peptide sequence p. 9
            using (new CheckDocumentState(1, 1, 2, 10))
            {
                RunUI(() => SetClipboardText("IEAIPQIDK\tGST-tag"));
                var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
                RunUI(pasteDlg.PastePeptides);
                WaitForProteinMetadataBackgroundLoaderCompletedUI();
                RunUI(() => pasteDlg.Size = new Size(700, 210));
                PauseForScreenShot<PasteDlg.PeptideListTab>("Insert Peptide List", 9);

                OkDialog(pasteDlg, pasteDlg.OkDialog);
            }

            RunUI(SkylineWindow.ExpandPrecursors);
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(folderAbsoluteQuant + @"test_file.sky")));
            WaitForCondition(() => File.Exists(GetTestPath(folderAbsoluteQuant + @"test_file.sky")));
            RunUI( () => SkylineWindow.Size = new Size(840, 410));

            PauseForScreenShot("Main window with Targets view", 9);

            // Exporting a transition list p. 10
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                          {
                              exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO;
                              exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                              exportMethodDlg.OptimizeType = ExportOptimize.NONE;
                              exportMethodDlg.MethodType = ExportMethodType.Standard;
                          });
                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List", 10);

                OkDialog(exportMethodDlg, () =>
                    exportMethodDlg.OkDialog(GetTestPath("Quant_Abs_Thermo_TSQ_Vantage.csv")));
            }

            // Importing RAW files into Skyline p. 11, 12
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            PauseForScreenShot<ImportResultsDlg>("Import Results - click OK to get shot of Import Results Files and then cancel", 11);

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
            PauseForScreenShot("Main window with imported data", 13);

            // Analyzing SRM Data from FOXN1-GST Sample p. 14
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
                SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.TOTAL);
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
            RunUI(() => SkylineWindow.Size = new Size(1470, 656));
            RestoreViewOnScreen(14);
            PauseForScreenShot("Main window with Peak Areas, Retention Times and FOXN1-GST for light", 14);

            RunUI(() => SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.TransitionGroups, 1));
            WaitForGraphs();

            RunUI(() =>
            {
                int transitionCount = SkylineWindow.DocumentUI.PeptideTransitionGroups.ToArray()[1].TransitionCount;
                CheckGstGraphs(transitionCount, transitionCount);
            });
            PauseForScreenShot("Main window with Peak Areas, Retention Times and FOXN1-GST for heavy", 14);

            RunUI(() => SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.Molecules, 0));
            WaitForGraphs();
            // Heavy normalization
            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.FromIsotopeLabelType(IsotopeLabelType.heavy)));
            WaitForGraphs();
            RunUI(() =>
            {
                int transitionGroupCount = SkylineWindow.DocumentUI.Peptides.First().TransitionGroupCount;
                CheckGstGraphs(transitionGroupCount, transitionGroupCount - 1);
            });
            PauseForScreenShot("Main window with totals graphs for light and heavy and FOXN1-GST", 15);
            
            // Peptide Quantitification Settings p. 16
            peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = (PeptideSettingsUI.TABS)5);
            const string quantUnits = "fmol/ul";
            RunUI(() =>
            {
                peptideSettingsUi.QuantRegressionFit = RegressionFit.LINEAR;
                peptideSettingsUi.QuantNormalizationMethod = new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy);
                peptideSettingsUi.QuantUnits = quantUnits;
            });
            PauseForScreenShot("Peptide Settings Quantification Tab", 16);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            // Specify analyte concentrations of external standards
            var documentGridForm = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
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

            if (IsPauseForScreenShots)
            {
                RunUI(() =>
                {
                    SkylineWindow.Width = 500;
                    var gridFloatingWindow = documentGridForm.Parent.Parent;
                    gridFloatingWindow.Size = new Size(370, 315);
                    gridFloatingWindow.Top = SkylineWindow.Top;
                    gridFloatingWindow.Left = SkylineWindow.Right + 20;
                });
                PauseForScreenShot("Document grid with concentrations filled in", 17);
            }

            // View the calibration curve p. 18
            RunUI(() => SkylineWindow.ShowDocumentGrid(false));

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 14;
                    Settings.Default.AreaFontSize = 14;
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    SkylineWindow.ShowPeakAreaLegend(false);
                    SkylineWindow.ShowRTLegend(false);
                });
                RestoreCoverViewOnScreen();
                RunUI(() =>
                {
                    var calibrationOpenForm = WaitForOpenForm<CalibrationForm>();
                    var calibrationFloatingWindow = calibrationOpenForm.Parent.Parent;
                    calibrationFloatingWindow.Top = SkylineWindow.Bottom - calibrationFloatingWindow.Height - 35;
                    calibrationFloatingWindow.Left = SkylineWindow.Left + 15;

                });
                TakeCoverShot();
                return;
            }

            var calibrationForm = ShowDialog<CalibrationForm>(() => SkylineWindow.ShowCalibrationForm());
            if (IsPauseForScreenShots)
            {
                RunUI(() =>
                {
                    var calibrationFloatingWindow = calibrationForm.Parent.Parent;
                    calibrationFloatingWindow.Width = 565;
                    calibrationFloatingWindow.Top = SkylineWindow.Top;
                    calibrationFloatingWindow.Left = SkylineWindow.Right + 20;
                });
                PauseForScreenShot("View calibration curve", 18);
            }

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
