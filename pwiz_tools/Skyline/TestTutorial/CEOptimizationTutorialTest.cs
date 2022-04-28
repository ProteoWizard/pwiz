/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
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
    /// <summary>
    /// Testing the tutorial for Skyline Collision Energy Optimization
    /// </summary>
    [TestClass]
    public class CEOptimizationTutorialTest : AbstractFunctionalTestEx
    {
        private bool AsSmallMolecules { get; set; }

        [TestMethod]
        public void TestCEOptimizationTutorial()
        {
            AsSmallMolecules = false;
            RunCEOptimizationTutorialTest();
        }

        [TestMethod]
        public void TestCEOptimizationTutorialAsSmallMolecules()
        {
            if (SkipSmallMoleculeTestVersions())
            {
                return;
            }
            AsSmallMolecules = true;
            RunCEOptimizationTutorialTest();
        }

        private void RunCEOptimizationTutorialTest()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "OptimizeCE";

            ForceMzml = true;   // Mzml is ~2x faster for this test.

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/OptimizeCE-20_2.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.ms/tutorials/OptimizeCE.zip"
                    : @"https://skyline.ms/tutorials/OptimizeCEMzml.zip",
                @"TestTutorial\CEOptimizationViews.zip"
            };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            var folderOptimizeCE = UseRawFiles ? "OptimizeCE" : "OptimizeCEMzml"; // Not L10N
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderOptimizeCE, relativePath));
        }

        protected override void DoTest()
        {
            // Skyline Collision Energy Optimization
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("CE_Vantage_15mTorr.sky"))); // Not L10N

            if (AsSmallMolecules)
            {
                ConvertDocumentToSmallMolecules();
            }

            // Deriving a New Linear Equation, p. 2
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editList = 
                ShowDialog<EditListDlg<SettingsListBase<CollisionEnergyRegression>, CollisionEnergyRegression>>
                (transitionSettingsUI.EditCEList);
            RunUI(() => editList.SelectItem("Thermo")); // Not L10N
            EditCEDlg editItem = ShowDialog<EditCEDlg>(editList.EditItem);

            PauseForScreenShot<EditCEDlg>("Edit Collision Energy Equation form", 3);

            ChargeRegressionLine regressionLine2 = new ChargeRegressionLine(2, 0.034, 3.314); 
            ChargeRegressionLine regressionLine3 = new ChargeRegressionLine(3, 0.044, 3.314);

            CheckRegressionLines(new[] {regressionLine2, regressionLine3}, editItem.Regression.Conversions);

            RunUI(() =>
            {
                editItem.DialogResult = DialogResult.OK;
                editList.DialogResult = DialogResult.Cancel;
                transitionSettingsUI.DialogResult = DialogResult.Cancel;
            });
            WaitForClosedForm(transitionSettingsUI);

            // Measuring Retention Times for Method Scheduling, p. 3
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO;
                    exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                    exportMethodDlg.OptimizeType = ExportOptimize.NONE;
                    exportMethodDlg.MethodType = ExportMethodType.Standard;
                });

                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List form", 4);

                RunUI(() => exportMethodDlg.OkDialog(GetTestPath("CE_Vantage_15mTorr_unscheduled.csv"))); // Not L10N
                WaitForClosedForm(exportMethodDlg);
            }

            string filePathTemplate = GetTestPath("CE_Vantage_15mTorr_unscheduled.csv"); // Not L10N
            CheckTransitionList(filePathTemplate, new []{120}, 6);

            const string unscheduledName = "Unscheduled"; // Not L10N
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                var path =
                    new[] {new KeyValuePair<string, MsDataFileUri[]>(unscheduledName,
                        // This is not actually a valid file path (missing OptimizeCE)
                        // but Skyline should correctly find the file in the same folder
                        // as the document.
                        new[] { MsDataFileUri.Parse(GetTestPath("CE_Vantage_15mTorr_unscheduled" + ExtThermoRaw))})}; // Not L10N
                importResultsDlg.NamedPathSets = path;
                importResultsDlg.OkDialog();
            });
            WaitForCondition(5*60*1000, () => SkylineWindow.Document.Settings.HasResults &&
                SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 5 minutes
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 7, 27, 30, 120);
            var docUnsched = SkylineWindow.Document;
            AssertResult.IsDocumentResultsState(SkylineWindow.Document,
                                                unscheduledName,
                                                docUnsched.MoleculeCount,
                                                docUnsched.MoleculeTransitionGroupCount, 0,
                                                docUnsched.MoleculeTransitionCount, 0);

            RunUI(() =>
            {
                SkylineWindow.ExpandProteins();
                SkylineWindow.ExpandPeptides();
            });

            RestoreViewOnScreen(5);
            PauseForScreenShot("Main Skyline window", 5);

            // Creating Optimization Methods, p. 5
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO;
                    exportMethodDlg.ExportStrategy = ExportStrategy.Buckets;
                    exportMethodDlg.MaxTransitions = 110;
                    exportMethodDlg.IgnoreProteins = true;
                    exportMethodDlg.OptimizeType = ExportOptimize.CE;
                    exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                });

                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List form", 6);

                RunUI(() => exportMethodDlg.OkDialog(GetTestPath("CE_Vantage_15mTorr.csv"))); // Not L10N
                WaitForClosedForm(exportMethodDlg);
            }

            string filePathTemplate1 = GetTestPath("CE_Vantage_15mTorr_000{0}.csv"); // Not L10N
            CheckTransitionList(filePathTemplate1, new[] { 220, 220, 264, 308, 308 }, 9);

            var filePath = GetTestPath("CE_Vantage_15mTorr_0001.csv"); // Not L10N
            CheckCEValues(filePath, 11);
           
            // Analyze Optimization Data, p. 7
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                importResultsDlg.OptimizationName = ExportOptimize.CE;
                importResultsDlg.NamedPathSets = DataSourceUtil.GetDataSourcesInSubdirs(TestFilesDirs[0].FullPath).ToArray();
                importResultsDlg.NamedPathSets[0] =
                     new KeyValuePair<string, MsDataFileUri[]>("Optimize CE", importResultsDlg.NamedPathSets[0].Value.Take(5).ToArray()); // Not L10N
                importResultsDlg.OkDialog();
            });
            RunUI(() => 
            {
                SkylineWindow.ShowSingleTransition();
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });

            WaitForDocumentLoaded(15 * 60 * 1000); // 10 minutes
            string decorator = AsSmallMolecules
                ? RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator
                : string.Empty;

            FindNode(decorator + "IHGFDLAAINLQR");
            RestoreViewOnScreen(8);
            RunUI(() => SkylineWindow.Size = new Size(984, 553));
            PauseForScreenShot("Main Skyline window", 8);

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 14;
                    Settings.Default.AreaFontSize = 14;
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    SkylineWindow.ShowPeakAreaLegend(false);
                });

                RestoreCoverViewOnScreen();

                RunUI(SkylineWindow.FocusDocument);

                TakeCoverShot();
                return;
            }

            // p. 8
            // Not L10N
            RemoveTargetByDisplayName(decorator + "EGIHAQQK");

            FindNode(decorator + "IDALNENK");

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.TOTAL));

            PauseForScreenShot("Main Skyline window", 9);

            RunUI(SkylineWindow.EditDelete);

            RemoveTargetByDisplayName(AsSmallMolecules ? decorator + "LIC[+57.0]DNTHITK" : "LICDNTHITK");

            // Creating a New Equation for CE, p. 9
            var transitionSettingsUI1 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editCEDlg1 = ShowDialog<EditListDlg<SettingsListBase<CollisionEnergyRegression>, CollisionEnergyRegression>>(transitionSettingsUI1.EditCEList);
            var addItem = ShowDialog<EditCEDlg>(editCEDlg1.AddItem);
            RunUI(() =>
            {
                addItem.RegressionName = "Thermo Vantage Tutorial"; // Not L10N
                addItem.UseCurrentData();
            });

            var graphRegression = ShowDialog<GraphRegression>(addItem.ShowGraph);

            PauseForScreenShot<GraphRegression>("Collision Energy Regression graphs", 10);

            var graphDatas = graphRegression.RegressionGraphDatas.ToArray();
            Assert.AreEqual(2, graphDatas.Length);

            ChargeRegressionLine regressionLine21 = new ChargeRegressionLine(2, 0.0305, 2.5061);
            ChargeRegressionLine regressionLine31 = new ChargeRegressionLine(3, 0.0397, 1.4217);
            var expectedRegressions = new[] {regressionLine21, regressionLine31};

            CheckRegressionLines(expectedRegressions, new[]
                                                          {
                                                              new ChargeRegressionLine(2, 
                                                                  Math.Round(graphDatas[0].RegressionLine.Slope, 4),
                                                                  Math.Round(graphDatas[0].RegressionLine.Intercept, 4)), 
                                                              new ChargeRegressionLine(3,
                                                                  Math.Round(graphDatas[1].RegressionLine.Slope, 4),
                                                                  Math.Round(graphDatas[1].RegressionLine.Intercept, 4)), 
                                                          });

            RunUI(graphRegression.CloseDialog);
            WaitForClosedForm(graphRegression);
            RunUI(addItem.OkDialog);
            WaitForClosedForm(addItem);
            RunUI(editCEDlg1.OkDialog);
            WaitForClosedForm(editCEDlg1);
            RunUI(transitionSettingsUI1.OkDialog);
            WaitForClosedForm(transitionSettingsUI1);

            // Optimizing Each Transition, p. 10
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI2 =>
            {
                transitionSettingsUI2.UseOptimized = true;
                transitionSettingsUI2.OptimizeType = OptimizedMethodType.Transition.GetLocalizedString();
                transitionSettingsUI2.OkDialog();
            });
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List), exportMethodDlg =>
            {
                exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                exportMethodDlg.OkDialog(GetTestPath("CE_Vantage_15mTorr_optimized.csv")); // Not L10N
            });

            var filePathTemplate2 = GetTestPath("CE_Vantage_15mTorr_optimized.csv"); // Not L10N

            CheckTransitionList(filePathTemplate2, new[] { 108 }, 9);

            RunUI(() => SkylineWindow.SaveDocument());
            WaitForConditionUI(() => !SkylineWindow.Dirty);
        }

        public static void CheckRegressionLines(ChargeRegressionLine[] lines1, ChargeRegressionLine[] lines2)
        {
            Assert.IsTrue(ArrayUtil.EqualsDeep(lines1, lines2));
        }

        public void CheckTransitionList(string templatePath, int[] transitionCounts, int columnCount)
        {
            for (int i = 1; i <= transitionCounts.Length; i++)
            {
                string filePath = TestFilesDirs[0].GetTestPath(string.Format(templatePath, i));
                WaitForCondition(() => File.Exists(filePath), "waiting for creation of "+filePath);
                string[] lines = File.ReadAllLines(filePath);
                Assert.AreEqual(transitionCounts[i - 1], lines.Length);
                string[] line = lines[0].Split(',');
                int count = line.Length;
                // Comma at end to indicate start of column on a new row.
                Assert.IsTrue(count - 1 == columnCount);
            }
            // If there are multiple file possibilities, make sure there are
            // not more files than expected by checking count+1
            if (templatePath.Contains("{0}")) // Not L10N
                Assert.IsFalse(File.Exists(TestFilesDirs[0].GetTestPath(string.Format(templatePath, transitionCounts.Length+1))));
        }

        public void CheckCEValues(string filePath, int ceCount)
        {
            List<string> ceValues = new List<string>();
            string[] lines = File.ReadAllLines(filePath);

            string precursor = lines[0].Split(TextUtil.SEPARATOR_CSV)[0];
            foreach (var line in lines)
            {
                var columns = line.Split(TextUtil.SEPARATOR_CSV);
                var ce = columns[2];
                var secondPrecursor = columns[0];

                // Different CE values for each precursor ion, repeated for each
                // product ion of the precursor.
                if (precursor != secondPrecursor)
                {
                    Assert.IsTrue(ceValues.Count == ceCount);
                    ceValues.Clear();
                    precursor = secondPrecursor;
                }
                // Only add once per precursor 
                if (!ceValues.Contains(ce))
                    ceValues.Add(ce);
            }

            // Check last precusor set.
            Assert.IsTrue(ceValues.Count == ceCount);
        }
    }
}
