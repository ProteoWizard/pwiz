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
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for iRT Retention Time Prediction
    /// </summary>
    [TestClass]
    public class IrtTutorialTest : AbstractFunctionalTestEx
    {
        
        [TestMethod]
        public void TestIrtTutorial()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "iRT";

            ForceMzml = true;   // 2-3x faster than raw files for this test.

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/iRT-20_1.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.ms/tutorials/iRT.zip"
                    : @"https://skyline.ms/tutorials/iRTMzml.zip",
                @"TestTutorial\IrtViews.zip"
            };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            string folderIrt = UseRawFiles ? "iRT" : "iRTMzml"; // Not L10N
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderIrt, relativePath));
        }

        protected override void DoTest()
        {
            // iRT Retention Time Prediction
            string standardDocumentFile = GetTestPath("iRT-C18 Standard.sky"); // Not L10N
            RunUI(() => SkylineWindow.OpenFile(standardDocumentFile));
            WaitForDocumentLoaded(); // might have some updating to do for protein metadata
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath("iRT-C18 Calibrate.sky"))); // Not L10N

            // Load raw files for iRT calculator calibration p. 2 
            const string unschedHuman1Fileroot = "A_D110907_SiRT_HELA_11_nsMRM_150selected_1_30min-5-35"; // Not L10N
            string unschedHuman1Name = unschedHuman1Fileroot.Substring(41, 7);
            const string unschedHuman2Fileroot = "A_D110907_SiRT_HELA_11_nsMRM_150selected_2_30min-5-35"; // Not L10N
            string unschedHuman2Name = unschedHuman2Fileroot.Substring(41, 7);
            ImportNewResults(new[] { unschedHuman1Fileroot, unschedHuman2Fileroot }, 5, false, true, "Names form", 3);
            var docCalibrate = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            const int pepCount = 11, tranCount = 33;
            AssertEx.IsDocumentState(docCalibrate, null, 1, pepCount, pepCount, tranCount);
            AssertResult.IsDocumentResultsState(docCalibrate, unschedHuman1Name, pepCount, pepCount, 0, tranCount, 0);
            AssertResult.IsDocumentResultsState(docCalibrate, unschedHuman2Name, pepCount, pepCount, 0, tranCount, 0);

            RunUI(() =>
                      {
                          SkylineWindow.ArrangeGraphsTiled();
                          SkylineWindow.ShowRTPeptideGraph();
                          using (var enumLabels = SkylineWindow.RTGraphController.GraphSummary.Categories.GetEnumerator())
                          {
                              foreach (var nodePep in docCalibrate.Peptides)
                              {
                                  Assert.IsTrue(enumLabels.MoveNext() && enumLabels.Current != null);
                                  Assert.IsTrue(nodePep.Peptide.Sequence.StartsWith(enumLabels.Current.Substring(0, 3)));
                              }
                          }
                      });

            // Page 3.
            PauseForScreenShot<GraphSummary.RTGraphView>("RT graph metafile", 3);   // Peptide RT graph

            RunUI(() =>
                      {
                          SkylineWindow.ShowRTReplicateGraph();
                          SkylineWindow.AutoZoomBestPeak();
                          SkylineWindow.SelectedPath = docCalibrate.GetPathTo((int) SrmDocument.Level.Molecules, 0);
                          SkylineWindow.Size = new Size(914, 560);
                      });
            // Ensure graphs look like p. 5
            WaitForGraphs();

            RestoreViewOnScreen(5);
            PauseForScreenShot("Main window showing chromatograms and RT graph", 5);   // Skyline window with docked RT replicate comparison graph

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
                    SkylineWindow.SelectedPath = docCalibrate.GetPathTo((int)SrmDocument.Level.Molecules, iPeptide);
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

            // Calibrate a calculator p. 7
            const string irtCalcName = "iRT-C18";
            string irtCalcPath = GetTestPath(irtCalcName + IrtDb.EXT);
            const string irtCalcPathScreenShot = @"C:\Users\Brendan\Documents\iRT\iRT-C18.irtdb";
            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editIrtCalc1 = ShowDialog<EditIrtCalcDlg>(peptideSettingsUI1.AddCalculator);
            RunUI(() =>
                      {
                          editIrtCalc1.CalcName = irtCalcName;
                          editIrtCalc1.CreateDatabase(irtCalcPath); // Not L10N
                      });
            {
                var calibrateDlg = ShowDialog<CalibrateIrtDlg>(editIrtCalc1.Calibrate);
                RunUI(() =>
                {
                    calibrateDlg.StandardName = "Biognosys (30 min cal)";
                    calibrateDlg.UseResults();
                    Assert.AreEqual(11, calibrateDlg.StandardPeptideCount);
                    calibrateDlg.SetFixedPoints(1, 10);
                    for (int i = 0; i < calibrateDlg.StandardPeptideCount; i++)
                    {
                        Assert.AreEqual(listTimes[i], calibrateDlg.StandardPeptideList[i].RetentionTime, 0.2);
                    }
                });

                PauseForScreenShot<CalibrateIrtDlg>("Calibrate iRT Calculator form", 6);   // Calibrate iRT Calculator form

                RunDlg<GraphRegression>(calibrateDlg.GraphRegression, dlg => dlg.CloseDialog());
                RunDlg<GraphRegression>(calibrateDlg.GraphIrts, dlg => dlg.CloseDialog());

                RunUI(calibrateDlg.OkDialog);
            }
            Assert.IsTrue(WaitForConditionUI(() => editIrtCalc1.StandardPeptideCount == 11));

            if (IsPauseForScreenShots)
            {
                RunUI(() => editIrtCalc1.CalcPath = irtCalcPathScreenShot);
                PauseForScreenShot<EditIrtCalcDlg>("Edit iRT Calculator form", 7);   // Edit iRT Calculator form
                RunUI(() => editIrtCalc1.CalcPath = irtCalcPath);
            }

            // Check iRT values and update to defined values p. 7-8
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
                      });
            OkDialog(editIrtCalc1, editIrtCalc1.OkDialog);
            OkDialog(peptideSettingsUI1, peptideSettingsUI1.OkDialog);

            // Inspect RT regression graph p. 9
            RunUI(SkylineWindow.ShowRTRegressionGraphScoreToRun);
            WaitForRegression();

            RestoreViewOnScreen(9);
            PauseForScreenShot<GraphSummary.RTGraphView>("Retention Times Regression graph metafile", 9);   // RT Regression graph

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.09, 0.9991);

                          SkylineWindow.ShowSingleReplicate();
                          SkylineWindow.SequenceTree.Focus();   // If the focus is left on the results tab, then the next line does nothing
                          SkylineWindow.SelectedResultsIndex = 0;
                      });

            WaitForRegression();

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.15, 0.9991);

                          SkylineWindow.SelectedResultsIndex = 1;
                      });

            WaitForRegression();

            RunUI(() => VerifyRTRegression(0.15, 15.04, 0.9991));
            RunUI(() => SkylineWindow.ShowAverageReplicates());
            RunUI(() => SkylineWindow.SaveDocument());

            // Create a document containing human and standard peptides, p. 10
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("iRT Human.sky")));
            WaitForProteinMetadataBackgroundLoaderCompletedUI(); // let peptide metadata background loader do its work
            RunUI(() =>
                      {
                          SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
                          SkylineWindow.ImportFiles(standardDocumentFile);
                      });
            WaitForProteinMetadataBackgroundLoaderCompletedUI(); // let peptide metadata background loader do its work

            RestoreViewOnScreen(10);
            PauseForScreenShot("Targets tree clipped out of main window", 10);   // Target tree

            RunUI(() =>
                      {

                          Assert.AreEqual("iRT-C18 Standard Peptides", SkylineWindow.SelectedNode.Text); // Not L10N
                          Assert.AreEqual(1231, SkylineWindow.DocumentUI.PeptideTransitionCount);

                          SkylineWindow.SaveDocument(GetTestPath("iRT Human+Standard.sky")); // Not L10N
                          SkylineWindow.SaveDocument(GetTestPath("iRT Human+Standard Calibrate.sky")); // Not L10N
                      }); 

            // Remove heavy precursors, p. 11
            var docHumanAndStandard = SkylineWindow.Document;
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
                    {
                        refineDlg.RefineLabelType = IsotopeLabelType.heavy;
                        refineDlg.OkDialog();
                    });
            var docLightOnly = WaitForDocumentChange(docHumanAndStandard);
            Assert.AreEqual(632, docLightOnly.PeptideTransitionCount);

            // Create auto-calculate regression RT predictor, p. 11
            const string irtPredictorName = "iRT-C18"; // Not L10N
            {
                var docPre = SkylineWindow.Document;
                var peptideSettingsUI2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() => peptideSettingsUI2.ChooseRegression(irtPredictorName));
                var regressionDlg = ShowDialog<EditRTDlg>(peptideSettingsUI2.EditRegression);
                RunUI(() =>
                {
                    Assert.AreEqual(irtPredictorName, regressionDlg.Regression.Name);
                    Assert.AreEqual(irtCalcName, regressionDlg.Regression.Calculator.Name);
                    regressionDlg.SetAutoCalcRegression(true);
                    regressionDlg.SetTimeWindow(5);
                });

                PauseForScreenShot("Edit Retention Time Predictor form", 11);   // Edit retention time predictor form

                OkDialog(regressionDlg, regressionDlg.OkDialog);
                OkDialog(peptideSettingsUI1, peptideSettingsUI2.OkDialog);
                // Make sure iRT calculator is loaded
                WaitForDocumentChangeLoaded(docPre);
            }

            // Export unscheduled transition list, p. 12
            {
                const string calibrateBasename = "iRT Human+Standard Calibrate"; // Not L10N
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportMethodDlg.ExportStrategy = ExportStrategy.Buckets;
                    exportMethodDlg.IgnoreProteins = true;
                    exportMethodDlg.MaxTransitions = 335;
                });

                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List form", 12);

                RunUI(() => exportMethodDlg.OkDialog(GetTestPath(calibrateBasename + TextUtil.EXT_CSV)));
                WaitForClosedForm(exportMethodDlg);

                Assert.AreEqual(332, File.ReadAllLines(GetTestPath(calibrateBasename + "_0001.csv")).Length); // Not L10N
                Assert.AreEqual(333, File.ReadAllLines(GetTestPath(calibrateBasename + "_0002.csv")).Length); // Not L10N
            }

            // Import human peptide calibration results p. 13
            ImportNewResults(new[] { unschedHuman1Fileroot, unschedHuman2Fileroot }, -1, true);

            // Review iRT-C18 graph p. 13-14
            RunUI(() => SkylineWindow.ChooseCalculator(irtCalcName));
            RunUI(SkylineWindow.ShowRTRegressionGraphScoreToRun);
            WaitForRegression();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 14);

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.09, 0.9991);
                          Assert.AreEqual(11, SkylineWindow.DocumentUI.PeptideCount -
                              SkylineWindow.RTGraphController.Outliers.Length);
                      });

            // Find all unintegrated transitions, p. 14-15
            {
                var findDlg = ShowDialog<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg);
                RunUI(() =>
                {
                    findDlg.FindOptions = new FindOptions().ChangeText(string.Empty)
                        .ChangeCustomFinders(Finders.ListAllFinders().Where(f => f is UnintegratedTransitionFinder));
                });

                PauseForScreenShot<FindNodeDlg>("Find form", 15);

                RestoreViewOnScreen(15);

                RunUI(() =>
                {
                    findDlg.FindAll();
                    findDlg.Close();
                });
                WaitForClosedForm(findDlg);
            }

            PauseForScreenShot<FindResultsForm>("Find Results pane", 15);

            var findAllForm = WaitForOpenForm<FindResultsForm>();

            Assert.IsNotNull(findAllForm);
            const int expectedItems = 6;
            RunUI(() =>
                      {
                          Assert.AreEqual(expectedItems, findAllForm.ItemCount);

                          SkylineWindow.ShowAllTransitions();
                          SkylineWindow.AutoZoomBestPeak();
                          SkylineWindow.Size = new Size(657, 632);
                      });

            // Review peaks with missing transitions, p. 16
            for (int i = 0; i < expectedItems; i++)
            {
                int iItem = i;
                RunUI(() => findAllForm.ActivateItem(iItem));
                WaitForGraphs();
                RunUI(() => Assert.AreEqual((int)SequenceTree.StateImageId.no_peak,
                    SkylineWindow.SelectedNode.StateImageIndex));

                if (i == 1)
                {
                    PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile (1 of 2)", 16);   // Chromatogram graph
                }
                if (i == 2)
                {
                    PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile (2 of 2)", 16);
                }
                if (i == 3)
                {
                    RunUI(() =>
                    {
                        var document = SkylineWindow.DocumentUI;
                        var nodeGroup = document.FindNode(SkylineWindow.SelectedPath.Parent) as TransitionGroupDocNode;
                        Assert.IsNotNull(nodeGroup);
                        var nodeTran = document.FindNode(SkylineWindow.SelectedPath) as TransitionDocNode;
                        Assert.IsNotNull(nodeTran);
                        var graph = SkylineWindow.GetGraphChrom(Resources.ImportResultsDlg_DefaultNewName_Default_Name);
                        // New peak picking picks correct peak
                        Assert.AreEqual(19.8, graph.BestPeakTime.Value, 0.05);
//                        TransitionGroupDocNode nodeGroupGraph;
//                        TransitionDocNode nodeTranGraph;
//                        var scaledRT = graph.FindAnnotatedPeakRetentionTime(19.8, out nodeGroupGraph, out nodeTranGraph);
//                        Assert.AreSame(nodeGroup, nodeGroupGraph);
//                        Assert.AreNotSame(nodeTran, nodeTranGraph);
//                        Assert.AreEqual(7, nodeTranGraph.Transition.Ordinal);   // y7
//                        graph.FirePickedPeak(nodeGroupGraph, nodeTranGraph, scaledRT);
                    });
                }
                }

           // New peak picking picks correct peak
