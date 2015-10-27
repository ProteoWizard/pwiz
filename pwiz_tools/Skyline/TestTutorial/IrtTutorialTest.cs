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
    public class IrtTutorialTest : AbstractFunctionalTest
    {
        
        [TestMethod]
        public void TestIrtTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            ForceMzml = true;   // 2-3x faster than raw files for this test.

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/iRT-1_4.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.gs.washington.edu/tutorials/iRT.zip"
                    : @"https://skyline.gs.washington.edu/tutorials/iRTMzml.zip",
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
            string unschedHuman1Name = unschedHuman1Fileroot.Substring(41);
            const string unschedHuman2Fileroot = "A_D110907_SiRT_HELA_11_nsMRM_150selected_2_30min-5-35"; // Not L10N
            string unschedHuman2Name = unschedHuman2Fileroot.Substring(41);
            ImportNewResults(new[] { unschedHuman1Fileroot, unschedHuman2Fileroot }, 41, false);
            var docCalibrate = WaitForProteinMetadataBackgroundLoaderCompletedUI();
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
                              Assert.IsTrue(nodePep.Peptide.Sequence.StartsWith(enumLabels.Current.Substring(0, 3)));
                          }
                      });

            // Page 3.
            PauseForScreenShot<GraphSummary.RTGraphView>("RT graph metafile", 3);   // Peptide RT graph

            RunUI(() =>
                      {
                          SkylineWindow.ShowRTReplicateGraph();
                          SkylineWindow.AutoZoomBestPeak();
                          SkylineWindow.SelectedPath = docCalibrate.GetPathTo((int) SrmDocument.Level.Molecules, 0);
                      });
            // Ensure graphs look like p. 3 and 4
            WaitForGraphs();

            RestoreViewOnScreen(04);
            PauseForScreenShot("Main window showing chromatograms and RT graph", 4);   // Skyline window with docked RT replicate comparison graph

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

                PauseForScreenShot<CalibrateIrtDlg>("Calibrate iRT Calculator form", 5);   // Calibrate iRT Calculator form

                RunUI(calibrateDlg.OkDialog);
            }
            Assert.IsTrue(WaitForConditionUI(() => editIrtCalc1.StandardPeptideCount == 11));

            PauseForScreenShot<EditIrtCalcDlg>("Edit iRT Calculater form", 6);   // Edit iRT Caclulator form

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

            RestoreViewOnScreen(08);
            PauseForScreenShot<GraphSummary.RTGraphView>("Retention Times Regression graph metafile", 8);   // RT Regression graph

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.09, 0.9991);

                          SkylineWindow.ShowSingleReplicate();
                          SkylineWindow.SequenceTree.Focus();   // If the focus is left on the results tab, then the next line does nothing
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
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("iRT Human.sky")));
            WaitForProteinMetadataBackgroundLoaderCompletedUI(); // let peptide metadata background loader do its work
            RunUI(() =>
                      {
                          SkylineWindow.SelectedPath = new IdentityPath(SequenceTree.NODE_INSERT_ID);
                          SkylineWindow.ImportFiles(standardDocumentFile);
                      });
            WaitForProteinMetadataBackgroundLoaderCompletedUI(); // let peptide metadata background loader do its work

            RestoreViewOnScreen(09);
            PauseForScreenShot("Targets tree clipped out of main winodw", 9);   // Target tree

            RunUI(() =>
                      {

                          Assert.AreEqual("iRT-C18 Standard Peptides", SkylineWindow.SelectedNode.Text); // Not L10N
                          Assert.AreEqual(1231, SkylineWindow.DocumentUI.PeptideTransitionCount);

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
            Assert.AreEqual(632, docLightOnly.PeptideTransitionCount);

            // Create auto-calculate regression RT predictor, p. 10
            const string irtPredictorName = "iRT-C18"; // Not L10N
            {
                var docPre = SkylineWindow.Document;
                var peptideSettingsUI2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                var regressionDlg = ShowDialog<EditRTDlg>(peptideSettingsUI2.AddRTRegression);
                RunUI(() =>
                {
                    regressionDlg.SetRegressionName(irtPredictorName);
                    regressionDlg.ChooseCalculator(irtCalcName);
                    regressionDlg.SetAutoCalcRegression(true);
                    regressionDlg.SetTimeWindow(5);
                });

                PauseForScreenShot("Edit Retention Time Predictor form", 10);   // Edit retention time predictor form

                OkDialog(regressionDlg, regressionDlg.OkDialog);
                OkDialog(peptideSettingsUI1, peptideSettingsUI2.OkDialog);
                // Make sure iRT calculator is loaded
                WaitForDocumentChangeLoaded(docPre);
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

                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List form", 11);

                RunUI(() => exportMethodDlg.OkDialog(GetTestPath(calibrateBasename + TextUtil.EXT_CSV)));
                WaitForClosedForm(exportMethodDlg);

                Assert.AreEqual(332, File.ReadAllLines(GetTestPath(calibrateBasename + "_0001.csv")).Length); // Not L10N
                Assert.AreEqual(333 + (TestSmallMolecules ? 2 : 0), File.ReadAllLines(GetTestPath(calibrateBasename + "_0002.csv")).Length); // Not L10N
            }

            // Import human peptide calibration results p. 12
            ImportNewResults(new[] { unschedHuman1Fileroot, unschedHuman2Fileroot }, -1, true);

            // Review iRT-C18 graph p. 12-13
            RunUI(() => SkylineWindow.ChooseCalculator(irtCalcName));
            WaitForGraphs();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 13);

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

                PauseForScreenShot<FindNodeDlg>("Find form", 14);

                RestoreViewOnScreen(15);

                RunUI(() =>
                {
                    findDlg.FindAll();
                    findDlg.Close();
                });
                WaitForClosedForm(findDlg);
            }

            PauseForScreenShot<FindResultsForm>("Find Results pane", 14);

            var findAllForm = WaitForOpenForm<FindResultsForm>();

            Assert.IsNotNull(findAllForm);
            const int expectedItems = 8;
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

                if (i == 2)
                {
                    PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile (1 of 2)", 15);
                    
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
                if (i == 4)
                {
                    PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile (2 of 2)", 15);   // Chromatogram graph
                }
            }

           // New peak picking picks correct peak
