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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
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
            TestFilesZip = ExtensionTestContext.CanImportThermoRaw ?  @"https://brendanx-uw1.gs.washington.edu/tutorials/iRT.zip"
                               : @"https://brendanx-uw1.gs.washington.edu/tutorials/iRTMzml.zip";
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            string folderIrt = ExtensionTestContext.CanImportThermoRaw ? "iRT" : "iRTMzml";
            return TestFilesDir.GetTestPath(Path.Combine(folderIrt, relativePath));
        }

        protected override void DoTest()
        {
            // Skyline Collision Energy Optimization
            string standardDocumentFile = GetTestPath("iRT-C18 Standard.sky");
            RunUI(() => SkylineWindow.OpenFile(standardDocumentFile));
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath("iRT-C18 Calibrate.sky")));

            // Load raw files for iRT calculator calibration p. 2
            const string unschedHuman1Fileroot = "A_D110907_SiRT_HELA_11_nsMRM_150selected_1_30min-5-35";
            string unschedHuman1Name = unschedHuman1Fileroot.Substring(41);
            const string unschedHuman2Fileroot = "A_D110907_SiRT_HELA_11_nsMRM_150selected_2_30min-5-35";
            string unschedHuman2Name = unschedHuman2Fileroot.Substring(41);
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                var namedPathSets = new[]
                {
                    new KeyValuePair<string, string[]>(unschedHuman1Name, new[] {
                            TestFilesDir.GetTestPath(unschedHuman1Fileroot + ExtensionTestContext.ExtThermoRaw)}),
                    new KeyValuePair<string, string[]>(unschedHuman2Name, new[] {
                            TestFilesDir.GetTestPath(unschedHuman2Fileroot + ExtensionTestContext.ExtThermoRaw)}),
                };
                importResultsDlg.NamedPathSets = namedPathSets;
                importResultsDlg.OkDialog();
            });
            WaitForCondition(120 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
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
                          SkylineWindow.ShowRTReplicateGraph();
                          SkylineWindow.AutoZoomBestPeak();
                          SkylineWindow.SelectedPath = docCalibrate.GetPathTo((int) SrmDocument.Level.Peptides, 0);
                      });
            // Ensure graphs look like p. 3 and 4
            WaitForGraphs();
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
            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editIrtCalc = ShowDialog<EditIrtCalcDlg>(peptideSettingsUI1.AddCalculator);
            RunUI(() =>
                      {
                          editIrtCalc.CalcName = "iRT-C18";
                          editIrtCalc.CreateDatabase(TestContext.GetTestPath("iRT-C18.irtdb"));
                      });
            RunDlg<CalibrateIrtDlg>(editIrtCalc.Calibrate, calibrateDlg =>
                                                               {
                                                                   calibrateDlg.UseResults();
                                                                   Assert.AreEqual(11, calibrateDlg.StandardPeptideCount);
                                                                   calibrateDlg.SetFixedPoints(1, 10);
                                                                   for (int i = 0; i < calibrateDlg.StandardPeptideCount; i++)
                                                                   {
                                                                       Assert.AreEqual(listTimes[i], calibrateDlg.StandardPeptideList[i].RetentionTime, 0.2);
                                                                   }
                                                                   calibrateDlg.OkDialog();
                                                               });

            // Check iRT values and update to defined values p. 6-7
            var irtDefinitionPath = GetTestPath(@"iRT definition.xlsx");
            string irtDefText = GetExcelFileText(irtDefinitionPath, "iRT-C18", 2, true);

            RunUI(() =>
                      {
                          var standardPeptidesArray = editIrtCalc.StandardPeptides.ToArray();
                          Assert.AreEqual(11, standardPeptidesArray.Length);
                          Assert.AreEqual(0, standardPeptidesArray[1].Irt, 0.00001);
                          Assert.AreEqual(100, standardPeptidesArray[10].Irt, 0.00001);
                          CheckIrtStandardPeptides(standardPeptidesArray, irtDefText, 6);

                          SetClipboardText(irtDefText);
                          editIrtCalc.DoPasteStandard();

                          standardPeptidesArray = editIrtCalc.StandardPeptides.ToArray();
                          Assert.AreEqual(11, standardPeptidesArray.Length);
                          Assert.AreEqual(0, standardPeptidesArray[1].Irt, 0.001);
                          Assert.AreEqual(100, standardPeptidesArray[10].Irt, 0.005);
                          CheckIrtStandardPeptides(standardPeptidesArray, irtDefText, 0.00001);

                          editIrtCalc.OkDialog();
                          peptideSettingsUI1.OkDialog();
                      });
            WaitForClosedForm(editIrtCalc);
            WaitForClosedForm(peptideSettingsUI1);

            // Inspect RT regression graph p. 8
            RunUI(SkylineWindow.ShowRTLinearRegressionGraph);
            WaitForGraphs();
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

            RunUI(() => SkylineWindow.SaveDocument());

            // Create a document containing human and standard peptides, p. 9
            RunUI(() =>
                      {
                          SkylineWindow.OpenFile(GetTestPath("iRT Human.sky"));
                          SkylineWindow.SelectedPath = new IdentityPath(SequenceTree.NODE_INSERT_ID);
                          SkylineWindow.ImportFiles(standardDocumentFile);

                          Assert.AreEqual("iRT-C18 Standard Peptides", SkylineWindow.SelectedNode.Text);
                          Assert.AreEqual(1231, SkylineWindow.DocumentUI.TransitionCount);

                          SkylineWindow.SaveDocument(GetTestPath("iRT Human+Standard.sky"));
                          SkylineWindow.SaveDocument(GetTestPath("iRT Human+Standard Calibrate.sky"));
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
            var peptideSettingsUI2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunDlg<EditRTDlg>(peptideSettingsUI2.AddRTRegression, regressionDlg =>
                    {
                        regressionDlg.SetRegressionName("iRT-C18");
                        regressionDlg.ChooseCalculator("iRT-C18");
                        regressionDlg.SetAutoCalcRegression(true);
                        regressionDlg.SetTimeWindow(5);
                        regressionDlg.OkDialog();
                    });
            RunUI(peptideSettingsUI2.OkDialog);
            WaitForClosedForm(peptideSettingsUI2);

            // Export unscheduled transition list, p. 11
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List),
                exportMethodDlg =>
                    {
                        exportMethodDlg.ExportStrategy = ExportStrategy.Buckets;
                        exportMethodDlg.IgnoreProteins = true;
                        exportMethodDlg.MaxTransitions = 335;
                        exportMethodDlg.OkDialog(GetTestPath("iRT Human+Standard.csv"));
                    });
            Assert.AreEqual(332, File.ReadAllLines(GetTestPath("iRT Human+Standard_0001.csv")).Length);
            Assert.AreEqual(333, File.ReadAllLines(GetTestPath("iRT Human+Standard_0002.csv")).Length);
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
                Assert.AreEqual(standardPeptidesArray[iRow].Irt, double.Parse(line.Split('\t')[1]), threshold);
                iRow++;
            }
        }
    }
}