//            RunUI(() => findAllForm.ActivateItem(3));
//            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile (3 of 3)", 15);

            RunUI(SkylineWindow.ToggleIntegrateAll);
            RunUI(findAllForm.Close);
            WaitForClosedForm(findAllForm);

            RunUI(SkylineWindow.ShowRTRegressionGraphScoreToRun);

            // Calculate new iRT values for human peptides, p. 18
            {
                WaitForConditionUI(() =>
                    SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.score_to_run_regression) &&
                    SkylineWindow.RTGraphController.RegressionRefined != null);

                var editIrtCalc2 = ShowDialog<EditIrtCalcDlg>(SkylineWindow.ShowEditCalculatorDlg);
                RunUI(() => Assert.AreEqual(0, editIrtCalc2.LibraryPeptideCount));
                var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(editIrtCalc2.AddResults);

                PauseForScreenShot<AddIrtPeptidesDlg>("Add Peptides form", 17);

                RunUI(() =>
                {
                    Assert.AreEqual(148, addPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(2, addPeptidesDlg.RunsConvertedCount);
                    Assert.AreEqual(0, addPeptidesDlg.RunsFailedCount);
                });
                var recalibrateDlg = ShowDialog<MultiButtonMsgDlg>(addPeptidesDlg.OkDialog);
                OkDialog(recalibrateDlg, recalibrateDlg.Btn1Click);

                if (IsPauseForScreenShots)
                {
                    RunUI(() => editIrtCalc2.CalcPath = irtCalcPathScreenShot);
                    PauseForScreenShot<EditIrtCalcDlg>("Edit iRT Calculator form", 18);   // Edit iRT Calculator form
                    RunUI(() => editIrtCalc2.CalcPath = irtCalcPath);
                }

                RunUI(() => Assert.AreEqual(148, editIrtCalc2.LibraryPeptideCount));

                CommitIrtCalcChange(editIrtCalc2);
            }

            // Check the RT regression, p. 19
            WaitForRegression();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 19);

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.09, 0.99985);
                          Assert.AreEqual(0, SkylineWindow.RTGraphController.Outliers.Length);

                          SkylineWindow.SaveDocument();
                          SkylineWindow.HideFindResults();
                      });

            // Recalibrate method to 90-minute gradient, p. 20
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
                    peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                    peptideSettingsUI.ChooseRegression(irtPredictorName);
                    peptideSettingsUI.IsUseMeasuredRT = true;
                    peptideSettingsUI.TimeWindow = 5;
                });

                PauseForScreenShot<PeptideSettingsUI.PredictionTab>("Peptide Settings - Prediction tab", 21);

                RunUI(peptideSettingsUI.OkDialog);
                WaitForClosedForm(peptideSettingsUI);
            }

            // Import 90-minute standard mix run, p. 19
            const string unsched90MinFileroot = "A_D110913_SiRT_HELA_11_nsMRM_150selected_90min-5-40_TRID2215_01"; // Not L10N
            ImportNewResults(new[] { unsched90MinFileroot }, -1, false);
            WaitForGraphs();

            // Verify regression graph, p. 19
            RunUI(SkylineWindow.ShowRTRegressionGraphScoreToRun);
            WaitForRegression();
            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 22);
            RunUI(() =>
                      {
                          VerifyRTRegression(0.40, 24.77, 0.9998);
                          Assert.AreEqual(147, SkylineWindow.RTGraphController.Outliers.Length);
                      });

            // Check scheduling graph, p. 20
            RunUI(SkylineWindow.ShowRTSchedulingGraph);
            RunDlg<SchedulingGraphPropertyDlg>(() => SkylineWindow.ShowRTPropertyDlg(SkylineWindow.GraphRetentionTime), propertyDlg =>
                    {
                        propertyDlg.TimeWindows = new[] { 2.0, 5.0, 10.0 };
                        propertyDlg.OkDialog();
                    });
            WaitForGraphs();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Scheduling graph metafile", 23);

            // Export new 90-minute scheduled transition list, p. 22
            const string scheduledBasename = "iRT Human+Standard"; // Not L10N
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                    {
                        exportMethodDlg.ExportStrategy = ExportStrategy.Buckets;
                        exportMethodDlg.MaxTransitions = 265;
                        exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                    });

                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List form", 24);

                RunUI(() => exportMethodDlg.OkDialog(GetTestPath(scheduledBasename + TextUtil.EXT_CSV)));
                WaitForClosedForm(exportMethodDlg);
            }

            Assert.AreEqual(1223, File.ReadAllLines(GetTestPath(scheduledBasename + "_0001.csv")).Length); // Not L10N
            Assert.IsFalse(File.Exists(GetTestPath("iRT Human+Standard_0002.csv"))); // Not L10N

            // Import scheduled data, p. 23
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
                    {
                        manageResultsDlg.RemoveAllReplicates();
                        manageResultsDlg.OkDialog();
                    });

            RunUI(() => SkylineWindow.SaveDocument());
            const string sched90MinFileroot = "A_D110913_SiRT_HELA_11_sMRM_150selected_90min-5-40_SIMPLE"; // Not L10N
            ImportNewResults(new[] { sched90MinFileroot }, -1, false);

            RunUI(SkylineWindow.ShowRTRegressionGraphScoreToRun);
            WaitForRegression();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 26);

            // Review regression and outliers, p. 24
            RunUI(() =>
                      {
                          VerifyRTRegression(0.358, 25.920, 0.91606);
                          Assert.AreEqual(0, SkylineWindow.RTGraphController.Outliers.Length);
                      });

            RunDlg<RegressionRTThresholdDlg>(SkylineWindow.ShowRegressionRTThresholdDlg, thresholdDlg =>
                    {
                        thresholdDlg.Threshold = 0.998;
                        thresholdDlg.OkDialog();
                    });

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 27);

            // Verify 2 outliers highlighted and removed, p. 25
            WaitForConditionUI(() => SkylineWindow.RTGraphController.Outliers.Length == 2);
            RunUI(() =>
                      {
                          VerifyRTRegression(0.393, 24.85, 0.9989);

                          SkylineWindow.RemoveRTOutliers();
                          SkylineWindow.ShowPlotType(PlotTypeRT.residuals);
                      });
            WaitForRegression();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 28);

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 14;
                    Settings.Default.AreaFontSize = 14;
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                });

                RestoreCoverViewOnScreen();

                var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                RenameReplicate(manageResultsDlg, 0, "HELA_11_sMRM_150selected_90min");
                OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);

                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() =>
                {
                    peptideSettingsUI.Top = SkylineWindow.Top;
                    peptideSettingsUI.Left = SkylineWindow.Left - peptideSettingsUI.Width - 20;
                });
                var irtEditor = ShowDialog<EditIrtCalcDlg>(peptideSettingsUI.EditCalculator);
                RunUI(() =>
                {
                    irtEditor.Height = SkylineWindow.DockPanel.Height + 10;
                    irtEditor.Width += 25;
                    irtEditor.Top = SkylineWindow.Bottom - irtEditor.Height - SkylineWindow.StatusBarHeight - 5;
                    irtEditor.Left = SkylineWindow.Right - irtEditor.Width - 5;
                });
                TakeCoverShot();
                OkDialog(irtEditor, irtEditor.CancelDialog);
                OkDialog(peptideSettingsUI, peptideSettingsUI.CancelDialog);
                return;
            }

            // Check outlier removal, p. 25
            RunUI(() =>
                      {
                          VerifyRTRegression(0.393, 24.85, 0.9989);
                          Assert.AreEqual(0, SkylineWindow.RTGraphController.Outliers.Length);
                      });

            RunUI(() =>
            {
                SkylineWindow.ShowPlotType(PlotTypeRT.correlation);
                SkylineWindow.ShowGraphRetentionTime(false, GraphTypeSummary.score_to_run_regression);
                SkylineWindow.ShowGraphRetentionTime(false, GraphTypeSummary.schedule);
            });

            // Review a peak and its predicted retention time, p. 26
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findDlg =>
                    {
                        findDlg.FindOptions = new FindOptions().ChangeText("DATNVG"); // Not L10N
                        findDlg.FindNext();
                        findDlg.Close();
                    });
            WaitForGraphs();

            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile", 29);   // Chromatogram graph

            RunUI(() =>
                      {
                          var graphChrom = SkylineWindow.GetGraphChrom(sched90MinFileroot);
                          Assert.IsTrue(graphChrom.BestPeakTime.HasValue);
                          Assert.AreEqual(47.3, graphChrom.BestPeakTime.Value, 0.05);
                          Assert.IsTrue(graphChrom.PredictedRT.HasValue);
                          Assert.AreEqual(47.6, graphChrom.PredictedRT.Value, 0.05);
                      });

            RunUI(SkylineWindow.ShowRTRegressionGraphScoreToRun);

            // Import retention times from a spectral library, p. 31
            {
                WaitForConditionUI(() =>
                    SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.score_to_run_regression) &&
                    SkylineWindow.RTGraphController.RegressionRefined != null);

                var editIrtCalc = ShowDialog<EditIrtCalcDlg>(SkylineWindow.ShowEditCalculatorDlg);
                var addLibraryDlg = ShowDialog<AddIrtSpectralLibrary>(editIrtCalc.AddLibrary);
                RunUI(() =>
                          {
                              addLibraryDlg.Source = SpectralLibrarySource.file;
                              addLibraryDlg.FilePath = GetTestPath(Path.Combine("Yeast+Standard", // Not L10N
                                                                               "Yeast_iRT_C18_0_00001.blib")); // Not L10N
                              addLibraryDlg.FilePathFocus();
                          });

                PauseForScreenShot<AddIrtSpectralLibrary>("Add Spectral Library form", 31);

                // Verify converted peptide iRT values and OK dialogs, p. 31
                var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(addLibraryDlg.OkDialog);
                RunUI(() =>
                {
                    Assert.AreEqual(558, addPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(1, addPeptidesDlg.RunsConvertedCount);  // Libraries now convert through internal alignment to single RT scale
                    Assert.AreEqual(3, addPeptidesDlg.KeepPeptidesCount);
                });

                PauseForScreenShot<AddIrtPeptidesDlg>("Add Peptides form", 31);

                var recalibrateDlg = ShowDialog<MultiButtonMsgDlg>(addPeptidesDlg.OkDialog);
                OkDialog(recalibrateDlg, recalibrateDlg.Btn1Click);

                Assert.IsTrue(WaitForConditionUI(() => editIrtCalc.LibraryPeptideCount == 706));

                CommitIrtCalcChange(editIrtCalc);
            }

            // Inspect MS1 filtered Skyline file created from library DDA data, p. 32
            RunUI(() => SkylineWindow.OpenFile(GetTestPath(Path.Combine("Yeast+Standard", // Not L10N
                                                                        "Yeast+Standard (refined) - 2min.sky"))));
            WaitForDocumentLoaded();
            RunUI(() =>  SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.Molecules, 0));
            WaitForRegression();

            // Verify numbers that show up in the screenshot
            RunUI(() =>
                      {
                          // If the cache gets rebuilt, then because the chromatograms
                          // were minimized, the peak picking is not exactly the same
                          // using the minimized chromatograms.
                          VerifyRTRegression(0.3, 19.37, 0.9998);
                          var graphChrom = SkylineWindow.GetGraphChrom("Velos_2011_1110_RJ_16"); // Not L10N
                          Assert.AreEqual(37.6, graphChrom.RetentionMsMs[0], 0.05);
                          Assert.IsTrue(graphChrom.BestPeakTime.HasValue);
                          Assert.AreEqual(37.6, graphChrom.BestPeakTime.Value, 0.05);
                          graphChrom = SkylineWindow.GetGraphChrom("Velos_2011_1110_RJ_14"); // Not L10N
                          Assert.AreEqual(37.3, graphChrom.RetentionMsMs[0], 0.05);
                          Assert.AreEqual(37.6, graphChrom.RetentionMsMs[1], 0.05);
                          Assert.IsTrue(graphChrom.BestPeakTime.HasValue);
                          Assert.AreEqual(37.4, graphChrom.BestPeakTime.Value, 0.05);

                          SkylineWindow.Size = new Size(1250, 660);
                      });

            PauseForScreenShot("Main window", 33);

            // Add results and verify add dialog counts, p. 33
            {
                RunUI(() => SkylineWindow.Width = 500); // Make room for the form below

                var editIrtCalc = ShowDialog<EditIrtCalcDlg>(SkylineWindow.ShowEditCalculatorDlg);
                var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(editIrtCalc.AddResults);
                RunUI(() =>
                {
                    Assert.AreEqual(0, addPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(2, addPeptidesDlg.RunsConvertedCount);
                    Assert.AreEqual(558, addPeptidesDlg.OverwritePeptidesCount);
                    Assert.AreEqual(3, addPeptidesDlg.ExistingPeptidesCount);

                    addPeptidesDlg.Left = SkylineWindow.Right + 20;
                });

                PauseForScreenShot <AddIrtPeptidesDlg>("Add Peptides form", 34);

                var recalibrateDlg = ShowDialog<MultiButtonMsgDlg>(addPeptidesDlg.OkDialog);
                OkDialog(recalibrateDlg, recalibrateDlg.Btn1Click);
                RunUI(editIrtCalc.OkDialog);
                WaitForClosedForm(editIrtCalc);
            }

            RunUI(() => SkylineWindow.SaveDocument());
            RunUI(SkylineWindow.NewDocument);
        }

        private static void CommitIrtCalcChange(EditIrtCalcDlg editIrtCalc)
        {
            // TODO(brendanx): For now just allow audit logging to skip this operation until we can figure out how to handle it
            RunUI(() => SkylineWindow.AssumeNonNullModificationAuditLogging = false);

            using (new WaitDocumentChange())
            {
                OkDialog(editIrtCalc, editIrtCalc.OkDialog);
            }

            RunUI(() => SkylineWindow.AssumeNonNullModificationAuditLogging = true);
        }

        private void ImportNewResults(IEnumerable<string> baseNames, int suffixLength, bool multiFile,
            bool? removeFix = null, string pauseText = null, int? pausePage = null)
        {
            var listNamedPathSets = new List<KeyValuePair<string, MsDataFileUri[]>>();
            var listPaths = new List<string>();
            foreach (string baseName in baseNames)
            {
                string fileName = GetTestPath(baseName + ExtThermoRaw);
                if (multiFile)
                    listPaths.Add(fileName);
                else
                    listNamedPathSets.Add(new KeyValuePair<string, MsDataFileUri[]>(baseName, new[] { MsDataFileUri.Parse(fileName)}));
                }
            if (multiFile)
                listNamedPathSets.Add(new KeyValuePair<string, MsDataFileUri[]>(Resources.ImportResultsDlg_DefaultNewName_Default_Name, listPaths.Select(MsDataFileUri.Parse).ToArray()));

            using (new WaitDocumentChange())
            {
                var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                RunUI(() =>
                {
                    importResultsDlg.RadioAddNewChecked = true;
                    importResultsDlg.NamedPathSets = listNamedPathSets.ToArray();
                });
                if (removeFix.HasValue)
                {
                    var resultsNames = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
                    RunUI(() =>
                    {
                        if (suffixLength != -1)
                            resultsNames.Suffix = resultsNames.Suffix.Substring(resultsNames.Suffix.Length - suffixLength);
                    });

                    if (pauseText != null && pausePage.HasValue)
                        PauseForScreenShot<ImportResultsNameDlg>(pauseText, pausePage.Value);

                        if (removeFix.Value)
                        OkDialog(resultsNames, resultsNames.YesDialog);
                        else
                        OkDialog(resultsNames, resultsNames.NoDialog);
                }
                else
                {
                    OkDialog(importResultsDlg, importResultsDlg.OkDialog);
                }
            }
            WaitForDocumentLoaded(5 * 60 * 1000);    // 5 minutes
            WaitForClosedAllChromatogramsGraph();
        }

        // Always called in RunUI
        private static void VerifyRTRegression(double slope, double intercept, double r)
        {
            WaitForCondition(() => SkylineWindow.RTGraphController.RegressionRefined != null);
            var regressionRT = (RegressionLineElement) SkylineWindow.RTGraphController.RegressionRefined.Conversion;
            Assert.AreEqual(slope, regressionRT.Slope, 0.005);
            Assert.AreEqual(intercept, regressionRT.Intercept, 0.005);
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