//            RunUI(() => findAllForm.ActivateItem(3));
//            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile (3 of 3)", 15);

            RunUI(SkylineWindow.ToggleIntegrateAll);
            RunUI(findAllForm.Close);
            WaitForClosedForm(findAllForm);

            RestoreViewOnScreen(17);

            // Calculate new iRT values for human peptides, p. 16
            {
                var editIrtCalc2 = ShowDialog<EditIrtCalcDlg>(SkylineWindow.ShowEditCalculatorDlg);
                RunUI(() => Assert.AreEqual(0, editIrtCalc2.LibraryPeptideCount));
                var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(editIrtCalc2.AddResults);

                PauseForScreenShot<AddIrtPeptidesDlg>("Add Peptides form", 15);

                RunUI(() =>
                {
                    Assert.AreEqual(148, addPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(2, addPeptidesDlg.RunsConvertedCount);
                    Assert.AreEqual(0, addPeptidesDlg.RunsFailedCount);
                    addPeptidesDlg.OkDialog();
                });
                WaitForClosedForm(addPeptidesDlg);

                PauseForScreenShot<EditIrtCalcDlg>("Edit iRT Calculator form", 16);

                RunUI(() => Assert.AreEqual(148, editIrtCalc2.LibraryPeptideCount));
                RunUI(editIrtCalc2.OkDialog);
                WaitForClosedForm(editIrtCalc2);
            }

            // Check the RT regression, p. 17
            WaitForGraphs();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 17);

            RunUI(() =>
                      {
                          VerifyRTRegression(0.15, 15.09, 0.99985);
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
                    peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                    peptideSettingsUI.ChooseRegression(irtPredictorName);
                    peptideSettingsUI.IsUseMeasuredRT = true;
                    peptideSettingsUI.TimeWindow = 5;
                });

                PauseForScreenShot<PeptideSettingsUI.PredictionTab>("Peptide Settings - Prediction tab", 18);

                RunUI(peptideSettingsUI.OkDialog);
                WaitForClosedForm(peptideSettingsUI);
            }

            // Import 90-minute standard mix run, p. 19
            const string unsched90MinFileroot = "A_D110913_SiRT_HELA_11_nsMRM_150selected_90min-5-40_TRID2215_01"; // Not L10N
            ImportNewResults(new[] { unsched90MinFileroot }, -1, false);
            WaitForGraphs();

            // Verify regression graph, p. 19
            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 19);
            RunUI(() =>
                      {
                          VerifyRTRegression(0.40, 24.77, 0.9998);
                          Assert.AreEqual(147, SkylineWindow.RTGraphController.Outliers.Length);
                      });

            // Check scheduling graph, p. 20
            RunUI(SkylineWindow.ShowRTSchedulingGraph);
            RunDlg<SchedulingGraphPropertyDlg>(SkylineWindow.ShowRTPropertyDlg, propertyDlg =>
                    {
                        propertyDlg.TimeWindows = new[] { 2.0, 5.0, 10.0 };
                        propertyDlg.OkDialog();
                    });
            WaitForGraphs();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Scheduling graph metafile", 20);

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

                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List form", 21);

                RunUI(() => exportMethodDlg.OkDialog(GetTestPath(scheduledBasename + TextUtil.EXT_CSV)));
                WaitForClosedForm(exportMethodDlg);
            }

            Assert.AreEqual(1223 + (TestSmallMolecules ? 4 : 0), File.ReadAllLines(GetTestPath(scheduledBasename + "_0001.csv")).Length); // Not L10N
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

            RunUI(SkylineWindow.ShowRTLinearRegressionGraph);
            WaitForGraphs();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 23);

            // Review regression and outliers, p. 24
            RunUI(() =>
                      {
                          VerifyRTRegression(0.358, 25.920, 0.9162);
                          Assert.AreEqual(0, SkylineWindow.RTGraphController.Outliers.Length);
                      });

            RunDlg<RegressionRTThresholdDlg>(SkylineWindow.ShowRegressionRTThresholdDlg, thresholdDlg =>
                    {
                        thresholdDlg.Threshold = 0.998;
                        thresholdDlg.OkDialog();
                    });

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 24);

            // Verify 2 outliers highlighed and removed, p. 25
            WaitForConditionUI(() => SkylineWindow.RTGraphController.Outliers.Length == 2);
            RunUI(() =>
                      {
                          VerifyRTRegression(0.393, 24.85, 0.9989);

                          SkylineWindow.RemoveRTOutliers();
                      });
            WaitForGraphs();

            PauseForScreenShot<GraphSummary.RTGraphView>("RT Regression graph metafile", 25);

            // Check outlier removal, p. 25
            RunUI(() =>
                      {
                          VerifyRTRegression(0.393, 24.85, 0.9989);
                          Assert.AreEqual(0, SkylineWindow.RTGraphController.Outliers.Length);
                      });

            // Review a peak and its predicted retention time, p. 26
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findDlg =>
                    {
                        findDlg.FindOptions = new FindOptions().ChangeText("DATNVG"); // Not L10N
                        findDlg.FindNext();
                        findDlg.Close();
                    });
            WaitForGraphs();

            RestoreViewOnScreen(27);
            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile", 26);   // Chromatogram graph

            RunUI(() =>
                      {
                          var graphChrom = SkylineWindow.GetGraphChrom(sched90MinFileroot);
                          Assert.IsTrue(graphChrom.BestPeakTime.HasValue);
                          Assert.AreEqual(47.3, graphChrom.BestPeakTime.Value, 0.05);
                          Assert.IsTrue(graphChrom.PredictedRT.HasValue);
                          Assert.AreEqual(47.6, graphChrom.PredictedRT.Value, 0.05);
                      });

            // Import retention times from a spectral library, p. 27
            RestoreViewOnScreen(17); // get regression graph back
            {
                var editIrtCalc = ShowDialog<EditIrtCalcDlg>(SkylineWindow.ShowEditCalculatorDlg);
                var addLibrayDlg = ShowDialog<AddIrtSpectralLibrary>(editIrtCalc.AddLibrary);
                RunUI(() =>
                          {
                              addLibrayDlg.Source = SpectralLibrarySource.file;
                              addLibrayDlg.FilePath = GetTestPath(Path.Combine("Yeast+Standard", // Not L10N
                                                                               "Yeast_iRT_C18_0_00001.blib")); // Not L10N
                          });

                PauseForScreenShot<AddIrtSpectralLibrary>("Add Spectral Library form", 27);
            
                // Verify converted peptide iRT values and OK dialogs, p. 28
                var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(addLibrayDlg.OkDialog);
                RunUI(() =>
                {
                    Assert.AreEqual(558, addPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(2, addPeptidesDlg.RunsConvertedCount);
                    Assert.AreEqual(3, addPeptidesDlg.KeepPeptidesCount);
                });

                PauseForScreenShot<AddIrtPeptidesDlg>("Add Peptides form", 28);

                RunUI(addPeptidesDlg.OkDialog);
                WaitForClosedForm(addPeptidesDlg);
                RunUI(addLibrayDlg.OkDialog);
                WaitForClosedForm(addLibrayDlg);

                Assert.IsTrue(WaitForConditionUI(() => editIrtCalc.LibraryPeptideCount == 706));
                RunUI(editIrtCalc.OkDialog);
                WaitForClosedForm(editIrtCalc);
            }

            // Inspect MS1 filtered Skyline file created from library DDA data, p. 29
            RunUI(() => SkylineWindow.OpenFile(GetTestPath(Path.Combine("Yeast+Standard", // Not L10N
                                                                        "Yeast+Standard (refined) - 2min.sky"))));
            WaitForDocumentLoaded();
            RunUI(() =>  SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.Molecules, 0));
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

            PauseForScreenShot("Main window", 29);

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

                PauseForScreenShot <AddIrtPeptidesDlg>("Add Peptides form", 30);

                RunUI(addPeptidesDlg.OkDialog);
                WaitForClosedForm(addPeptidesDlg);
                RunUI(editIrtCalc.OkDialog);
                WaitForClosedForm(editIrtCalc);
            }

            RunUI(() => SkylineWindow.SaveDocument());
            RunUI(SkylineWindow.NewDocument);
        }

        private void ImportNewResults(IEnumerable<string> baseNames, int suffixLength, bool multiFile)
        {
            var listNamedPathSets = new List<KeyValuePair<string, MsDataFileUri[]>>();
            var listPaths = new List<string>();
            foreach (string baseName in baseNames)
            {
                string fileName = GetTestPath(baseName + ExtThermoRaw);
                if (multiFile)
                    listPaths.Add(fileName);
                else
                {
                    string replicateName = suffixLength != -1 ? baseName.Substring(suffixLength) : baseName;
                    listNamedPathSets.Add(new KeyValuePair<string, MsDataFileUri[]>(replicateName, new[] { MsDataFileUri.Parse(fileName)}));
                }
            }
            if (multiFile)
                listNamedPathSets.Add(new KeyValuePair<string, MsDataFileUri[]>(Resources.ImportResultsDlg_DefaultNewName_Default_Name, listPaths.Select(MsDataFileUri.Parse).ToArray()));

            using (new WaitDocumentChange())
            {
                RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
                {
                    importResultsDlg.RadioAddNewChecked = true;
                    importResultsDlg.NamedPathSets = listNamedPathSets.ToArray();
                    importResultsDlg.OkDialog();
                });
            }
            WaitForDocumentLoaded(5 * 60 * 1000);    // 5 minutes
        }

        private static void VerifyRTRegression(double slope, double intercept, double r)
        {
            WaitForCondition(() => SkylineWindow.RTGraphController.RegressionRefined != null);
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