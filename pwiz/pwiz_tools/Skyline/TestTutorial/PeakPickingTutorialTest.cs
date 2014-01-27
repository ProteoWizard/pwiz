/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class PeakPickingTutorialTest : AbstractFunctionalTest
    {
        private readonly string[] _importFiles =
            {
                "olgas_S130501_006_StC-DosR_B2",
                "olgas_S130501_007_StC-DosR_C2",
                "olgas_S130501_008_StC-DosR_A4",
                "olgas_S130501_009_StC-DosR_B4",
                "olgas_S130501_010_StC-DosR_C4"
            };

        [TestMethod]
        public void TestPeakPickingTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            TestFilesZipPaths = new[]
                {
                    ExtensionTestContext.CanImportAbWiff
                        ? @"https://skyline.gs.washington.edu/tutorials/PeakPicking.zip"
                        : @"https://skyline.gs.washington.edu/tutorials/PeakPickingMzml.zip",
                    @"TestTutorial\PeakPickingViews.zip"
                };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            var folderTutorial = ExtensionTestContext.CanImportAbWiff ? "PeakPicking" : "PeakPickingMzml"; // Not L10N
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderTutorial, relativePath));
        }

        /// <summary>
        /// Change to true to write coefficient arrays
        /// </summary>
        private bool IsRecordMode { get { return false; } }

        private readonly string[] EXPECTED_COEFFICIENTS =
        {
            "0.5416|-2.2482|0.5928|2.2079|0.3654|0.0594|0.1669|-0.0539| null |0.2563|7.6497|-0.0968|0.5192| null | null | null | null ", // Not L10N
            "0.2865| null | null | null |5.4170|-0.0291|0.6770|1.1543| null | null | null | null | null | null | null | null | null ", // Not L10N
        };

        protected override void DoTest()
        {
            Settings.Default.PeakScoringModelList.Clear();

            // Open the file
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("SRMCourse_DosR-hDP__20130501-tutorial-empty.sky"))); // Not L10N
            WaitForDocumentLoaded();

            // Add decoys
            var generateDecoysDlg = ShowDialog<GenerateDecoysDlg>(() => SkylineWindow.ShowGenerateDecoysDlg());
            RunUI(() =>
            {
                generateDecoysDlg.DecoysMethod = DecoyGeneration.REVERSE_SEQUENCE;
                generateDecoysDlg.NumDecoys = 29;
            });
            PauseForScreenShot("p2 - decoy dialog");
            
            RunUI(generateDecoysDlg.OkDialog);
            WaitForClosedForm(generateDecoysDlg);

            PauseForScreenShot("p3 - main window");

            // Open the file with decoys
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("SRMCourse_DosR-hDP__20130501-tutorial-empty-decoys.sky"))); // Not L10N
            WaitForDocumentLoaded();

            // Import the raw data
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                var path = new KeyValuePair<string, string[]>[5];
                for (int i = 0; i < 5; ++i)
                {
                    path[i] = new KeyValuePair<string, string[]>(_importFiles[i],
                                            new[] { GetTestPath(_importFiles[i] + ExtensionTestContext.ExtAbWiff) });
                }

                importResultsDlg.NamedPathSets = path;
            });
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
            PauseForScreenShot("p4 - common prefix form");
            RunUI(() =>
            {
                string prefix = importResultsNameDlg.Prefix;
                importResultsNameDlg.Prefix = prefix.Substring(0, prefix.Length - 1);
                importResultsNameDlg.YesDialog();
            });
            WaitForClosedForm(importResultsNameDlg);
            WaitForClosedForm(importResultsDlg);
            WaitForCondition(5 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 5 minutes
            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"p5.view"));
            const string peptideSeqHighlight = "LPDGNGIELCR";
            RunUI(() =>
                {
                    var nodeGroup = SkylineWindow.DocumentUI.TransitionGroups.ToArray()[71];
                    Assert.AreEqual(nodeGroup.TransitionGroup.Peptide.Sequence, peptideSeqHighlight);
                    var chromGroupInfo = nodeGroup.ChromInfos.ToList()[0];
                    Assert.IsNotNull(chromGroupInfo.RetentionTime);
                    Assert.AreEqual(chromGroupInfo.RetentionTime.Value, 16.5, 0.1);
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                });
            RunDlg<ChromChartPropertyDlg>(SkylineWindow.ShowChromatogramProperties, dlg =>
                {
                    dlg.FontSize = 14;
                    dlg.OkDialog();
                });
            PauseForScreenShot("p5 - main window");

            // Train the peak scoring model
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            PauseForScreenShot("p6 - reintegrate form");
            var editDlg = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel);
            RunUI(() => editDlg.TrainModel());
            PauseForScreenShot("p6 - peak scoring dialog trained");
            RunUI(() => Assert.AreEqual(0.668, editDlg.PeakCalculatorsGrid.Items[4].PercentContribution ?? 0, 0.005));

            RunUI(() => editDlg.SelectedGraphTab = 2);
            PauseForScreenShot("p7 - p value graph");

            RunUI(() => editDlg.SelectedGraphTab = 3);
            PauseForScreenShot("p8 - q value graph");

            RunUI(() => editDlg.SelectedGraphTab = 1);
            RunUI(() => editDlg.PeakCalculatorsGrid.SelectRow(3));
            PauseForScreenShot("p10 - peak scoring dialog feature score");

            RunUI(() =>
            {
                // The rows which the tutorial says are missing scores are in fact missing scores
                foreach (int i in new[] { 3, 8, 9, 12, 13, 14, 15, 16 })
                {
                    Assert.IsFalse(editDlg.IsActiveCell(i, 0));
                }
                editDlg.IsFindButtonVisible = true;
                editDlg.FindMissingValues(3);
                editDlg.PeakScoringModelName = "test1";
            });
            PauseForScreenShot("p11 - peak scoring dialog find missing");

            OkDialog(editDlg, editDlg.OkDialog);
            OkDialog(reintegrateDlg, reintegrateDlg.CancelDialog);

            PauseForScreenShot("p12 - find results form");

            // Remove the peptide with no library dot product, and train again
            FindResultsForm findResultsForm = null;
            var missingPeptides = new List<string> { "LGGNEQVTR", "IPVDSIYSPVLK", "YFNDGDIVEGTIVK", 
                                                     "DFDSLGTLR", "GGYAGMLVGSVGETVAQLAR", "GGYAGMLVGSVGETVAQLAR"};
            var isDecoys = new List<bool> {false, false, false, false, false, true};
            RunUI(() =>
            {
                findResultsForm = Application.OpenForms.OfType<FindResultsForm>().FirstOrDefault();
                Assert.IsNotNull(findResultsForm);
// ReSharper disable once PossibleNullReferenceException
                Assert.AreEqual(findResultsForm.ItemCount, 6);
                for (int i = 0; i < 6; ++i)
                {
                    findResultsForm.ActivateItem(i);
                    Assert.AreEqual(SkylineWindow.SelectedPeptideSequence, missingPeptides[i]);
                    if (0 < i && i < 5)
                        SkylineWindow.SetStandardType(PeptideDocNode.STANDARD_TYPE_QC);
                }
            });

            RunUI(() => findResultsForm.Close());

            for (int i = 0; i < 6; ++i)
            {
                if (!(0 < i && i < 5))
                    RemovePeptide(missingPeptides[i], isDecoys[i]);
            }

            var reintegrateDlgNew = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            var editListLibrary = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                reintegrateDlgNew.EditPeakScoringModel);

            RunUI(() => editListLibrary.SelectItem("test1"));
            var editDlgLibrary = ShowDialog<EditPeakScoringModelDlg>(editListLibrary.EditItem);
            RunUI(() =>
                {
                    foreach (int i in new[] { 3, 9, 10, 11, 12 })
                    {
                        Assert.IsTrue(editDlgLibrary.IsActiveCell(i, 0));
                        Assert.IsFalse(editDlgLibrary.PeakCalculatorsGrid.Items[i].IsEnabled);
                        editDlgLibrary.PeakCalculatorsGrid.Items[i].IsEnabled = true;
                    }
                    editDlgLibrary.TrainModel(true);
                });
            PauseForScreenShot("p13 - peak scoring dialog with library score");

            RunUI(() => editDlgLibrary.SelectedGraphTab = 3);
            PauseForScreenShot("p14 - q values with library score");

            OkDialog(editDlgLibrary, editDlgLibrary.OkDialog);

            // Open up the model again for editing, re-train with second best peaks and removing some scores
            RunUI(() => editListLibrary.SelectItem("test1")); // Not L10N
            var editDlgNew = ShowDialog<EditPeakScoringModelDlg>(editListLibrary.EditItem);

            RunUI(() =>
                {
                    Assert.IsFalse(editDlgNew.UsesSecondBest);
                    Assert.IsTrue(editDlgNew.UsesDecoys);
                    Assert.IsTrue(editDlgNew.PeakCalculatorsGrid.Items[7].IsEnabled);
                    Assert.IsTrue(editDlgNew.PeakCalculatorsGrid.Items[7].PercentContribution < 0);
                    Assert.IsTrue(editDlgNew.PeakCalculatorsGrid.Items[5].IsEnabled);
                    Assert.IsTrue(editDlgNew.PeakCalculatorsGrid.Items[5].PercentContribution < 0);
                    editDlgNew.UsesSecondBest = true;
                    editDlgNew.PeakCalculatorsGrid.Items[7].IsEnabled = false;
                    editDlgNew.PeakCalculatorsGrid.Items[5].IsEnabled = false;
                    editDlgNew.TrainModel(true);
                    // Check that these cells are still active even though they've been unchecked
                    Assert.IsTrue(editDlgNew.IsActiveCell(7, 0));
                });
            PauseForScreenShot("p15 - peak scoring dialog with second best");

            OkDialog(editDlgNew, editDlgNew.CancelDialog);
            OkDialog(editListLibrary, editListLibrary.OkDialog);

            // Apply the model to reintegrate peaks
            RunUI(() =>
            {
                reintegrateDlgNew.ComboPeakScoringModelSelected = "test1";
                reintegrateDlgNew.ReintegrateAll = true;
                reintegrateDlgNew.OverwriteManual = true;
                reintegrateDlgNew.AddAnnotation = true;
            });
            PauseForScreenShot("p16 - reintegrate");

            OkDialog(reintegrateDlgNew, reintegrateDlgNew.OkDialog);
            RunUI(() =>
            {
                var nodeGroup = SkylineWindow.DocumentUI.TransitionGroups.ToArray()[70];
                Assert.AreEqual(nodeGroup.TransitionGroup.Peptide.Sequence, peptideSeqHighlight);
                var chromGroupInfo = nodeGroup.ChromInfos.ToList()[0];
                Assert.IsNotNull(chromGroupInfo.RetentionTime);
                Assert.AreEqual(18.0, chromGroupInfo.RetentionTime.Value, 0.1);
            });
            FindNode(peptideSeqHighlight);
            PauseForScreenShot("p17 - corrected peak at 18.0");

            // Reintegrate slightly differently, with a q value cutoff
            var reintegrateDlgQ = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
                {
                    reintegrateDlgQ.ReintegrateAll = false;
                    reintegrateDlgQ.Cutoff = 0.001;
                    reintegrateDlgQ.OverwriteManual = true;
                    reintegrateDlgQ.AddAnnotation = true;
                });
            OkDialog(reintegrateDlgQ, reintegrateDlgQ.OkDialog);
            PauseForScreenShot("p17 and 18 - targets view with some null peaks & chrom with no picked peak");

            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"p14.view"));
            FindNode((622.3086).ToString(CultureInfo.CurrentCulture) + "++");
            PauseForScreenShot("p19 - main window with interference on transition");

            // Export the mProphet features
            var mProphetExportDlg = ShowDialog<MProphetFeaturesDlg>(SkylineWindow.ShowMProphetFeaturesDialog);

            RunUI(() => mProphetExportDlg.BestScoresOnly = true);
            PauseForScreenShot("p20 - mProphet features dialog");
            
            // TODO: actually write the features here using WriteFeatures
            OkDialog(mProphetExportDlg, mProphetExportDlg.CancelDialog);

            // Export a report
            string pathReport = GetTestPath("qValues_Exported_report.csv");
            const string qvalueHeader = "annotation_QValue";
            string reportName = Resources.ReportSpecList_GetDefaults_Peptide_RT_Results;
            if (IsEnableLiveReports)
            {
                var reportExportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
                var editListReport = ShowDialog<EditListDlg<SettingsListBase<ReportOrViewSpec>, ReportOrViewSpec>>(
                    reportExportDlg.EditList);
                RunUI(() => editListReport.SelectItem(reportName));
                PauseForScreenShot("p21 - edit report form list");

                var customizeViewDlg = ShowDialog<ViewEditor>(editListReport.EditItem);
                PauseForScreenShot("p22 - customize view");

                RunUI(() => customizeViewDlg.ChooseColumnsTab.AddColumn(PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value")
                    .Property(AnnotationDef.ANNOTATION_PREFIX + qvalueHeader)));
                PauseForScreenShot("p23 - Selected columns");

                OkDialog(customizeViewDlg, customizeViewDlg.OkDialog);
                OkDialog(editListReport, editListReport.OkDialog);
                RunUI(() => reportExportDlg.ReportName = reportName);
                OkDialog(reportExportDlg, () => reportExportDlg.OkDialog(pathReport, TextUtil.CsvSeparator));
            }
            else
            {
                var exportReportDlg = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
                var editReportListDlg = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg.EditList);
                RunUI(() => editReportListDlg.SelectItem(reportName));
                PauseForScreenShot("p21 - edit report form list");

                var pivotReportDlg = ShowDialog<PivotReportDlg>(editReportListDlg.EditItem);
                PauseForScreenShot("p22 - customize view");

                RunUI(() =>
                {
                    pivotReportDlg.Select(new Identifier("Peptides", "Precursors", "PrecursorResults", qvalueHeader));
                    pivotReportDlg.AddSelectedColumn();
                });
                PauseForScreenShot("p23 - Selected columns");

                OkDialog(pivotReportDlg, pivotReportDlg.OkDialog);
                OkDialog(editReportListDlg, editReportListDlg.OkDialog);
                RunUI(() => exportReportDlg.ReportName = reportName);
                OkDialog(exportReportDlg, () => exportReportDlg.OkDialog(pathReport, TextUtil.CsvSeparator));
            }

            Assert.IsTrue(File.Exists(pathReport));
            using (var reader = new StreamReader(pathReport))
            {
                string line = reader.ReadLine();
                Assert.IsNotNull(line);
                var fieldHeaders = line.Split(TextUtil.CsvSeparator);
                const int qvalueColumnIndex = 6;
                Assert.AreEqual(qvalueColumnIndex + 1, fieldHeaders.Length);
                Assert.AreEqual(qvalueHeader, fieldHeaders[qvalueColumnIndex]);
                int qvalueCount = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    var fields = line.Split(TextUtil.CsvSeparator);
                    double qvalue;
                    if (double.TryParse(fields[qvalueColumnIndex], out qvalue))
                        qvalueCount++;
                }
                Assert.AreEqual(290, qvalueCount); // PrecursorResults field means 29 peptides * 5 replicates * 2 label types
            }

            // Open OpenSWATH gold standard dataset
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("AQUA4_Human_picked_napedro2-mod2.sky"))); // Not L10N
            WaitForDocumentLoaded();

            // Perform re-score of DIA data
            var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            PauseForScreenShot("p25 - rescore peaks for DIA data");

            var rescoreResultsDlg = ShowDialog<RescoreResultsDlg>(manageResults.Rescore);
            PauseForScreenShot("p25 - rescore as same file");

            RunUI(() => rescoreResultsDlg.Rescore(false));
            WaitForCondition(5 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 5 minutes
            WaitForClosedForm(rescoreResultsDlg);
            WaitForClosedForm(manageResults);
            WaitForConditionUI(() => FindOpenForm<AllChromatogramsGraph>() == null);

            // Train the peak scoring model for the DIA dataset
            var reintegrateDlgDia = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);

            // Open the previous scoring model for use with the DIA dataset
            var editListDia = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlgDia.EditPeakScoringModel);
            RunUI(() => editListDia.SelectItem("test1"));
            var editDlgFromSrm = ShowDialog<EditPeakScoringModelDlg>(editListDia.EditItem);
            PauseForScreenShot("p26 - SRM model applied to DIA data");
            RunUI(() =>
                {
                    ValidateCoefficients(editDlgFromSrm, 0);

                    for (int j = 0; j < editDlgFromSrm.PeakCalculatorsGrid.Items.Count; ++j)
                    {
                        Assert.AreEqual(editDlgFromSrm.PeakCalculatorsGrid.Items[j].PercentContribution, null);
                    }
                    int i = 0;
                    Assert.IsFalse(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsFalse(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsFalse(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsFalse(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsTrue(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsTrue(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsTrue(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsTrue(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsFalse(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsFalse(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsFalse(editDlgFromSrm.IsActiveCell(i++, 0));
                    Assert.IsFalse(editDlgFromSrm.IsActiveCell(i, 0));
                });
            
            OkDialog(editDlgFromSrm, editDlgFromSrm.CancelDialog);
            OkDialog(editListDia, editListDia.CancelDialog);

            // Train a new model for the DIA dataset
            var editDlgDia = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlgDia.AddPeakScoringModel);
            RunUI(() =>
                {
                    editDlgDia.UsesDecoys = false;
                    editDlgDia.UsesSecondBest = true;
                    editDlgDia.TrainModel();
                });

            RunUI(() => ValidateCoefficients(editDlgDia, 1));

            PauseForScreenShot("p27 - DIA peak scoring dialog with second best");
            
            RunUI(() =>
                {
                    editDlgDia.SelectedGraphTab = 1;
                    editDlgDia.PeakCalculatorsGrid.SelectRow(3);
                    editDlgDia.IsFindButtonVisible = true;
                    editDlgDia.FindMissingValues(3);
                    editDlgDia.PeakScoringModelName = "testDIA";
                });
            OkDialog(editDlgDia, editDlgDia.OkDialog);
            RunUI(() =>
            {
                reintegrateDlgDia.ReintegrateAll = true;
                reintegrateDlgDia.OverwriteManual = true;
                reintegrateDlgDia.AddAnnotation = true;
            });
            OkDialog(reintegrateDlgDia, reintegrateDlgDia.OkDialog);

            findResultsForm = Application.OpenForms.OfType<FindResultsForm>().FirstOrDefault();
            Assert.IsNotNull(findResultsForm);
            Assert.AreEqual(findResultsForm.ItemCount, 34);
        }

        private void ValidateCoefficients(EditPeakScoringModelDlg editDlgFromSrm, int coeffIndex)
        {
            string coefficients = string.Join(@"|", GetCoefficientStrings(editDlgFromSrm));
            if (IsRecordMode)
                Console.WriteLine(@"""{0}"", // Not L10N", coefficients);  // Not L10N
            else
                AssertEx.AreEqualLines(EXPECTED_COEFFICIENTS[coeffIndex], coefficients);
        }

        private IEnumerable<string> GetCoefficientStrings(EditPeakScoringModelDlg editDlg)
        {
            for (int i = 0; i < editDlg.PeakCalculatorsGrid.Items.Count; i++)
            {
                double? weight = editDlg.PeakCalculatorsGrid.Items[i].Weight;
                if (weight.HasValue)
                    yield return string.Format(CultureInfo.InvariantCulture, "{0:F04}", weight.Value);
                else
                    yield return " null ";  // To help values line up
            }
        }
    }
}
