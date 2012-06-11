/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for CE Optimization.
    /// </summary>
    [TestClass]
    public class OptimizeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestOptimization()
        {
            TestFilesZip = @"TestFunctional\OptimizeTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Test CE optimization.  Creates optimization transition lists,
        /// imports optimization data, shows graphs, recalculates linear equations,
        /// and exports optimized method.
        /// </summary>
        protected override void DoTest()
        {
            // Remove all results files with the wrong extension for the current locale
            foreach (var fileName in Directory.GetFiles(TestFilesDir.FullPath, "*_REP*.*", SearchOption.AllDirectories))
            {
                if (!fileName.ToLower().EndsWith(ExtensionTestContext.ExtThermoRaw.ToLower()))
                    File.Delete(fileName);
            }

            // Open the .sky file
            string documentPath = TestFilesDir.GetTestPath("CE_Vantage_15mTorr_scheduled_mini.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            string filePath = TestFilesDir.GetTestPath("OptimizeCE.csv");
            ExportCEOptimizingTransitionList(filePath);

            // Create new CE regression for different transition list
            var docCurrent = SkylineWindow.Document;

            var transitionSettingsUI1 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editList = ShowDialog<EditListDlg<SettingsListBase<CollisionEnergyRegression>, CollisionEnergyRegression>>(transitionSettingsUI1.EditCEList);
            RunUI(() => editList.SelectItem("Thermo"));

            var editCE = ShowDialog<EditCEDlg>(editList.CopyItem);
            const string newCEName = "Thermo (Wide CE)";
            const double newStepSize = 2;
            const int newStepCount = 3;
            RunUI(() =>
                      {
                          editCE.Regression = (CollisionEnergyRegression) editCE.Regression
                                                                              .ChangeStepSize(newStepSize)
                                                                              .ChangeStepCount(newStepCount)
                                                                              .ChangeName(newCEName);
                      });
            OkDialog(editCE, editCE.OkDialog);
            OkDialog(editList, editList.OkDialog);
            RunUI(() =>
                      {
                          transitionSettingsUI1.RegressionCEName = newCEName;
                      });
            OkDialog(transitionSettingsUI1, transitionSettingsUI1.OkDialog);

            WaitForDocumentChange(docCurrent);

            // Make sure new settings are in document
            var newRegression = SkylineWindow.Document.Settings.TransitionSettings.Prediction.CollisionEnergy;
            Assert.AreEqual(newCEName, newRegression.Name);
            Assert.AreEqual(newStepSize, newRegression.StepSize);
            Assert.AreEqual(newStepCount, newRegression.StepCount);

            // Save a new optimization transition list with the new settings
            ExportCEOptimizingTransitionList(filePath);

            // Undo the change of CE regression
            RunUI(SkylineWindow.Undo);

            docCurrent = SkylineWindow.Document;

            var importResults = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);

            RunUI(() =>
                      {
                          importResults.NamedPathSets = DataSourceUtil.GetDataSourcesInSubdirs(TestFilesDir.FullPath).ToArray();
                          importResults.OptimizationName = ExportOptimize.CE;
                      });

            var removePrefix = ShowDialog<ImportResultsNameDlg>(importResults.OkDialog);
            RunUI(removePrefix.NoDialog);

            WaitForDocumentChange(docCurrent);

            foreach (var nodeTran in SkylineWindow.Document.Transitions)
            {
                Assert.IsTrue(nodeTran.HasResults);
                Assert.AreEqual(2, nodeTran.Results.Count);
            }

            // Set up display while loading
            RunUI(SkylineWindow.ArrangeGraphsTiled);

            SelectNode(SrmDocument.Level.Transitions, 0);

            RunUI(SkylineWindow.AutoZoomBestPeak);

            // Add some heavy precursors while loading
            const LabelAtoms labelAtoms = LabelAtoms.C13 | LabelAtoms.N15;
            const string heavyK = "Heavy K";
            const string heavyR = "Heavy R";
            Settings.Default.HeavyModList.Add(new StaticMod(heavyK, "K", ModTerminus.C, null, labelAtoms, null, null));
            Settings.Default.HeavyModList.Add(new StaticMod(heavyR, "R", ModTerminus.C, null, labelAtoms, null, null));

            docCurrent = SkylineWindow.Document;

            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            RunUI(() =>
                      {
                          peptideSettingsUI1.PickedHeavyMods = new[] {heavyK, heavyR};
                      });
            OkDialog(peptideSettingsUI1, peptideSettingsUI1.OkDialog);

            // First make sure the first settings change occurs
            WaitForDocumentChange(docCurrent);
            // Wait until everything is loaded
            WaitForCondition(300*1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);

            RunUI(() => SkylineWindow.SaveDocument());

            // Make sure imported data is of the expected shape.
            foreach (var nodeTran in SkylineWindow.Document.Transitions)
            {
                Assert.IsTrue(nodeTran.HasResults);
                Assert.AreEqual(2, nodeTran.Results.Count);
                foreach (var chromInfoList in nodeTran.Results)
                {
                    if (nodeTran.Transition.Group.LabelType.IsLight)
                    {
                        Assert.IsNotNull(chromInfoList,
                            string.Format("Peptide {0}{1}, fragment {2}{3} missing results",
                                nodeTran.Transition.Group.Peptide.Sequence,
                                Transition.GetChargeIndicator(nodeTran.Transition.Group.PrecursorCharge),
                                nodeTran.Transition.FragmentIonName,
                                Transition.GetChargeIndicator(nodeTran.Transition.Charge)));
                        Assert.AreEqual(11, chromInfoList.Count);
                    }
                    else
                        Assert.IsNull(chromInfoList);
                }
            }

            // Export a normal transition list with the default Thermo equation
            string normalPath = TestFilesDir.GetTestPath("NormalCE.csv");
            ExportCETransitionList(normalPath, null);

            // Export a transition list with CE optimized by precursor
            var transitionSettingsUI2 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
                      {
                          transitionSettingsUI2.UseOptimized = true;
                          transitionSettingsUI2.OptimizeType = OptimizedMethodType.Precursor.ToString();
                      });
            OkDialog(transitionSettingsUI2, transitionSettingsUI2.OkDialog);
            string precursorPath = TestFilesDir.GetTestPath("PrecursorCE.csv");
            ExportCETransitionList(precursorPath, normalPath);

            // Export a transition list with CE optimized by transition
            var transitionSettingsUI3 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
                      {
                          transitionSettingsUI3.OptimizeType = OptimizedMethodType.Transition.ToString();
                      });
            OkDialog(transitionSettingsUI3, transitionSettingsUI3.OkDialog);
            string transitionPath = TestFilesDir.GetTestPath("TransitionCE.csv");
            ExportCETransitionList(transitionPath, precursorPath);

            // Recalculate the CE optimization regression from this data
            const string reoptimizeCEName = "Thermo Reoptimized";
            docCurrent = SkylineWindow.Document;
            var transitionSettingsUI4 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editList4 = ShowDialog<EditListDlg<SettingsListBase<CollisionEnergyRegression>, CollisionEnergyRegression>>(transitionSettingsUI4.EditCEList);
            var editCE4 = ShowDialog<EditCEDlg>(editList4.AddItem);
            // Show the regression graph
            var showGraph = ShowDialog<GraphRegression>(editCE4.ShowGraph);
            RunUI(showGraph.CloseDialog);
            RunUI(() =>
                      {
                          editCE4.RegressionName = reoptimizeCEName;
                          editCE4.UseCurrentData();
                      });
            OkDialog(editCE4, editCE4.OkDialog);
            OkDialog(editList4, editList4.OkDialog);
            RunUI(() =>
            {
                transitionSettingsUI4.RegressionCEName = reoptimizeCEName;
                transitionSettingsUI4.OptimizeType = OptimizedMethodType.None.ToString();
            });
            OkDialog(transitionSettingsUI4, transitionSettingsUI4.OkDialog);
            WaitForDocumentChange(docCurrent);

            // Make sure new settings are in document
            var reoptimizeRegression = SkylineWindow.Document.Settings.TransitionSettings.Prediction.CollisionEnergy;
            Assert.AreEqual(reoptimizeCEName, reoptimizeRegression.Name);
            Assert.AreEqual(1, reoptimizeRegression.Conversions.Length);
            Assert.AreEqual(2, reoptimizeRegression.Conversions[0].Charge);

            // Export a transition list with the new equation
            string reoptimizePath = TestFilesDir.GetTestPath("ReoptimizeCE.csv");
            ExportCETransitionList(reoptimizePath, normalPath);

            RunUI(() => SkylineWindow.ShowGraphPeakArea(true));
            RunUI(() => SkylineWindow.ShowGraphRetentionTime(true));
            RunUI(SkylineWindow.ShowRTReplicateGraph);

            VerifyGraphs();

            SelectNode(SrmDocument.Level.TransitionGroups, 2);

            VerifyGraphs();
        }

        private static void VerifyGraphs()
        {
            RunUI(SkylineWindow.ShowAllTransitions);
            WaitForGraphs();

            SrmDocument docCurrent = SkylineWindow.Document;
            int transitions = docCurrent.TransitionCount/docCurrent.TransitionGroupCount;
            foreach (var chromSet in docCurrent.Settings.MeasuredResults.Chromatograms)
                Assert.AreEqual(transitions, SkylineWindow.GetGraphChrom(chromSet.Name).CurveCount);
            Assert.AreEqual(transitions, SkylineWindow.GraphPeakArea.CurveCount);
            Assert.AreEqual(transitions, SkylineWindow.GraphRetentionTime.CurveCount);

            RunUI(SkylineWindow.ShowSingleTransition);
            WaitForGraphs();

            int maxSteps = 0;
            foreach (var chromSet in docCurrent.Settings.MeasuredResults.Chromatograms)
            {
                int stepCount = chromSet.OptimizationFunction.StepCount*2 + 1;
                maxSteps = Math.Max(maxSteps, stepCount);
                Assert.AreEqual(stepCount, SkylineWindow.GetGraphChrom(chromSet.Name).CurveCount);
            }
            Assert.AreEqual(maxSteps, SkylineWindow.GraphPeakArea.CurveCount);
            Assert.AreEqual(maxSteps, SkylineWindow.GraphRetentionTime.CurveCount);

            RunUI(SkylineWindow.ShowTotalTransitions);
            WaitForGraphs();

            foreach (var chromSet in docCurrent.Settings.MeasuredResults.Chromatograms)
                Assert.AreEqual(1, SkylineWindow.GetGraphChrom(chromSet.Name).CurveCount);
            Assert.AreEqual(1, SkylineWindow.GraphPeakArea.CurveCount);
            Assert.AreEqual(1, SkylineWindow.GraphRetentionTime.CurveCount);
        }

        private const int COL_PREC_MZ = 0;
        private const int COL_PROD_MZ = 1;
        private const int COL_CE = 2;
        private static void ExportCEOptimizingTransitionList(string filePath)
        {
            File.Delete(filePath);

            var exportDialog = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.List));

            // Export CE optimization transition list
            RunUI(() =>
            {
                exportDialog.ExportStrategy = ExportStrategy.Single;
                exportDialog.MethodType = ExportMethodType.Standard;
                exportDialog.OptimizeType = ExportOptimize.CE;
            });
            OkDialog(exportDialog, () => exportDialog.OkDialog(filePath));

            WaitForCondition(() => File.Exists(filePath));

            VerifyCEOptimizingTransitionList(filePath, SkylineWindow.Document);            
        }

        private static void VerifyCEOptimizingTransitionList(string filePath, SrmDocument document)
        {
            var regressionCE = document.Settings.TransitionSettings.Prediction.CollisionEnergy;
            double stepSize = regressionCE.StepSize;
            int stepCount = regressionCE.StepCount;
            stepCount = stepCount*2 + 1;

            string[] lines = File.ReadAllLines(filePath);
            Assert.AreEqual(document.TransitionCount*stepCount, lines.Length);

            int stepsSeen = 0;
            double lastPrecursorMz = 0;
            double lastProductMz = 0;
            double lastCE = 0;

            var cultureInfo = CultureInfo.InvariantCulture;

            foreach (string line in lines)
            {
                string[] row = line.Split(',');
                double precursorMz = double.Parse(row[COL_PREC_MZ], cultureInfo);
                double productMz = double.Parse(row[COL_PROD_MZ], cultureInfo);
                double ce = double.Parse(row[COL_CE], cultureInfo);
                if (precursorMz != lastPrecursorMz ||
                    Math.Abs((productMz - lastProductMz) - ChromatogramInfo.OPTIMIZE_SHIFT_SIZE) > 0.0001)
                {
                    if (stepsSeen > 0)
                        Assert.AreEqual(stepCount, stepsSeen);

                    lastPrecursorMz = precursorMz;
                    lastProductMz = productMz;
                    lastCE = ce;
                    stepsSeen = 1;
                }
                else
                {
                    Assert.AreEqual(lastCE + stepSize, ce);
                    lastProductMz = productMz;
                    lastCE = ce;
                    stepsSeen++;
                }
            }
        }

        private static void ExportCETransitionList(string filePath, string fileCompare)
        {
            var exportDialog = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.List));

            // Export CE optimization transition list
            RunUI(() =>
            {
                exportDialog.ExportStrategy = ExportStrategy.Single;
                exportDialog.MethodType = ExportMethodType.Standard;
                exportDialog.OkDialog(filePath);
            });
            VerifyCETransitionList(filePath, fileCompare, SkylineWindow.Document);
        }

        private static void VerifyCETransitionList(string filePath, string fileCompare, SrmDocument document)
        {
            string[] lines1 = File.ReadAllLines(filePath);
            string[] lines2 = null;
            if (fileCompare != null)
            {
                lines2 = File.ReadAllLines(fileCompare);
                Assert.AreEqual(lines2.Length, lines1.Length);
            }

            var optType = document.Settings.TransitionSettings.Prediction.OptimizedMethodType;
            bool precursorCE = (optType != OptimizedMethodType.Transition);

            bool diffCEFound = (fileCompare == null);
            bool diffTranFound = false;

            int iLine = 0;
            var dictLightCEs = new Dictionary<string, double>();
            foreach (var nodeGroup in document.TransitionGroups)
            {
                if (nodeGroup.IsLight)
                    dictLightCEs.Clear();
                double firstCE = double.Parse(lines1[iLine].Split(',')[COL_CE], CultureInfo.InvariantCulture);
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    string[] row1 = lines1[iLine].Split(',');
                    double tranCE = double.Parse(row1[COL_CE], CultureInfo.InvariantCulture);
                    if (lines2 != null)
                    {
                        // Check to see if the two files differ
                        string[] row2 = lines2[iLine].Split(',');
                        if (row1[COL_CE] != row2[COL_CE])
                            diffCEFound = true;
                    }
                    iLine++;

                    // Store light CE values, and compare the heavy CE values to make
                    // sure they are equal
                    if (nodeGroup.IsLight)
                        dictLightCEs.Add(nodeTran.Transition.ToString(), tranCE);
                    else
                        Assert.AreEqual(dictLightCEs[nodeTran.Transition.ToString()], tranCE);

                    // If precursor CE type, then all CEs should be equal
                    if (precursorCE)
                        Assert.AreEqual(firstCE, tranCE);
                    else if (firstCE != tranCE)
                        diffTranFound = true;
                }
            }
            Assert.IsTrue(diffCEFound);
            Assert.IsTrue(precursorCE || diffTranFound);
        }
    }
}
