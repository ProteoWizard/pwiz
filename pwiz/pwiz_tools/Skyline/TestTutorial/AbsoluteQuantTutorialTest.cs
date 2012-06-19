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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
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

            TestFilesZip = ExtensionTestContext.CanImportThermoRaw
                               ? @"https://skyline.gs.washington.edu/tutorials/AbsoluteQuant.zip"
                               : @"https://skyline.gs.washington.edu/tutorials/AbsoluteQuantMzml.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var folderAbsoluteQuant = ExtensionTestContext.CanImportThermoRaw ? "AbsoluteQuant" : "AbsoluteQuantMzml";
            // Generating a Transition List, p. 4
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI,
                                         transitionSettingsUI =>
                                         {
                                             // Predicition Settings
                                             transitionSettingsUI.PrecursorMassType = MassType.Monoisotopic;
                                             transitionSettingsUI.FragmentMassType = MassType.Monoisotopic;
                                             transitionSettingsUI.RegressionCEName = "Thermo TSQ Vantage";
                                             transitionSettingsUI.RegressionDPName = "None";

                                             // Filter Settings
                                             transitionSettingsUI.PrecursorCharges = "2";
                                             transitionSettingsUI.ProductCharges = "1";
                                             transitionSettingsUI.FragmentTypes = "y";
                                             transitionSettingsUI.RangeFrom = "ion 3";
                                             transitionSettingsUI.RangeTo = "last ion - 1";
                                             transitionSettingsUI.OkDialog();
                                         });

            // Configuring Peptide settings p. 4
            PeptideSettingsUI peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var modHeavyK = new StaticMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUI);
            RunUI(() => peptideSettingsUI.PickedHeavyMods = new[] { modHeavyK.Name });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            // Inserting a peptide sequence p. 5
            RunUI(() => SetClipboardText("IEAIPQIDK\tGST-tag"));
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg,
                             pasteDlg =>
                             {
                                 pasteDlg.PastePeptides();
                                 pasteDlg.OkDialog();
                             });

            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 2, 10);

            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(folderAbsoluteQuant + @"test_file.sky")));
            WaitForCondition(() => File.Exists(TestFilesDir.GetTestPath(folderAbsoluteQuant + @"test_file.sky")));

            // Exporting a transition list p. 6
            // TODO: Export name never specified in tutorial.
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List),
                exportMethodDlg =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO;
                    exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                    exportMethodDlg.OptimizeType = ExportOptimize.NONE;
                    exportMethodDlg.MethodType = ExportMethodType.Standard;
                    exportMethodDlg.OkDialog(
                        TestFilesDir.GetTestPath("Quant_Abs_Thermo_TSQ_Vantage.csv"));
                });

            // Importing RAW files into Skyline p. 7
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                var rawFiles = DataSourceUtil.GetDataSources(TestFilesDirs[0].FullPath).First().Value.Skip(1);
                var namedPathSets = from rawFile in rawFiles
                                    select new KeyValuePair<string, string[]>(
                                        Path.GetFileNameWithoutExtension(rawFile), new[] { rawFile });
                importResultsDlg.NamedPathSets = namedPathSets.ToArray();
            });
            RunDlg<ImportResultsNameDlg>(importResultsDlg.OkDialog,
               importResultsNameDlg => importResultsNameDlg.NoDialog());

            WaitForGraphs();

            RunUI(() => SkylineWindow.ArrangeGraphsTiled());
            Assert.AreEqual(8, SkylineWindow.GraphChromatograms.Count(graphChrom => !graphChrom.IsHidden));

            WaitForCondition(10 * 60 * 1000,    // ten minutes
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);


            // Analyzing SRM Data from FOXN1-GST Sample p. 9
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults,
                importResultsDlg1 =>
                {
                    var rawFiles = DataSourceUtil.GetDataSources(TestFilesDirs[0].FullPath).First().Value.Take(1);
                    var namedPathSets = from rawFile in rawFiles
                                        select new KeyValuePair<string, string[]>(
                                            Path.GetFileNameWithoutExtension(rawFile), new[] { rawFile });
                    importResultsDlg1.NamedPathSets = namedPathSets.ToArray();
                    importResultsDlg1.OkDialog();
                });
            WaitForGraphs();

            WaitForCondition(5 * 60 * 1000, // five minutes
                             () =>
                             SkylineWindow.Document.Settings.HasResults &&
                             SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);

            RunUI(() =>
            {
                SkylineWindow.IntegrateAll();
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
                int transitionCount = SkylineWindow.DocumentUI.TransitionGroups.First().TransitionCount;
                CheckGstGraphs(transitionCount, transitionCount);
            });

            RunUI(() => SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.TransitionGroups, 1));
            WaitForGraphs();

            RunUI(() =>
            {
                int transitionCount = SkylineWindow.DocumentUI.TransitionGroups.ToArray()[1].TransitionCount;
                CheckGstGraphs(transitionCount, transitionCount);
            });

            RunUI(() => SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.Peptides, 0));
            WaitForGraphs();
            // Heavy normalization
            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_ratio_view));
            WaitForGraphs();

            RunUI(() =>
            {
                int transitionGroupCount = SkylineWindow.DocumentUI.Peptides.First().TransitionGroupCount;
                CheckGstGraphs(transitionGroupCount, transitionGroupCount - 1);
            });

            // Generating a Calibration Curve p. 11
            var exportReportDlg = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
            var editReportListDlg =
                ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg.EditList);
            const string reportName = "Peptide Ratio Results Test";
            var columnsToAdd = new[]
                                   {
                                       new Identifier("Peptides", "Sequence"),
                                       new Identifier("ProteinName"),
                                       new Identifier("Results", "ReplicateName"),
                                       new Identifier("Peptides", "PeptideResults", "RatioToStandard")
                                   };
            RunDlg<PivotReportDlg>(editReportListDlg.AddItem, pivotReportDlg =>
            {
                pivotReportDlg.ReportName = reportName;
                foreach (Identifier id in columnsToAdd)
                {
                    Assert.IsTrue(pivotReportDlg.TrySelect(id));
                    pivotReportDlg.AddSelectedColumn();
                }
                Assert.AreEqual(columnsToAdd.Length, pivotReportDlg.ColumnCount);
                pivotReportDlg.OkDialog();
            });

            RunUI(editReportListDlg.OkDialog);
            WaitForClosedForm(editReportListDlg);

            var columnSeparator = TextUtil.CsvSeparator;
            RunUI(() =>
            {
                exportReportDlg.ReportName = reportName;
                exportReportDlg.OkDialog(TestFilesDir.GetTestPath("Calibration.csv"), columnSeparator);
            });

            // Check if export file is correct. 
            string filePath = TestFilesDir.GetTestPath("Calibration.csv");
            Assert.IsTrue(File.Exists(filePath));
            string[] lines = File.ReadAllLines(filePath);
            string[] line0 = lines[0].Split(columnSeparator);
            int count = line0.Length;
            Assert.IsTrue(lines.Count() == SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count + 1);
            Assert.IsTrue(count == columnsToAdd.Length);

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
