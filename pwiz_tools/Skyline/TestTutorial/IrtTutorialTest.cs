/*
 * Original author: Brendan MacLean <bmaclean .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Collision Energy Optimization
    /// </summary>
    [TestClass]
    public class IrtTutorialTest : AbstractFunctionalTest
    {
        
        [TestMethod]
        public void TestIrtTutorial()
        {
            TestFilesZip = ExtensionTestContext.CanImportThermoRaw ?  @"https://skyline.gs.washington.edu/tutorials/iRT.zip"
                               : @"https://skyline.gs.washington.edu/tutorials/iRTMzml.zip"; // Not L10N
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            string folderIrt = ExtensionTestContext.CanImportThermoRaw ? "iRT" : "iRTMzml"; // Not L10N
            return TestFilesDir.GetTestPath(Path.Combine(folderIrt, relativePath));
        }

        protected override void DoTest()
        {
            // Skyline Collision Energy Optimization
            string standardDocumentFile = GetTestPath("iRT-C18 Standard.sky"); // Not L10N
            RunUI(() => SkylineWindow.OpenFile(standardDocumentFile));
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath("iRT-C18 Calibrate.sky"))); // Not L10N

            // Load raw files for iRT calculator calibration p. 2 
            const string unschedHuman1Fileroot = "A_D110907_SiRT_HELA_11_nsMRM_150selected_1_30min-5-35"; // Not L10N
            string unschedHuman1Name = unschedHuman1Fileroot.Substring(41);
            const string unschedHuman2Fileroot = "A_D110907_SiRT_HELA_11_nsMRM_150selected_2_30min-5-35"; // Not L10N
            string unschedHuman2Name = unschedHuman2Fileroot.Substring(41);
            ImportNewResults(new[] { unschedHuman1Fileroot, unschedHuman2Fileroot }, 41, false);
            var docCalibrate = SkylineWindow.Document;
            const int pepCount = 11, tranCount = 33;
            AssertEx.IsDocumentState(docCalibrate, null, 1, pepCount, pepCount, tranCount);
            AssertResult.IsDocumentResultsState(docCalibrate, unschedHuman1Name, pepCount, pepCount, 0, tranCount, 0);
            AssertResult.IsDocumentResultsState(docCalibrate, unschedHuman2Name, pepCount, pepCount, 0, tranCount, 0);

            RunUI(() =>
                      {
                          SkylineWindow.ArrangeGraphsTiled();
                          SkylineWindow.ShowRTPeptideGraph();
                          var enumLabels = SkylineWindow.RTGraphController.GraphSummary.Categories.GetEnumerator();
                          foreach (var nodePep in docCalibrate.Peptides)
                          {
                              Assert.IsTrue(enumLabels.MoveNext() && enumLabels.Current != null);
                              Assert.IsTrue(nodePep.Peptide.Sequence.StartsWith(enumLabels.Current));
                          }
                      });

            // Page 3.
            PauseForScreenShot();   // Peptide RT graph

            RunUI(() =>
                      {
                          SkylineWindow.ShowRTReplicateGraph();
                          SkylineWindow.AutoZoomBestPeak();
                          SkylineWindow.SelectedPath = docCalibrate.GetPathTo((int) SrmDocument.Level.Peptides, 0);
                      });
            // Ensure graphs look like p. 3 and 4
            WaitForGraphs();

            PauseForScreenShot();   // Skyline window with docked RT replicate comparison graph

            RunUI(() =>
                      {
                          Assert.AreEqual(3, SkylineWindow.RTGraphController.GraphSummary.CurveCount);
                          Assert.AreEqual(2, SkylineWindow.RTGraphController.GraphSummary.Categories.Count());
                          var chromGraphs = SkylineWindow.GraphChromatograms.ToArray();
                          Assert.AreEqual(2, chromGraphs.Length);
                          Assert.AreEqual(11.3, chromGraphs[0].GraphItems.First().BestPeakTime, 0.05);
                          Assert.AreEqual(11.2, chromGraphs[1].GraphItems.First().BestPeakTime, 0.05);                          
                      });

            var listTimes = new List<double>();
            for (int i = 0; i < docCalibrate.PeptideCount; i++)
            {
                int iPeptide = i;
                RunUI(() =>
                {
                    SkylineWindow.SelectedPath = docCalibrate.GetPathTo((int)SrmDocument.Level.Peptides, iPeptide);
                });
                WaitForGraphs();
                RunUI(() =>
                          {
                              var chromGraphs = SkylineWindow.GraphChromatograms.ToArray();
                              double time1 = chromGraphs[0].GraphItems.First(g => g.BestPeakTime > 0).BestPeakTime;
                              double time2 = chromGraphs[1].GraphItems.First(g => g.BestPeakTime > 0).BestPeakTime;
                              listTimes.Add((time1 + time2)/2);
                              Assert.AreEqual(time1, time2, 0.2);
                          });
            }

            // Calibrate a calculator p. 4-5
            const string irtCalcName = "iRT-C18";
            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editIrtCalc1 = ShowDialog<EditIrtCalcDlg>(peptideSettingsUI1.AddCalculator);
            RunUI(() =>
                      {
                          editIrtCalc1.CalcName = irtCalcName;
                          editIrtCalc1.CreateDatabase(GetTestPath("iRT-C18.irtdb")); // Not L10N
                      });
            {
                var calibrateDlg = ShowDialog<CalibrateIrtDlg>(editIrtCalc1.Calibrate);
                RunUI(() =>
                {
                    calibrateDlg.UseResults();
                    Assert.AreEqual(11, calibrateDlg.StandardPeptideCount);
                    calibrateDlg.SetFixedPoints(1, 10);
                    for (int i = 0; i < calibrateDlg.StandardPeptideCount; i++)
                    {
                        Assert.AreEqual(listTimes[i], calibrateDlg.StandardPeptideList[i].RetentionTime, 0.2);
                    }
                });

                PauseForScreenShot();   // Calibrate iRT Calculator form

                RunUI(calibrateDlg.OkDialog);
            }
            Assert.IsTrue(WaitForConditionUI(() => editIrtCalc1.StandardPeptideCount == 11));

            PauseForScreenShot();   // Edit iRT Caclulator form

            // Check iRT values and update to defined values p. 6-7
            var irtDefinitionPath = GetTestPath("iRT definition.xlsx"); // Not L10N
            string irtDefText = GetExcelFileText(irtDefinitionPath, "iRT-C18", 2, true); // Not L10N

            RunUI(() =>
                      {
                          var standardPeptidesArray = editIrtCalc1.StandardPeptides.ToArray();
                          Assert.AreEqual(11, standardPeptidesArray.Length);
                          Assert.AreEqual(0, standardPeptidesArray[1].Irt, 0.00001);
                          Assert.AreEqual(100, standardPeptidesArray[10].Irt, 0.00001);
                          CheckIrtStandardPeptides(standardPeptidesArray, irtDefText, 6);

                          SetClipboardText(irtDefText);
                          editIrtCalc1.DoPasteStandard();

                          standardPeptidesArray = editIrtCalc1.StandardPeptides.ToArray();
                          Assert.AreEqual(11, standardPeptidesArray.Length);
                          Assert.AreEqual(0, standardPeptidesArray[1].Irt, 0.001);
                          Assert.AreEqual(100, standardPeptidesArray[10].Irt, 0.005);
                          CheckIrtStandardPeptides(standardPeptidesArray, irtDefText, 0.00001);

                          editIrtCalc1.OkDialog();
                          peptideSettingsUI1.OkDialog();
                      });
            WaitForClosedForm(editIrtCalc1);
            WaitForClosedForm(peptideSettingsUI1);

            // Inspect RT regression graph p. 8
            RunUI(SkylineWindow.ShowRTLinearRegressionGraph);
            WaitForGraphs();

            PauseForScreenShot();   // RT Regression graph

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.09, 0.9991);

                          SkylineWindow.ShowSingleReplicate();
                          SkylineWindow.SelectedResultsIndex = 0;
                      });

            WaitForGraphs();

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.15, 0.9991);

                          SkylineWindow.SelectedResultsIndex = 1;
                      });

            WaitForGraphs();

            RunUI(() => VerifyRTRegression(0.15, 15.04, 0.9991));
            RunUI(() => SkylineWindow.ShowAverageReplicates());
            RunUI(() => SkylineWindow.SaveDocument());

            // Create a document containing human and standard peptides, p. 9
            RunUI(() =>
                      {
                          SkylineWindow.OpenFile(GetTestPath("iRT Human.sky")); // Not L10N
                          SkylineWindow.SelectedPath = new IdentityPath(SequenceTree.NODE_INSERT_ID);
                          SkylineWindow.ImportFiles(standardDocumentFile);
                      });

            PauseForScreenShot();   // Target tree

            RunUI(() =>
                      {

                          Assert.AreEqual("iRT-C18 Standard Peptides", SkylineWindow.SelectedNode.Text); // Not L10N
                          Assert.AreEqual(1231, SkylineWindow.DocumentUI.TransitionCount);

                          SkylineWindow.SaveDocument(GetTestPath("iRT Human+Standard.sky")); // Not L10N
                          SkylineWindow.SaveDocument(GetTestPath("iRT Human+Standard Calibrate.sky")); // Not L10N
                      }); 

            // Remove heavy precursors, p. 10
            var docHumanAndStandard = SkylineWindow.Document;
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
                    {
                        refineDlg.RefineLabelType = IsotopeLabelType.heavy;
                        refineDlg.OkDialog();
                    });
            var docLightOnly = WaitForDocumentChange(docHumanAndStandard);
            Assert.AreEqual(632, docLightOnly.TransitionCount);

            // Create auto-calculate regression RT predictor, p. 10
            const string irtPredictorName = "iRT-C18"; // Not L10N
            {
                var peptideSettingsUI2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                var regressionDlg = ShowDialog<EditRTDlg>(peptideSettingsUI2.AddRTRegression);
                RunUI(() =>
                {
                    regressionDlg.SetRegressionName(irtPredictorName);
                    regressionDlg.ChooseCalculator(irtCalcName);
                    regressionDlg.SetAutoCalcRegression(true);
                    regressionDlg.SetTimeWindow(5);
                });

                PauseForScreenShot();   // Edit retention time predictor form

                RunUI(regressionDlg.OkDialog);
                WaitForClosedForm(regressionDlg);
                RunUI(peptideSettingsUI2.OkDialog);
                WaitForClosedForm(peptideSettingsUI2);
            }

            // Export unscheduled transition list, p. 11
            {
                const string calibrateBasename = "iRT Human+Standard Calibrate"; // Not L10N
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportMethodDlg.ExportStrategy = ExportStrategy.Buckets;
                    exportMethodDlg.IgnoreProteins = true;
                    exportMethodDlg.MaxTransitions = 335;
                });

                PauseForScreenShot();   // Export Transition List form

                RunUI(() => exportMethodDlg.OkDialog(GetTestPath(calibrateBasename + TextUtil.EXT_CSV)));
                WaitForClosedForm(exportMethodDlg);

                Assert.AreEqual(332, File.ReadAllLines(GetTestPath(calibrateBasename + "_0001.csv")).Length); // Not L10N
                Assert.AreEqual(333, File.ReadAllLines(GetTestPath(calibrateBasename + "_0002.csv")).Length); // Not L10N
            }

            // Import human peptide calibration results p. 12
            ImportNewResults(new[] { unschedHuman1Fileroot, unschedHuman2Fileroot }, -1, true);

            // Review iRT-C18 graph p. 12-13
            RunUI(() => SkylineWindow.ChooseCalculator(irtCalcName));
            WaitForGraphs();

            PauseForScreenShot();   // RT Regression graph

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.09, 0.9991);
                          Assert.AreEqual(11, SkylineWindow.DocumentUI.PeptideCount -
                              SkylineWindow.RTGraphController.Outliers.Length);
                      });

            // Find all unintegrated transitions, p. 13-14
            {
                var findDlg = ShowDialog<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg);
                RunUI(() =>
                {
                    findDlg.FindOptions = new FindOptions().ChangeText(string.Empty)
                        .ChangeCustomFinders(Finders.ListAllFinders().Where(f => f is UnintegratedTransitionFinder));
                });

                PauseForScreenShot();   // Find form

                RunUI(() =>
                {
                    findDlg.FindAll();
                    findDlg.Close();
                });
                WaitForClosedForm(findDlg);
            }

            PauseForScreenShot();   // Find Results pane

            var findAllForm = FindOpenForm<FindResultsForm>();
            Assert.IsNotNull(findAllForm);
            const int expectedItems = 6;
            RunUI(() =>
                      {
                          Assert.AreEqual(expectedItems, findAllForm.ItemCount);

                          SkylineWindow.ShowAllTransitions();
                          SkylineWindow.AutoZoomBestPeak();
                      });

            // Review peaks with missing transitions, p. 15
            for (int i = 0; i < expectedItems; i++)
            {
                int iItem = i;
                RunUI(() => findAllForm.ActivateItem(iItem));
                WaitForGraphs();
                RunUI(() => Assert.AreEqual((int)SequenceTree.StateImageId.no_peak,
                    SkylineWindow.SelectedNode.StateImageIndex));

                if (i == 3)
                {
                    PauseForScreenShot();   // Chromatogram graph
                    RunUI(() =>
                    {
                        var document = SkylineWindow.DocumentUI;
                        var nodeGroup = document.FindNode(SkylineWindow.SelectedPath.Parent) as TransitionGroupDocNode;
                        Assert.IsNotNull(nodeGroup);
                        var nodeTran = document.FindNode(SkylineWindow.SelectedPath) as TransitionDocNode;
                        Assert.IsNotNull(nodeTran);
                        SkylineWindow.GetGraphChrom(MULTI_FILE_REPLICATE_NAME)
                                     .FirePickedPeak(nodeGroup, nodeTran, new ScaledRetentionTime(19.8, 19.8));
                    });
                }
                if (i == 5)
                {
                    PauseForScreenShot();   // Chromatogram graph
                    RunUI(() =>
                              {
                                  SkylineWindow.SelectedPath = SkylineWindow.SelectedPath.Parent.Parent;
                                  SkylineWindow.EditDelete();
                              });
                }
            }
            RunUI(() => findAllForm.ActivateItem(3));

            PauseForScreenShot();   // Chromatogram graph

            RunUI(findAllForm.Close);
            WaitForClosedForm(findAllForm);

            // Calculate new iRT values for human peptides, p. 16
            {
                var editIrtCalc2 = ShowDialog<EditIrtCalcDlg>(SkylineWindow.ShowEditCalculatorDlg);
                RunUI(() => Assert.AreEqual(0, editIrtCalc2.LibraryPeptideCount));
                var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(editIrtCalc2.AddResults);

                PauseForScreenShot();   // Add Peptides form

                RunUI(() =>
                {
                    Assert.AreEqual(147, addPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(2, addPeptidesDlg.RunsConvertedCount);
                    Assert.AreEqual(0, addPeptidesDlg.RunsFailedCount);
                    addPeptidesDlg.OkDialog();
                });
                WaitForClosedForm(addPeptidesDlg);

                PauseForScreenShot();   // Edit iRT Calculator form

                RunUI(() => Assert.AreEqual(147, editIrtCalc2.LibraryPeptideCount));
                RunUI(editIrtCalc2.OkDialog);
                WaitForClosedForm(editIrtCalc2);
            }

            // Check the RT regression, p. 17
            WaitForGraphs();

            PauseForScreenShot();   // RT Regression graph

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.09, 0.9999);
                          Assert.AreEqual(0, SkylineWindow.RTGraphController.Outliers.Length);

                          SkylineWindow.SaveDocument();
                          SkylineWindow.HideFindResults();
                      });

            // Recalibrate method to 90-minute gradient, p. 18
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("iRT Human+Standard.sky"))); // Not L10N
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findDlg =>
                    {
                        findDlg.FindOptions = new FindOptions().ChangeText("NSAQ"); // Not L10N
                        findDlg.FindNext();
                        findDlg.Close();
                    });
            RunUI(SkylineWindow.EditDelete);

            {
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() =>
                {
                    peptideSettingsUI.ChooseRegression(irtPredictorName);
                    peptideSettingsUI.UseMeasuredRT(true);
                    peptideSettingsUI.TimeWindow = 5;
                });

                PauseForScreenShot();

                RunUI(peptideSettingsUI.OkDialog);
                WaitForClosedForm(peptideSettingsUI);
            }

            // Import 90-minute standard mix run, p. 19
            const string unsched90MinFileroot = "A_D110913_SiRT_HELA_11_nsMRM_150selected_90min-5-40_TRID2215_01"; // Not L10N
            ImportNewResults(new[] { unsched90MinFileroot }, -1, false);
            WaitForGraphs();

            // Verify regression graph, p. 20
            PauseForScreenShot();   // RT Regression graph
            RunUI(() =>
                      {
                          VerifyRTRegression(0.40, 24.77, 0.9998);
                          Assert.AreEqual(147, SkylineWindow.RTGraphController.Outliers.Length);
                      });

            // Check scheduling graph, p. 21
            RunUI(SkylineWindow.ShowRTSchedulingGraph);
            RunDlg<SchedulingGraphPropertyDlg>(SkylineWindow.ShowRTPropertyDlg, propertyDlg =>
                    {
                        propertyDlg.TimeWindows = new[] { 2.0, 5.0, 10.0 };
                        propertyDlg.OkDialog();
                    });
            WaitForGraphs();

            PauseForScreenShot();   // RT Scheduling graph

            // Export new 90-minute scheduled transition list, p. 22
            const string scheduledBasename = "iRT Human+Standard"; // Not L10N
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                    {
                        exportMethodDlg.ExportStrategy = ExportStrategy.Buckets;
                        exportMethodDlg.MaxTransitions = 260;
                        exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                    });

                PauseForScreenShot();   // Export Transition List form

                RunUI(() => exportMethodDlg.OkDialog(GetTestPath(scheduledBasename + TextUtil.EXT_CSV)));
                WaitForClosedForm(exportMethodDlg);
            }

            Assert.AreEqual(1223, File.ReadAllLines(GetTestPath(scheduledBasename + "_0001.csv")).Length); // Not L10N
            Assert.IsFalse(File.Exists(GetTestPath("iRT Human+Standard_0002.csv"))); // Not L10N

            // Import scheduled data, p. 23
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
                    {
                        manageResultsDlg.RemoveAll();
                        manageResultsDlg.OkDialog();
                    });

            RunUI(() => SkylineWindow.SaveDocument());
            const string sched90MinFileroot = "A_D110913_SiRT_HELA_11_sMRM_150selected_90min-5-40_SIMPLE"; // Not L10N
            ImportNewResults(new[] { sched90MinFileroot }, -1, false);

            RunUI(SkylineWindow.ShowRTLinearRegressionGraph);
            WaitForGraphs();

            PauseForScreenShot();   // RT Regression graph

            // Review regression and outliers, p. 24
            RunUI(() =>
                      {
                          VerifyRTRegression(0.38, 24.95, 0.9528);
                          Assert.AreEqual(1, SkylineWindow.RTGraphController.Outliers.Length);
                      });

            RunDlg<RegressionRTThresholdDlg>(SkylineWindow.ShowRegressionRTThresholdDlg, thresholdDlg =>
                    {
                        thresholdDlg.Threshold = 0.998;
                        thresholdDlg.OkDialog();
                    });

            PauseForScreenShot();   // RT Regression graph

            // Verify 6 outliers highlighed and removed, p. 25
            WaitForConditionUI(() => SkylineWindow.RTGraphController.Outliers.Length == 6);
            RunUI(() =>
                      {
                          VerifyRTRegression(0.39, 24.83, 0.9987);

                          SkylineWindow.RemoveRTOutliers();
                      });
            WaitForGraphs();

            PauseForScreenShot();   // RT Regression graph

            // Check outlier removal, p. 26
            RunUI(() =>
                      {
                          VerifyRTRegression(0.39, 24.83, 0.9987);
                          Assert.AreEqual(0, SkylineWindow.RTGraphController.Outliers.Length);
                      });

            // Review a peak and its predicted retention time, p. 27
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findDlg =>
                    {
                        findDlg.FindOptions = new FindOptions().ChangeText("DATNVG"); // Not L10N
                        findDlg.FindNext();
                        findDlg.Close();
                    });
            WaitForGraphs();

            PauseForScreenShot();   // Chromatogram graph

            RunUI(() =>
                      {
                          var graphChrom = SkylineWindow.GetGraphChrom(sched90MinFileroot);
                          Assert.IsTrue(graphChrom.BestPeakTime.HasValue);
                          Assert.AreEqual(47.3, graphChrom.BestPeakTime.Value, 0.05);
                          Assert.IsTrue(graphChrom.PredictedRT.HasValue);
                          Assert.AreEqual(47.6, graphChrom.PredictedRT.Value, 0.05);
                      });

            // Import retention times from a spectral library, p. 28
            {
                var editIrtCalc = ShowDialog<EditIrtCalcDlg>(SkylineWindow.ShowEditCalculatorDlg);
                var addLibrayDlg = ShowDialog<AddIrtSpectralLibrary>(editIrtCalc.AddLibrary);
                RunUI(() =>
                          {
                              addLibrayDlg.Source = SpectralLibrarySource.file;
                              addLibrayDlg.FilePath = GetTestPath(Path.Combine("Yeast+Standard", // Not L10N
                                                                               "Yeast_iRT_C18_0_00001.blib")); // Not L10N
                          });

                PauseForScreenShot();   // Add Spectral Library form
            
                // Verify converted peptide iRT values and OK dialogs, p. 29
                var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(addLibrayDlg.OkDialog);
                RunUI(() =>
                {
                    Assert.AreEqual(558, addPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(2, addPeptidesDlg.RunsConvertedCount);
                    Assert.AreEqual(3, addPeptidesDlg.KeepPeptidesCount);
                });

                PauseForScreenShot();   // Add Peptides form

                RunUI(addPeptidesDlg.OkDialog);
                WaitForClosedForm(addPeptidesDlg);
                RunUI(addLibrayDlg.OkDialog);
                WaitForClosedForm(addLibrayDlg);

                Assert.IsTrue(WaitForConditionUI(() => editIrtCalc.LibraryPeptideCount == 705));
                RunUI(editIrtCalc.OkDialog);
                WaitForClosedForm(editIrtCalc);
            }

            // Inspect MS1 filtered Skyline file created from library DDA data, p. 29
            RunUI(() => SkylineWindow.OpenFile(GetTestPath(Path.Combine("Yeast+Standard", // Not L10N
                                                                        "Yeast+Standard (refined) - 2min.sky"))));
            WaitForDocumentLoaded();
            RunUI(() =>  SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.Peptides, 0));
            WaitForGraphs();

            // Verify numbers that show up in the screenshot
            RunUI(() =>
                      {
                          // If the cache gets rebuilt, then because the chromatograms
                          // were minimized, the peak picking is not exactly the same
                          // using the minimized chromatograms.
                          VerifyRTRegression(0.3, 19.47, 0.9998);
                          var graphChrom = SkylineWindow.GetGraphChrom("Velos_2011_1110_RJ_16"); // Not L10N
                          Assert.AreEqual(37.6, graphChrom.RetentionMsMs[0], 0.05);
                          Assert.IsTrue(graphChrom.BestPeakTime.HasValue);
                          Assert.AreEqual(37.6, graphChrom.BestPeakTime.Value, 0.05);
                          graphChrom = SkylineWindow.GetGraphChrom("Velos_2011_1110_RJ_14"); // Not L10N
                          Assert.AreEqual(37.3, graphChrom.RetentionMsMs[0], 0.05);
                          Assert.AreEqual(37.6, graphChrom.RetentionMsMs[1], 0.05);
                          Assert.IsTrue(graphChrom.BestPeakTime.HasValue);
                          Assert.AreEqual(37.4, graphChrom.BestPeakTime.Value, 0.05);
                      });

            PauseForScreenShot();   // Skyline window

            // Add results and verify add dialog counts, p. 29-30
            {
                var editIrtCalc = ShowDialog<EditIrtCalcDlg>(SkylineWindow.ShowEditCalculatorDlg);
                var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(editIrtCalc.AddResults);
                RunUI(() =>
                {
                    Assert.AreEqual(0, addPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(2, addPeptidesDlg.RunsConvertedCount);
                    Assert.AreEqual(558, addPeptidesDlg.OverwritePeptidesCount);
                    Assert.AreEqual(3, addPeptidesDlg.ExistingPeptidesCount);
                });

                PauseForScreenShot();   // Add Peptides form

                RunUI(addPeptidesDlg.OkDialog);
                WaitForClosedForm(addPeptidesDlg);
                RunUI(editIrtCalc.OkDialog);
                WaitForClosedForm(editIrtCalc);
            }

            RunUI(SkylineWindow.NewDocument);
        }

        private const string MULTI_FILE_REPLICATE_NAME = "Chromatograms"; // Not L10N

        private void ImportNewResults(IEnumerable<string> baseNames, int suffixLength, bool multiFile)
        {
            var listNamedPathSets = new List<KeyValuePair<string, string[]>>();
            var listPaths = new List<string>();
            foreach (string baseName in baseNames)
            {
                string fileName = TestFilesDir.GetTestPath(baseName + ExtensionTestContext.ExtThermoRaw);
                if (multiFile)
                    listPaths.Add(fileName);
                else
                {
                    string replicateName = suffixLength != -1 ? baseName.Substring(suffixLength) : baseName;
                    listNamedPathSets.Add(new KeyValuePair<string, string[]>(replicateName, new[] {fileName}));
                }
            }
            if (multiFile)
                listNamedPathSets.Add(new KeyValuePair<string, string[]>(MULTI_FILE_REPLICATE_NAME, listPaths.ToArray()));

            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                importResultsDlg.NamedPathSets = listNamedPathSets.ToArray();
                importResultsDlg.OkDialog();
            });
            WaitForConditionUI(5 * 60 * 1000, () => SkylineWindow.DocumentUI.Settings.HasResults &&
                SkylineWindow.DocumentUI.Settings.MeasuredResults.IsLoaded);    // 5 minutes
        }

        private static void VerifyRTRegression(double slope, double intercept, double r)
        {
            Assert.AreEqual(slope, SkylineWindow.RTGraphController.RegressionRefined.Conversion.Slope, 0.005);
            Assert.AreEqual(intercept, SkylineWindow.RTGraphController.RegressionRefined.Conversion.Intercept, 0.005);
            Assert.AreEqual(r, SkylineWindow.RTGraphController.StatisticsRefined.R, 0.00005);
        }

        private static void CheckIrtStandardPeptides(DbIrtPeptide[] standardPeptidesArray, string irtDefText, double threshold)
        {
            int iRow = 0;
            var irtDefReader = new StringReader(irtDefText);
            string line;
            while ((line = irtDefReader.ReadLine()) != null)
            {
                Assert.AreEqual(standardPeptidesArray[iRow].Irt, double.Parse(line.Split(TextUtil.SEPARATOR_TSV)[1]), threshold);
                iRow++;
            }
        }
    }
}