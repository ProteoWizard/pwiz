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
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Optimization;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for CE Optimization.
    /// </summary>
    [TestClass]
    public class OptimizeTest : AbstractFunctionalTestEx
    {
        private bool AsSmallMolecules;

        [TestMethod]
        public void TestOptimization()
        {
            AsSmallMolecules = false;
            TestFilesZip = @"TestFunctional\OptimizeTest.zip";
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestOptimizationAsSmallMolecules ()
        {
            AsSmallMolecules = true;
            TestFilesZip = @"TestFunctional\OptimizeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            if (AsSmallMolecules && !RunSmallMoleculeTestVersions)
            {
                Console.Write(MSG_SKIPPING_SMALLMOLECULE_TEST_VERSION);
                return;
            }

            CEOptimizationTest();
            OptLibNeutralLossTest();

            CovOptimizationTest();
            Assert.IsFalse(IsCovRecordMode);    // Make sure no commits with this set to true
        }

        /// <summary>
        /// Test CE optimization.  Creates optimization transition lists,
        /// imports optimization data, shows graphs, recalculates linear equations,
        /// and exports optimized method.
        /// </summary>
        private void CEOptimizationTest()
        {
            // Remove all results files with the wrong extension for the current locale
            foreach (var fileName in Directory.GetFiles(TestFilesDir.FullPath, "*_REP*.*", SearchOption.AllDirectories))
            {
                if (!PathEx.HasExtension(fileName, ExtensionTestContext.ExtThermoRaw))
                    FileEx.SafeDelete(fileName);
            }

            // Open the .sky file
            string documentPath = TestFilesDir.GetTestPath("CE_Vantage_15mTorr_scheduled_mini.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            if (AsSmallMolecules)
            {
                ConvertDocumentToSmallMolecules();
            }

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

            // Test optimization library
            docCurrent = SkylineWindow.Document;

            // Open transition settings and add new optimization library
            var transitionSettingsUIOpt = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editOptLibLoadExisting = ShowDialog<EditOptimizationLibraryDlg>(transitionSettingsUIOpt.AddToOptimizationLibraryList);
            // Load from existing file
            const string existingLibName = "Test load existing library";
            var duplicatesOptdb = AsSmallMolecules ? "DuplicatesSmallMol.optdb" : "Duplicates.optdb";
            RunUI(() =>
            {
                editOptLibLoadExisting.OpenDatabase(TestFilesDir.GetTestPath(duplicatesOptdb));
                editOptLibLoadExisting.LibName = existingLibName;
            });
            OkDialog(editOptLibLoadExisting, editOptLibLoadExisting.OkDialog);
            // Add new optimization library
            var editOptLib = ShowDialog<EditOptimizationLibraryDlg>(transitionSettingsUIOpt.AddToOptimizationLibraryList);

            string optLibPath = TestFilesDir.GetTestPath(AsSmallMolecules ? "OptimizedSmallMol.optdb" : "Optimized.optdb");
            string pasteText = TextUtil.LineSeparate(
                GetPasteLine("TPEVDDEALEK", 2, "y9", 1, 122.50606),
                GetPasteLine("DGGIDPLVR", 2, "y6", 1, 116.33671),
                GetPasteLine("AAA", 5, "y1", 2, 5.0),
                GetPasteLine("AAC", 5, "y2", 2, 5.0));

            RunUI(() =>
            {
                editOptLib.CreateDatabase(optLibPath);
                editOptLib.LibName = "Test optimized library";
                SetClipboardText(pasteText);
            });
            var addOptDlg = ShowDialog<AddOptimizationsDlg>(editOptLib.DoPasteLibrary);
            OkDialog(addOptDlg, addOptDlg.OkDialog);

            // Add duplicates and skip existing
            // "AAA, +5", "y1++", "5.0"
            // "AAC, +5", "y2++", "10.0"
            var addOptDbDlgSkip = ShowDialog<AddOptimizationLibraryDlg>(editOptLib.AddOptimizationDatabase);
            RunUI(() =>
            {
                addOptDbDlgSkip.Source = OptimizationLibrarySource.settings;
                addOptDbDlgSkip.SetLibrary(existingLibName);
            });
            var addOptDlgAskSkip = ShowDialog<AddOptimizationsDlg>(addOptDbDlgSkip.OkDialog);
            Assert.AreEqual(0, addOptDlgAskSkip.OptimizationsCount);
            Assert.AreEqual(1, addOptDlgAskSkip.ExistingOptimizationsCount);
            RunUI(() => addOptDlgAskSkip.Action = AddOptimizationsAction.skip);
            OkDialog(addOptDlgAskSkip, addOptDlgAskSkip.OkDialog);
            var target_AAC = GetTarget("AAC");
            Assert.AreEqual(5.0, editOptLib.GetCEOptimization(target_AAC, GetAdduct(5), "y2", GetAdduct(2)).Value);
            // Add duplicates and average existing
            var addOptDbDlgAvg = ShowDialog<AddOptimizationLibraryDlg>(editOptLib.AddOptimizationDatabase);
            RunUI(() =>
            {
                addOptDbDlgAvg.Source = OptimizationLibrarySource.file;
                addOptDbDlgAvg.FilePath = TestFilesDir.GetTestPath(duplicatesOptdb);
            });
            var addOptDlgAskAvg = ShowDialog<AddOptimizationsDlg>(addOptDbDlgAvg.OkDialog);
            Assert.AreEqual(0, addOptDlgAskAvg.OptimizationsCount);
            Assert.AreEqual(1, addOptDlgAskAvg.ExistingOptimizationsCount);
            RunUI(() => addOptDlgAskAvg.Action = AddOptimizationsAction.average);
            OkDialog(addOptDlgAskAvg, addOptDlgAskAvg.OkDialog);
            Assert.AreEqual(7.5, editOptLib.GetCEOptimization(target_AAC, GetAdduct(5), "y2", GetAdduct(2)).Value);
            // Add duplicates and replace existing
            var addOptDbDlgReplace = ShowDialog<AddOptimizationLibraryDlg>(editOptLib.AddOptimizationDatabase);
            RunUI(() =>
            {
                addOptDbDlgReplace.Source = OptimizationLibrarySource.file;
                addOptDbDlgReplace.FilePath = TestFilesDir.GetTestPath(duplicatesOptdb);
            });
            var addOptDlgAskReplace = ShowDialog<AddOptimizationsDlg>(addOptDbDlgReplace.OkDialog);
            Assert.AreEqual(0, addOptDlgAskReplace.OptimizationsCount);
            Assert.AreEqual(1, addOptDlgAskReplace.ExistingOptimizationsCount);
            RunUI(() => addOptDlgAskReplace.Action = AddOptimizationsAction.replace);
            OkDialog(addOptDlgAskReplace, addOptDlgAskReplace.OkDialog);
            Assert.AreEqual(10.0, editOptLib.GetCEOptimization(target_AAC, GetAdduct(5), "y2", GetAdduct(2)).Value);

            // Try to add unconvertible old format optimization library
            var addOptDbUnconvertible = ShowDialog<AddOptimizationLibraryDlg>(editOptLib.AddOptimizationDatabase);
            RunUI(() =>
            {
                addOptDbUnconvertible.Source = OptimizationLibrarySource.file;
                addOptDbUnconvertible.FilePath = TestFilesDir.GetTestPath("OldUnconvertible.optdb");
            });
            OkDialog(addOptDbUnconvertible, addOptDbUnconvertible.OkDialog);
            var errorDlg = WaitForOpenForm<MessageDlg>();
            AssertEx.AreComparableStrings(Resources.OptimizationDb_ConvertFromOldFormat_Failed_to_convert__0__optimizations_to_new_format_,
                errorDlg.Message, 1);
            OkDialog(errorDlg, errorDlg.OkDialog);

            // Try to add convertible old format optimization library
            var addOptDbConvertible = ShowDialog<AddOptimizationLibraryDlg>(editOptLib.AddOptimizationDatabase);
            RunUI(() =>
            {
                addOptDbConvertible.Source = OptimizationLibrarySource.file;
                addOptDbConvertible.FilePath = TestFilesDir.GetTestPath("OldConvertible.optdb");
            });
            var addOptDlgAskConverted = ShowDialog<AddOptimizationsDlg>(addOptDbConvertible.OkDialog);
            Assert.AreEqual(AsSmallMolecules ? 111 : 109, addOptDlgAskConverted.OptimizationsCount);
            Assert.AreEqual(AsSmallMolecules ? 0 : 2, addOptDlgAskConverted.ExistingOptimizationsCount);
            RunUI(addOptDlgAskConverted.CancelDialog);

            // Done editing optimization library
            OkDialog(editOptLib, editOptLib.OkDialog);
            OkDialog(transitionSettingsUIOpt, transitionSettingsUIOpt.OkDialog);
            WaitForDocumentChange(docCurrent);

            string optLibExportPath = TestFilesDir.GetTestPath("OptLib.csv");
            ExportCETransitionList(optLibExportPath, null);

            // Undo the change of Optimization Library
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

            foreach (var nodeTran in SkylineWindow.Document.MoleculeTransitions)
            {
                Assert.IsTrue(nodeTran.HasResults);
                Assert.AreEqual(2, nodeTran.Results.Count);
            }

            // Set up display while loading
            RunUI(SkylineWindow.ArrangeGraphsTiled);

            SelectNode(SrmDocument.Level.TransitionGroups, 0);

            RunUI(SkylineWindow.AutoZoomBestPeak);
            // Add some heavy precursors while loading
            const LabelAtoms labelAtoms = LabelAtoms.C13 | LabelAtoms.N15;
            const string heavyK = "Heavy K";
            const string heavyR = "Heavy R";
            RunUI(() =>
            {
                Settings.Default.HeavyModList.Add(new StaticMod(heavyK, "K", ModTerminus.C, null, labelAtoms, null, null));
                Settings.Default.HeavyModList.Add(new StaticMod(heavyR, "R", ModTerminus.C, null, labelAtoms, null, null));
            });

            docCurrent = SkylineWindow.Document;

            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            RunUI(() =>
                      {
                          peptideSettingsUI1.PickedHeavyMods = new[] {heavyK, heavyR};
                      });
            OkDialog(peptideSettingsUI1, peptideSettingsUI1.OkDialog);

            // First make sure the first settings change occurs
            WaitForDocumentChangeLoaded(docCurrent, 300*1000);

            // Verify that "GraphChromatogram.DisplayOptimizationTotals" works for different values
            // of TransformChrom
            RunUI(()=>
            {
                SkylineWindow.ShowSingleTransition();
                SkylineWindow.SetTransformChrom(TransformChrom.raw);
            });
            WaitForGraphs();
            RunUI(() =>
            {
                SkylineWindow.SetTransformChrom(TransformChrom.interpolated);
            });
            WaitForGraphs();

            SelectNode(SrmDocument.Level.Transitions, 0);

            RunUI(() => SkylineWindow.SaveDocument());

            // Make sure imported data is of the expected shape.
            foreach (var nodeGroup in SkylineWindow.Document.MoleculeTransitionGroups)
            {
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    Assert.IsTrue(nodeTran.HasResults);
                    Assert.AreEqual(2, nodeTran.Results.Count);
                    foreach (var chromInfoList in nodeTran.Results)
                    {
                        if (nodeGroup.TransitionGroup.LabelType.IsLight)
                        {
                            Assert.IsFalse(chromInfoList.IsEmpty,
                                string.Format("Peptide {0}{1}, fragment {2}{3} missing results",
                                    nodeTran.Transition.Group.Peptide.Target,
                                    nodeGroup.TransitionGroup.PrecursorAdduct,
                                    nodeTran.Transition.FragmentIonName,
                                    Transition.GetChargeIndicator(nodeTran.Transition.Adduct)));
                            Assert.AreEqual(11, chromInfoList.Count);
                        }
                    }
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
                          transitionSettingsUI2.OptimizeType = OptimizedMethodType.Precursor.GetLocalizedString();
                      });
            OkDialog(transitionSettingsUI2, transitionSettingsUI2.OkDialog);
            string precursorPath = TestFilesDir.GetTestPath("PrecursorCE.csv");
            ExportCETransitionList(precursorPath, normalPath);

            // Export a transition list with CE optimized by transition
            var transitionSettingsUI3 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
                      {
                          transitionSettingsUI3.OptimizeType = OptimizedMethodType.Transition.GetLocalizedString();
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
                transitionSettingsUI4.OptimizeType = OptimizedMethodType.None.GetLocalizedString();
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

            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            RunUI(SkylineWindow.ShowRTReplicateGraph);

            VerifyGraphs();

            SelectNode(SrmDocument.Level.TransitionGroups, 2);

            VerifyGraphs();
        }

        private void OptLibNeutralLossTest()
        {
            if (AsSmallMolecules)
                return; // Not a concern for small mol docs

            // Open the .sky file
            string documentPath = TestFilesDir.GetTestPath("test_opt_nl.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            var docCurrent = SkylineWindow.Document;

            var transitionSettingsUIOpt = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editOptLib = ShowDialog<EditOptimizationLibraryDlg>(transitionSettingsUIOpt.AddToOptimizationLibraryList);

            string optLibPath = TestFilesDir.GetTestPath("NeutralLoss.optdb");

            RunUI(() =>
            {
                editOptLib.CreateDatabase(optLibPath);
                editOptLib.LibName = "Test neutral loss optimization";
                SetClipboardText(GetPasteLine("PES[+80.0]T[+80.0]ICIDER", 2, "precursor -98", 2, 5.0));
            });
            var addOptDlg = ShowDialog<AddOptimizationsDlg>(editOptLib.DoPasteLibrary);
            OkDialog(addOptDlg, addOptDlg.OkDialog);
            
            OkDialog(editOptLib, editOptLib.OkDialog);
            OkDialog(transitionSettingsUIOpt, transitionSettingsUIOpt.OkDialog);

            WaitForDocumentChange(docCurrent);

            string optLibExportPath = TestFilesDir.GetTestPath("OptNeutralLoss.csv");
            ExportCETransitionList(optLibExportPath, null);
        }

        /// <summary>
        /// Change to true to write covdata\*.csv test files
        /// </summary>
        private bool IsCovRecordMode { get { return false; } }

        private void CovOptimizationTest()
        {
            // Open the .sky file
            string documentPath = TestFilesDir.GetTestPath(@"covdata\cov_optimization_part.sky");
            string wiffFile = TestFilesDir.GetTestPath(@"covdata\wiff\041115 BG_sky Test Round 1.wiff");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            if (AsSmallMolecules)
            {
                ConvertDocumentToSmallMolecules();
            }

            string expectedTransitionsRough = TestFilesDir.GetTestPath(@"covdata\cov_rough_expected.csv");
            string outTransitionsRough = IsCovRecordMode && !AsSmallMolecules ? expectedTransitionsRough : TestFilesDir.GetTestPath(@"covdata\cov_rough.csv");
            string expectedTransitionsMedium = TestFilesDir.GetTestPath(@"covdata\cov_medium_expected.csv");
            string outTransitionsMedium = IsCovRecordMode && !AsSmallMolecules ? expectedTransitionsMedium : TestFilesDir.GetTestPath(@"covdata\cov_medium.csv");
            string expectedTransitionsFine = TestFilesDir.GetTestPath(@"covdata\cov_fine_expected.csv");
            string outTransitionsFine = IsCovRecordMode && !AsSmallMolecules ? expectedTransitionsFine : TestFilesDir.GetTestPath(@"covdata\cov_fine.csv");
            string expectedTransitionsFinal = TestFilesDir.GetTestPath(@"covdata\cov_final_expected.csv");
            string outTransitionsFinal = IsCovRecordMode && !AsSmallMolecules ? expectedTransitionsFinal : TestFilesDir.GetTestPath(@"covdata\cov_final.csv");
            string expectedTransitionsFinalWithOptLib = TestFilesDir.GetTestPath(@"covdata\cov_final_expected2.csv");
            string outTransitionsFinalWithOptLib = IsCovRecordMode && !AsSmallMolecules ? expectedTransitionsFinalWithOptLib : TestFilesDir.GetTestPath(@"covdata\cov_final2.csv");
            string expectedTransitionsFinalWithOptLib2 = TestFilesDir.GetTestPath(@"covdata\cov_final_expected3.csv");
            string outTransitionsFinalWithOptLib2 = IsCovRecordMode && !AsSmallMolecules ? expectedTransitionsFinalWithOptLib2 : TestFilesDir.GetTestPath(@"covdata\cov_final3.csv");

            var doc = SkylineWindow.Document;

            // Verify settings
            var prediction = doc.Settings.TransitionSettings.Prediction;
            var cov = prediction.CompensationVoltage;
            Assert.AreEqual(OptimizedMethodType.Precursor, prediction.OptimizedMethodType);
            Assert.AreEqual("ABI", cov.Name);   // Old name
            Assert.AreEqual(6, cov.MinCov);
            Assert.AreEqual(30, cov.MaxCov);
            Assert.AreEqual(3, cov.StepCountRough);
            Assert.AreEqual(3, cov.StepCountMedium);
            Assert.AreEqual(3, cov.StepCountFine);

            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings.RegressionCEName = "SCIEX";
                transitionSettings.RegressionDPName = "SCIEX";
            });
            var covList = ShowDialog<EditListDlg<SettingsListBase<CompensationVoltageParameters>, CompensationVoltageParameters>>(transitionSettings.EditCoVList);
            RunUI(() => covList.SelectItem("SCIEX"));
            var covSettings = ShowDialog<EditCoVDlg>(covList.EditItem);
            double? originalMin = null, originalMax = null;
            int? originalStepsRough = null, originalStepsMedium = null, originalStepsFine = null;
            RunUI(() =>
            {
                originalMin = covSettings.Min;
                originalMax = covSettings.Max;
                originalStepsRough = covSettings.StepsRough;
                originalStepsMedium = covSettings.StepsMedium;
                originalStepsFine = covSettings.StepsFine;
            });
            Assert.IsTrue(originalMin < originalMax);
            SetCoVParameters(covSettings, originalMax, originalMin, originalStepsRough, originalStepsMedium, originalStepsFine);
            var errorMinMax = ShowDialog<MessageDlg>(covSettings.OkDialog);
            Assert.AreEqual(Resources.EditCoVDlg_btnOk_Click_Maximum_compensation_voltage_cannot_be_less_than_minimum_compensation_volatage_, errorMinMax.Message);
            OkDialog(errorMinMax, errorMinMax.OkDialog);
            SetCoVParameters(covSettings, originalMin, originalMax, CompensationVoltageParameters.MIN_STEP_COUNT - 1, originalStepsMedium, originalStepsFine);
            var errorMinSteps = ShowDialog<MessageDlg>(covSettings.OkDialog);
            OkDialog(errorMinSteps, errorMinSteps.OkDialog);
            SetCoVParameters(covSettings, originalMin, originalMax, CompensationVoltageParameters.MAX_STEP_COUNT + 1, originalStepsMedium, originalStepsFine);
            var errorMaxSteps = ShowDialog<MessageDlg>(covSettings.OkDialog);
            OkDialog(errorMaxSteps, errorMaxSteps.OkDialog);
            SetCoVParameters(covSettings, originalMin, originalMax, originalStepsRough, originalStepsMedium, originalStepsFine);
            OkDialog(covSettings, covSettings.OkDialog);
            OkDialog(covList, covList.OkDialog);
            OkDialog(transitionSettings, transitionSettings.OkDialog);

            var dlgExportRoughTune = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            // Try to export without optimizing and check for error message
            RunUI(() =>
            {
                Assert.AreEqual(ExportInstrumentType.ABI, dlgExportRoughTune.InstrumentType);
                dlgExportRoughTune.OptimizeType = ExportOptimize.NONE;
            });
            var errorDlgNoCovs = ShowDialog<MultiButtonMsgDlg>(dlgExportRoughTune.OkDialog);
            RunUI(() => Assert.IsTrue(errorDlgNoCovs.Message.Contains(Resources.ExportMethodDlg_OkDialog_Your_document_does_not_contain_compensation_voltage_results__but_compensation_voltage_is_set_under_transition_settings_)));
            OkDialog(errorDlgNoCovs, errorDlgNoCovs.BtnCancelClick);

            // Try to export fine tune and check for error message
            RunUI(() => dlgExportRoughTune.OptimizeType = ExportOptimize.COV_FINE);
            var errorDlgFineExport = ShowDialog<MessageDlg>(dlgExportRoughTune.OkDialog);
            RunUI(() => Assert.IsTrue(errorDlgFineExport.Message.Contains(Resources.ExportMethodDlg_ValidateSettings_Cannot_export_fine_tune_transition_list__The_following_precursors_are_missing_medium_tune_results_)));
            OkDialog(errorDlgFineExport, errorDlgFineExport.OkDialog);

            // Try to export medium tune and check for error message
            RunUI(() => dlgExportRoughTune.OptimizeType = ExportOptimize.COV_MEDIUM);
            var errorDlgMediumExport = ShowDialog<MessageDlg>(dlgExportRoughTune.OkDialog);
            RunUI(() => Assert.IsTrue(errorDlgMediumExport.Message.Contains(Resources.ExportMethodDlg_ValidateSettings_Cannot_export_medium_tune_transition_list__The_following_precursors_are_missing_rough_tune_results_)));
            OkDialog(errorDlgMediumExport, errorDlgMediumExport.OkDialog);

            // Export rough tune and verify against expected results
            RunUI(() =>
            {
                dlgExportRoughTune.OptimizeType = ExportOptimize.COV_ROUGH;
                dlgExportRoughTune.OkDialog(outTransitionsRough);
            });
            CompareFiles(outTransitionsRough, expectedTransitionsRough);

            // Add a transition for which there are no results
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.Nodes[0].ExpandAll();
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0];
            });
            var pickList1 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                pickList1.ToggleFind();
                pickList1.SearchString = AsSmallMolecules ? "[M+2]" : "y4 ++";
                pickList1.SetItemChecked(0, true);
            });
            RunUI(pickList1.OnOk);
            // Try to export and check for warning about top ranked transition (will not occur after import results)
            var dlgExportNoTopRank = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            RunUI(() => dlgExportNoTopRank.OptimizeType = ExportOptimize.COV_ROUGH);
            var warnNoTopRank = ShowDialog<MultiButtonMsgDlg>(dlgExportNoTopRank.OkDialog);
            RunUI(() => Assert.IsTrue(warnNoTopRank.Message.Contains(
                Resources.ExportMethodDlg_OkDialog_Compensation_voltage_optimization_should_be_run_on_one_transition_per_peptide__and_the_best_transition_cannot_be_determined_for_the_following_precursors_)));
            OkDialog(warnNoTopRank, warnNoTopRank.BtnCancelClick);
            OkDialog(dlgExportNoTopRank, dlgExportNoTopRank.CancelDialog);

            // Import rough tune
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                Assert.IsFalse(importResultsDlg.CanOptimizeMedium);
                Assert.IsFalse(importResultsDlg.CanOptimizeFine);
                importResultsDlg.OptimizationName = ExportOptimize.COV_ROUGH;
                importResultsDlg.NamedPathSets = new[]
                {
                    new KeyValuePair<string, MsDataFileUri[]>("sMRM rough tune",
                        new MsDataFileUri[] {new MsDataFilePath(wiffFile, "sMRM rough tune", 2, LockMassParameters.EMPTY)})
                };
            });
            OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            doc = WaitForDocumentChangeLoaded(doc);

            // Paste in a peptide so we have missing results
            RunUI(() => SkylineWindow.Paste("PEPTIDER"));

            var dlgExportMediumTuneFail = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            // Try to export fine tune and check for error message
            RunUI(() => dlgExportMediumTuneFail.OptimizeType = ExportOptimize.COV_FINE);
            var errorDlgFineExport2 = ShowDialog<MessageDlg>(dlgExportMediumTuneFail.OkDialog);
            RunUI(() => Assert.IsTrue(errorDlgFineExport2.Message.Contains(Resources.ExportMethodDlg_ValidateSettings_Cannot_export_fine_tune_transition_list__The_following_precursors_are_missing_medium_tune_results_)));
            OkDialog(errorDlgFineExport2, errorDlgFineExport2.OkDialog);

            // Try to export medium tune and check for error message
            RunUI(() => dlgExportMediumTuneFail.OptimizeType = ExportOptimize.COV_MEDIUM);
            var errorDlgMediumExport2 = ShowDialog<MessageDlg>(dlgExportMediumTuneFail.OkDialog);
            RunUI(() => Assert.IsTrue(errorDlgMediumExport2.Message.Contains(Resources.ExportMethodDlg_ValidateSettings_Cannot_export_medium_tune_transition_list__The_following_precursors_are_missing_rough_tune_results_)));
            OkDialog(errorDlgMediumExport2, errorDlgMediumExport2.OkDialog);
            OkDialog(dlgExportMediumTuneFail, dlgExportMediumTuneFail.CancelDialog);

            // Undo the paste
            RunUI(() => SkylineWindow.Undo());

            var dlgExportMediumTune = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            // Try to export without optimizing and check for error message
            RunUI(() => dlgExportMediumTune.OptimizeType = ExportOptimize.NONE);
            var errorDlgMissingRoughCovs = ShowDialog<MultiButtonMsgDlg>(dlgExportMediumTune.OkDialog);
            RunUI(() => Assert.IsTrue(errorDlgMissingRoughCovs.Message.Contains(Resources.ExportMethodDlg_OkDialog_You_have_only_rough_tune_optimized_compensation_voltages_)));
            OkDialog(errorDlgMissingRoughCovs, errorDlgMissingRoughCovs.BtnCancelClick);

            // Retry export medium tune and verify against expected results
            RunUI(() =>
            {
                dlgExportMediumTune.OptimizeType = ExportOptimize.COV_MEDIUM;
                dlgExportMediumTune.OkDialog(outTransitionsMedium);
            });
            CompareFiles(outTransitionsMedium, expectedTransitionsMedium);

            // Import medium tune
            var importResultsDlgMedium = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                Assert.IsTrue(importResultsDlgMedium.CanOptimizeMedium);
                Assert.IsFalse(importResultsDlgMedium.CanOptimizeFine);
                importResultsDlgMedium.OptimizationName = ExportOptimize.COV_MEDIUM;
                importResultsDlgMedium.NamedPathSets = new[]
                {
                    new KeyValuePair<string, MsDataFileUri[]>("sMRM rmed tune",
                        new MsDataFileUri[] {new MsDataFilePath(wiffFile, "sMRM rmed tune", 3, LockMassParameters.EMPTY)})
                };
            });
            OkDialog(importResultsDlgMedium, importResultsDlgMedium.OkDialog);
            doc = WaitForDocumentChangeLoaded(doc);

            var dlgExportFineTune = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            // Try to export without optimizing and check for error message
            RunUI(() => dlgExportFineTune.OptimizeType = ExportOptimize.NONE);
            var errorDlgMissingMediumCovs = ShowDialog<MultiButtonMsgDlg>(dlgExportFineTune.OkDialog);
            RunUI(() => Assert.IsTrue(errorDlgMissingMediumCovs.Message.Contains(Resources.ExportMethodDlg_OkDialog_You_are_missing_fine_tune_optimized_compensation_voltages_)));
            OkDialog(errorDlgMissingMediumCovs, errorDlgMissingMediumCovs.BtnCancelClick);
            // Export fine tune and verify against expected results
            RunUI(() =>
            {
                dlgExportFineTune.OptimizeType = ExportOptimize.COV_FINE;
                dlgExportFineTune.OkDialog(outTransitionsFine);
            });
            CompareFiles(outTransitionsFine, expectedTransitionsFine);

            // Import fine tune
            var importResultsDlgFine = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                Assert.IsTrue(importResultsDlgFine.CanOptimizeMedium);
                Assert.IsTrue(importResultsDlgFine.CanOptimizeFine);
                importResultsDlgFine.OptimizationName = ExportOptimize.COV_FINE;
                importResultsDlgFine.NamedPathSets = new[]
                {
                    new KeyValuePair<string, MsDataFileUri[]>("sMRM fine tune",
                        new MsDataFileUri[] {new MsDataFilePath(wiffFile, "sMRM fine tune", 4, LockMassParameters.EMPTY)})
                };
            });
            OkDialog(importResultsDlgFine, importResultsDlgFine.OkDialog);
            WaitForDocumentChangeLoaded(doc);

            // Remove the transition we added earlier
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0]);
            var pickList2 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                pickList2.ToggleFind();
                pickList2.SearchString = AsSmallMolecules ? "[M+2]" : "y4 ++";
                pickList2.SetItemChecked(0, false);
            });
            RunUI(pickList2.OnOk);

            var dlgExportFinal = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            // Export and verify against expected results
            RunUI(() =>
            {
                dlgExportFinal.OptimizeType = ExportOptimize.NONE;
                dlgExportFinal.OkDialog(outTransitionsFinal);
            });
            CompareFiles(outTransitionsFinal, expectedTransitionsFinal);

            // Add new optimization library
            var dlgTransitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var dlgEditOptLib = ShowDialog<EditOptimizationLibraryDlg>(dlgTransitionSettings.AddToOptimizationLibraryList);

            string optLibPath = TestFilesDir.GetTestPath("cov.optdb");

            RunUI(() =>
            {
                dlgEditOptLib.CreateDatabase(optLibPath);
                dlgEditOptLib.LibName = "Test CoV library";
                Assert.AreEqual(ExportOptimize.CE, dlgEditOptLib.ViewType);
                dlgEditOptLib.ViewType = ExportOptimize.COV;
                Assert.AreEqual(ExportOptimize.COV, dlgEditOptLib.ViewType);
            });

            var dlgAddFromResults = ShowDialog<AddOptimizationsDlg>(dlgEditOptLib.AddResults);
            RunUI(() =>
            {
                Assert.AreEqual(5, dlgAddFromResults.OptimizationsCount);
                Assert.AreEqual(0, dlgAddFromResults.ExistingOptimizationsCount);
            });
            OkDialog(dlgAddFromResults, dlgAddFromResults.OkDialog);

            string pasteText = TextUtil.LineSeparate(
                GetPasteLine("GDFQFNISR", 2, 12.34),
                GetPasteLine("DVSLLHKPTTQISDFHVATR", 4, 23.45));
            RunUI(() =>
            {
                var libOptimizations = dlgEditOptLib.LibraryOptimizations;
                Assert.AreEqual(5, libOptimizations.Count);
                Assert.IsTrue(libOptimizations.Contains(GetDbOptimization(OptimizationType.compensation_voltage_fine, "FNDDFSR", GetAdduct(2), 17.00)));
                Assert.IsTrue(libOptimizations.Contains(GetDbOptimization(OptimizationType.compensation_voltage_fine, "GDFQFNISR", GetAdduct(2), 12.75)));
                Assert.IsTrue(libOptimizations.Contains(GetDbOptimization(OptimizationType.compensation_voltage_fine, "IDPNAWVER", GetAdduct(2), 12.50)));
                Assert.IsTrue(libOptimizations.Contains(GetDbOptimization(OptimizationType.compensation_voltage_fine, "TDRPSQQLR", GetAdduct(2), 14.00)));
                Assert.IsTrue(libOptimizations.Contains(GetDbOptimization(OptimizationType.compensation_voltage_fine, "DVSLLHKPTTQISDFHVATR", GetAdduct(4), 18.25)));
                dlgEditOptLib.SetOptimizations(new DbOptimization[0]);
                Assert.AreEqual(0, libOptimizations.Count);
                SetClipboardText(pasteText);
            });

            var addOptDlg = ShowDialog<AddOptimizationsDlg>(dlgEditOptLib.DoPasteLibrary);
            RunUI(() =>
            {
                Assert.AreEqual(2, addOptDlg.OptimizationsCount);
                Assert.AreEqual(0, addOptDlg.ExistingOptimizationsCount);
            });
            OkDialog(addOptDlg, addOptDlg.OkDialog);
            OkDialog(dlgEditOptLib, dlgEditOptLib.OkDialog);
            OkDialog(dlgTransitionSettings, dlgTransitionSettings.OkDialog);

            // Export with optimization library and verify against expected results
            var dlgExportFinal2 = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            RunUI(() =>
            {
                dlgExportFinal2.OptimizeType = ExportOptimize.NONE;                
            });
            OkDialog(dlgExportFinal2, () => dlgExportFinal2.OkDialog(outTransitionsFinalWithOptLib));
            CompareFiles(outTransitionsFinalWithOptLib, expectedTransitionsFinalWithOptLib);

            // Remove all results and export again, relying on the values in the library (3 values will be missing)
            RunUI(() => SkylineWindow.ModifyDocument("Remove results", document => document.ChangeMeasuredResults(null)));

            for (var loop = 2; loop-- > 0;)
            {
                var dlgExportFinal3 = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
                RunUI(() => dlgExportFinal3.OptimizeType = ExportOptimize.NONE);
                var lib2 = outTransitionsFinalWithOptLib2;
                var errorDlgMissingFineCovs = ShowDialog<MultiButtonMsgDlg>(() => dlgExportFinal3.OkDialog(lib2));
                RunUI(() => Assert.IsTrue(errorDlgMissingFineCovs.Message.Contains(Resources.ExportMethodDlg_OkDialog_You_are_missing_fine_tune_optimized_compensation_voltages_for_the_following_)));
                OkDialog(errorDlgMissingFineCovs, errorDlgMissingFineCovs.BtnYesClick);
                CompareFiles(outTransitionsFinalWithOptLib2, expectedTransitionsFinalWithOptLib2);

                RunUI(() => SkylineWindow.SaveDocument());

                // Try exporting with an explicitly set compensation voltage value and declustering potential
                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
                EnsureMixedTransitionListReport();
                EnableDocumentGridColumns(documentGrid, MIXED_TRANSITION_LIST_REPORT_NAME, 5,
                    new[]
                    {
                        "Proteins!*.Peptides!*.Precursors!*.ExplicitCompensationVoltage",
                        "Proteins!*.Peptides!*.Precursors!*.Transitions!*.ExplicitDeclusteringPotential", 
                    });
                const double explicitCV = 13.45;
                const double explicitDP = 14.32;
                var colCV = FindDocumentGridColumn(documentGrid, "Precursor.ExplicitCompensationVoltage");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colCV.Index].Value = explicitCV);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                                        SkylineWindow.Document.MoleculeTransitionGroups.First()
                                            .ExplicitValues.CompensationVoltage.Equals(explicitCV)));
                var colDP = FindDocumentGridColumn(documentGrid, "ExplicitDeclusteringPotential");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colDP.Index].Value = explicitDP);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitions.Any() &&
                                        SkylineWindow.Document.MoleculeTransitions.First()
                                            .ExplicitValues.DeclusteringPotential.Equals(explicitDP)));
                RunUI(() => documentGrid.Close());
                outTransitionsFinalWithOptLib2 = outTransitionsFinalWithOptLib2.Replace(".csv", "_explicit.csv");
                expectedTransitionsFinalWithOptLib2 = expectedTransitionsFinalWithOptLib2.Replace(".csv", "_explicit.csv");
            }
        }

        private DbOptimization GetDbOptimization(OptimizationType type, string sequence, Adduct adduct, double optValue)
        {
            var target = GetTarget(sequence);

            return new DbOptimization(type, target, adduct, null, Adduct.EMPTY, optValue);
        }

        private void CompareFiles(string fileActual, string fileExpected)
        {
            if (!AsSmallMolecules)
            {
                AssertEx.FileEquals(fileExpected, fileActual);
                return;
            }

            var textExpected = File.ReadAllText(fileExpected);
            var textActual = File.ReadAllText(fileActual);
            if (!textExpected.Contains(RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator))
            {
                textExpected = textExpected.Replace("peptides1.", "peptides1." + RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator).
                    Replace("+2", "[M+2H]").
                    Replace("+4", "[M+4H]").
                    Replace("y4.", "y4[M+].").
                    Replace("y5.", "y5[M+].").
                    Replace("y6.", "y6[M+].").
                    Replace("y7.", "y7[M+].").
                    Replace("y8.", "y8[M+].");
            }

            AssertEx.NoDiff(textExpected, textActual);
        }

        private Adduct GetAdduct(int charge)
        {
            return AsSmallMolecules
                ? Adduct.NonProteomicProtonatedFromCharge(charge)
                : Adduct.FromChargeProtonated(charge);
        }

        private string GetChargeIndicator(int charge)
        {
            var adduct = GetAdduct(charge);
            return AsSmallMolecules  
                ? adduct.ToString(CultureInfo.InvariantCulture)
                : Transition.GetChargeIndicator(adduct);
        }

        private Target GetTarget(string seq)
        {
            if (AsSmallMolecules)
            {
                var masscalc = new SequenceMassCalc(MassType.Monoisotopic);
                var moleculeFormula = masscalc.GetMolecularFormula(seq);
                var customMolecule = new CustomMolecule(moleculeFormula, 
                    RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator + seq.Replace(@"[", @"(").Replace(@"]", @")"));
                return new Target(customMolecule);
            }
            return new Target(seq);
        }

        private string GetPasteLine(string seq, int charge, string product, int productCharge, double ce)
        {
            if (AsSmallMolecules)
                seq = GetTarget(seq).DisplayName;
            var fields = new[]
            {
                string.Format(CultureInfo.CurrentCulture, "{0}{1}", seq, GetChargeIndicator(charge)),
                string.Format(CultureInfo.CurrentCulture, "{0}{1}", product, GetChargeIndicator(productCharge)),
                ce.ToString(CultureInfo.CurrentCulture)
            };
            return fields.ToDsvLine(TextUtil.SEPARATOR_TSV);
        }

        private string GetPasteLine(string seq, int charge, double cov)
        {
            if (AsSmallMolecules)
                seq = GetTarget(seq).ToSerializableString();
            var fields = new[]
            {
                string.Format(CultureInfo.CurrentCulture, "{0}{1}", seq, GetChargeIndicator(charge)),
                cov.ToString(CultureInfo.CurrentCulture)
            };
            return fields.ToDsvLine(TextUtil.SEPARATOR_TSV);
        }

        private void VerifyGraphs()
        {
            RunUI(SkylineWindow.ShowAllTransitions);
            WaitForGraphs();

            SrmDocument docCurrent = SkylineWindow.Document;
            int transitions = docCurrent.MoleculeTransitionCount / docCurrent.MoleculeTransitionGroupCount;
            foreach (var chromSet in docCurrent.Settings.MeasuredResults.Chromatograms)
                AssertEx.AreEqual(transitions, SkylineWindow.GetGraphChrom(chromSet.Name).CurveCount);
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
        private void ExportCEOptimizingTransitionList(string filePath)
        {
            FileEx.SafeDelete(filePath);

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

        private void VerifyCEOptimizingTransitionList(string filePath, SrmDocument document)
        {
            var regressionCE = document.Settings.TransitionSettings.Prediction.CollisionEnergy;
            double stepSize = regressionCE.StepSize;
            int stepCount = regressionCE.StepCount;
            stepCount = stepCount*2 + 1;

            string[] lines = File.ReadAllLines(filePath);
            Assert.AreEqual(document.MoleculeTransitionCount * stepCount, lines.Length);

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

            var optLib = document.Settings.TransitionSettings.Prediction.OptimizedLibrary;
            var optType = document.Settings.TransitionSettings.Prediction.OptimizedMethodType;
            bool precursorCE = (optType != OptimizedMethodType.Transition);

            bool diffCEFound = (fileCompare == null);
            bool diffTranFound = false;

            int iLine = 0;
            var dictLightCEs = new Dictionary<string, double>();
            foreach (PeptideGroupDocNode nodePepGroup in document.MoleculeGroups)
            {
                if (nodePepGroup.TransitionCount == 0)
                    continue;

                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
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
                                dictLightCEs[nodeTran.Transition.ToString()] = tranCE;
                            else
                                Assert.AreEqual(dictLightCEs[nodeTran.Transition.ToString()], tranCE);

                            if (optLib != null && !optLib.IsNone)
                            {
                                // If there is an optimized value, CE should be equal to it
                                DbOptimization optimization =
                                    optLib.GetOptimization(OptimizationType.collision_energy,
                                        document.Settings.GetSourceTarget(nodePep), nodeGroup.TransitionGroup.PrecursorAdduct,
                                        //nodeGroup.TransitionGroup.Peptide.Sequence, nodeGroup.TransitionGroup.PrecursorAdduct,
                                        nodeTran.FragmentIonName, nodeTran.Transition.Adduct);
                                if (optimization != null)
                                    Assert.AreEqual(optimization.Value, tranCE, 0.05);
                            }
                            else
                            {
                                // If precursor CE type, then all CEs should be equal
                                if (precursorCE && (optLib == null || optLib.IsNone))
                                    Assert.AreEqual(firstCE, tranCE);
                                else if (firstCE != tranCE)
                                    diffTranFound = true;
                            }
                        }
                    }
                }
            }
            Assert.IsTrue(diffCEFound);
            Assert.IsTrue(precursorCE || diffTranFound);
        }

        private static void SetCoVParameters(EditCoVDlg dlg, double? min, double? max, int? stepsRough, int? stepsMedium, int? stepsFine)
        {
            RunUI(() =>
            {
                dlg.Min = min;
                dlg.Max = max;
                dlg.StepsRough = stepsRough;
                dlg.StepsMedium = stepsMedium;
                dlg.StepsFine = stepsFine;
            });
        }
    }
}
