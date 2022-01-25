/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class SmallMoleculesQuantificationTutorialTest : AbstractFunctionalTestEx
    {
        protected override bool UseRawFiles
        {
            get { return !ForceMzml && ExtensionTestContext.CanImportWatersRaw; }
        }

        [TestMethod]
        public void TestSmallMoleculesQuantificationTutorial()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "SmallMoleculeQuantification";

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/SmallMoleculeQuant-20_1.pdf";
            ForceMzml = true; // Prefer mzML as being the more efficient download

            TestFilesZipPaths = new[]
            {
                (UseRawFiles
                   ? @"https://skyline.ms/tutorials/SmallMoleculeQuantification.zip"
                   : @"https://skyline.ms/tutorials/SmallMoleculeQuantification_mzML.zip"),
                @"TestTutorial\SmallMoleculesQuantificationViews.zip"
            };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath = null)
        {
            string folderSmallMolecule = UseRawFiles ? "SmallMoleculeQuantification" : "SmallMoleculeQuantification_mzML";
            string fullRelativePath = relativePath != null ? Path.Combine(folderSmallMolecule, relativePath) : folderSmallMolecule;
            return TestFilesDirs[0].GetTestPath(fullRelativePath);
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));

            // Inserting a Transition List, p. 2
            {
                var doc = SkylineWindow.Document;
                
                var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                RunUI(() => importDialog.Size = new Size(600, 300));
                PauseForScreenShot<InsertTransitionListDlg>("Insert Transition List form", 4);


                var text = "DrugX,Drug,light,283.04,1,129.96,1,26,16,2.7\r\nDrugX,Drug,heavy,286.04,1,133.00,1,26,16,2.7\r\n";
                text = text.Replace(',', TextUtil.CsvSeparator).Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                var col4Dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.textBox1.Text = text);

                RunUI(() => {
                    col4Dlg.radioMolecule.PerformClick();
                });
                PauseForScreenShot<ImportTransitionListColumnSelectDlg>("Insert Transition List form before columns selected", 5);
                RunUI(() => {
                    var comboBoxes = col4Dlg.ComboBoxes;
                    comboBoxes[0].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name);
                    comboBoxes[1].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name);
                    comboBoxes[2].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type);
                    comboBoxes[3].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z);
                    comboBoxes[4].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge);
                    comboBoxes[5].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z);
                    comboBoxes[6].SelectedIndex = comboBoxes[1].FindStringExact(Resources.PasteDlg_UpdateMoleculeType_Product_Charge);
                    comboBoxes[7].SelectedIndex = comboBoxes[1].FindStringExact(Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage);
                    comboBoxes[8].SelectedIndex = comboBoxes[1].FindStringExact(Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy);
                    comboBoxes[9].SelectedIndex = comboBoxes[1].FindStringExact(Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time);
                });

                PauseForScreenShot<ImportTransitionListColumnSelectDlg>("Column Select Dialog with identified columns", 6);

                OkDialog(col4Dlg, col4Dlg.OkDialog);

                var docTargets = WaitForDocumentChange(doc);

                AssertEx.IsDocumentState(docTargets, null, 1, 1, 2, 2);
                Assert.IsFalse(docTargets.MoleculeTransitions.Any(t => t.Transition.IsPrecursor()));

                RunUI(() =>
                {
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    SkylineWindow.Size = new Size(957, 654);
                });
                SelectNode(SrmDocument.Level.Transitions, 0);
                SelectNode(SrmDocument.Level.Transitions, 1);
                SelectNode(SrmDocument.Level.Molecules, 0);
                PauseForScreenShot<SkylineWindow>("Skyline with small molecule targets", 7);

                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    // Predicition Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Prediction;
                    transitionSettingsUI.PrecursorMassType = MassType.Monoisotopic;
                    transitionSettingsUI.FragmentMassType = MassType.Monoisotopic;
                    transitionSettingsUI.RegressionCEName = "Waters Xevo"; // Collision Energy
                    transitionSettingsUI.RegressionDPName = Resources.SettingsList_ELEMENT_NONE_None; // Declustering Potential
                    transitionSettingsUI.OptimizationLibraryName = Resources.SettingsList_ELEMENT_NONE_None; // Optimization Library
                    transitionSettingsUI.RegressionCOVName = Resources.SettingsList_ELEMENT_NONE_None; // Compensation Voltage

                    transitionSettingsUI.UseOptimized = true;
                    transitionSettingsUI.OptimizeType = OptimizedMethodType.Transition.GetLocalizedString();
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Prediction tab", 8);


                RunUI(() =>
                {
                    // Filter Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    transitionSettingsUI.SelectedPeptidesSmallMolsSubTab = 1;
                    transitionSettingsUI.SmallMoleculePrecursorAdducts = Adduct.M_PLUS_H.AdductFormula;
                    transitionSettingsUI.SmallMoleculeFragmentAdducts = Adduct.M_PLUS.AdductFormula;
                    transitionSettingsUI.SmallMoleculeFragmentTypes = TransitionFilter.SMALL_MOLECULE_FRAGMENT_CHAR;
                    transitionSettingsUI.FragmentMassType = MassType.Monoisotopic;
                    transitionSettingsUI.SetAutoSelect = true;
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings -Filter tab", 9);


                RunUI(() =>
                {
                    // Instrument Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                    transitionSettingsUI.MinMz = 50;
                    transitionSettingsUI.MaxMz = 1500;
                    transitionSettingsUI.MZMatchTolerance = .02;
                    transitionSettingsUI.MinTime = null;
                    transitionSettingsUI.MaxTime = null;
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Instrument tab", 10);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                WaitForDocumentChange(docTargets);

                RunUI(() => SkylineWindow.SaveDocument(GetTestPath("SMQuant_v1.sky")));

                ImportReplicates(true);

                SelectNode(SrmDocument.Level.Transitions, 1);
                SelectNode(SrmDocument.Level.Molecules, 0);

                PauseForScreenShot<SkylineWindow>("Skyline window multi-precursor graph", 13);

                var docResults = SkylineWindow.Document;

                var expectedTransCount = new Dictionary<string, int[]>
                {
                    // peptide count, transition groups, heavy transition groups, tranistions, heavy transitions
                    {"Blank_01", new[] {1, 0, 1, 0, 1}},
                    {"DoubleBlank1", new[] {1, 0, 1, 0, 1}},
                    {"DoubleBlank2", new[] {1, 0, 1, 0, 1}},
                    {"DoubleBlank3", new[] {1, 0, 1, 0, 1}},
                    {"47_0_1_1_00_1021523591", new[] {1, 0, 1, 0, 1}},

                };
                var msg = "";
                foreach (var chromatogramSet in docResults.Settings.MeasuredResults.Chromatograms)
                {
                    int[] transitions;
                    if (!expectedTransCount.TryGetValue(chromatogramSet.Name, out transitions))
                        transitions = new[] {1, 1, 1, 1, 1}; // Most have this value
                    try
                    {
                        AssertResult.IsDocumentResultsState(docResults, chromatogramSet.Name, transitions[0],
                            transitions[1], transitions[2], transitions[3], transitions[4]);
                    }
                    catch (Exception x)
                    {
                        msg += TextUtil.LineSeparate(x.Message);
                    }
                }
                if (!string.IsNullOrEmpty(msg))
                    Assert.IsTrue(string.IsNullOrEmpty(msg), msg);

                RestoreViewOnScreen(9);
                SelectNode(SrmDocument.Level.Transitions, 0);
                SelectNode(SrmDocument.Level.Transitions, 1);
                SelectNode(SrmDocument.Level.Molecules, 0);
                PauseForScreenShot<SkylineWindow>("Skyline window multi-replicate layout", 14);

                // Peak integration correction
                ActivateReplicate("DoubleBlank1"); // First with mismatched RT
                PauseForScreenShot<SkylineWindow>("Selected replicate with unexpected RT", 15);
                ChangePeakBounds("DoubleBlank2", 26.5, 27.5);
                ChangePeakBounds("DoubleBlank3", 26.5, 27.5);
                ChangePeakBounds("DoubleBlank1", 26.5, 27.5);
                PauseForScreenShot<SkylineWindow>("Adjusted peak boundaries", 16);

                using (new WaitDocumentChange(1, true))
                {
                    // Quant settings
                    var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

                    RunUI(() =>
                    {
                        peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                        peptideSettingsUI.QuantRegressionFit = RegressionFit.LINEAR;
                        peptideSettingsUI.QuantNormalizationMethod =
                            new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy);
                        peptideSettingsUI.QuantRegressionWeighting = RegressionWeighting.ONE_OVER_X_SQUARED;
                        peptideSettingsUI.QuantMsLevel = null; // All
                        peptideSettingsUI.QuantUnits = "uM";
                    });
                    PauseForScreenShot<PeptideSettingsUI.QuantificationTab>("Peptide Settings - Quantitation", 17);
                    OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
                }

                // Setting sample types
                WaitForClosedForm<DocumentGridForm>();
                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
                RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
                PauseForScreenShot<DocumentGridForm>("Document Grid - replicates", 18);

                /*IDictionary<string, Tuple<SampleType, double?>> sampleTypes =
                    new Dictionary<string, Tuple<SampleType, double?>> {
                    {"Blank_01", new Tuple<SampleType, double?>(SampleType.BLANK,null)},
                    {"Blank_02", new Tuple<SampleType, double?>(SampleType.BLANK,null)},
                    {"Blank_03", new Tuple<SampleType, double?>(SampleType.BLANK,null)},
                    {"Cal_1_01", new Tuple<SampleType, double?>(SampleType.STANDARD,10)},
                    {"Cal_1_02", new Tuple<SampleType, double?>(SampleType.STANDARD,10)},
                    {"Cal_2_01", new Tuple<SampleType, double?>(SampleType.STANDARD,20)},
                    {"Cal_2_02", new Tuple<SampleType, double?>(SampleType.STANDARD,20)},
                    {"Cal_3_01", new Tuple<SampleType, double?>(SampleType.STANDARD,100)},
                    {"Cal_3_02", new Tuple<SampleType, double?>(SampleType.STANDARD,100)},
                    {"Cal_4_01", new Tuple<SampleType, double?>(SampleType.STANDARD,200)},
                    {"Cal_4_02", new Tuple<SampleType, double?>(SampleType.STANDARD,200)},
                    {"Cal_5_01", new Tuple<SampleType, double?>(SampleType.STANDARD,400)},
                    {"Cal_5_02", new Tuple<SampleType, double?>(SampleType.STANDARD,400)},
                    {"Cal_6_01", new Tuple<SampleType, double?>(SampleType.STANDARD,600)},
                    {"Cal_6_02", new Tuple<SampleType, double?>(SampleType.STANDARD,600)},
                    {"Cal_7_01", new Tuple<SampleType, double?>(SampleType.STANDARD,800)},
                    {"Cal_7_02", new Tuple<SampleType, double?>(SampleType.STANDARD,800)},
                    {"DoubleBlank1", new Tuple<SampleType, double?>(SampleType.DOUBLE_BLANK,null)},
                    {"DoubleBlank2", new Tuple<SampleType, double?>(SampleType.DOUBLE_BLANK,null)},
                    {"DoubleBlank3", new Tuple<SampleType, double?>(SampleType.DOUBLE_BLANK,null)},
                    {"QC_High_01", new Tuple<SampleType, double?>(SampleType.QC,589)},
                    {"QC_High_02", new Tuple<SampleType, double?>(SampleType.QC,589)},
                    {"QC_High_03", new Tuple<SampleType, double?>(SampleType.QC,589)},
                    {"QC_Low_01", new Tuple<SampleType, double?>(SampleType.QC,121)},
                    {"QC_Low_02", new Tuple<SampleType, double?>(SampleType.QC,121)},
                    {"QC_Low_03", new Tuple<SampleType, double?>(SampleType.QC,121)},
                    {"QC_Mid_01", new Tuple<SampleType, double?>(SampleType.QC,346)},
                    {"QC_Mid_02", new Tuple<SampleType, double?>(SampleType.QC,346)},
                    {"QC_Mid_03", new Tuple<SampleType, double?>(SampleType.QC,346)}
                };*/

                string concentrationsFileName;
                if (CultureInfo.CurrentUICulture.Name.StartsWith("zh"))
                {
                    concentrationsFileName = "Concentrations_zh.xlsx";
                }
                else if (CultureInfo.CurrentUICulture.Name.StartsWith("ja"))
                {
                    concentrationsFileName = "Concentrations_ja.xlsx";
                }
                else
                {
                    concentrationsFileName = "Concentrations.xlsx";
                }
                SetExcelFileClipboardText(GetTestPath(concentrationsFileName), "Sheet1", 3, false);
                RunUI(() =>
                {
                    // Find and select Blank_01 cell
                    var replicateColumnIndex = documentGrid.FindColumn(PropertyPath.Root).Index;
                    documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows.Cast<DataGridViewRow>()
                        .Select(row => row.Cells[replicateColumnIndex])
                        .FirstOrDefault(cell => ((Replicate) cell.Value).Name == "Blank_01");

                    documentGrid.DataGridView.SendPaste();
                });
                //SetDocumentGridSampleTypesAndConcentrations(sampleTypes);
                PauseForScreenShot<DocumentGridForm>("Document Grid - sample types - enlarge for screenshot so rows 95_0_1_1_00_1021523636 to end can be seen", 22);
                foreach (var chromatogramSet in SkylineWindow.Document.MeasuredResults.Chromatograms)
                {
                    if (chromatogramSet.Name.StartsWith("DoubleBlank"))
                    {
                        Assert.AreEqual(SampleType.DOUBLE_BLANK, chromatogramSet.SampleType);
                    }
                    else if (chromatogramSet.Name.StartsWith("Blank"))
                    {
                        Assert.AreEqual(SampleType.BLANK, chromatogramSet.SampleType);
                    }
                    else if (chromatogramSet.Name.StartsWith("QC"))
                    {
                        Assert.AreEqual(SampleType.QC, chromatogramSet.SampleType);
                    }
                    else if (chromatogramSet.Name.StartsWith("Cal"))
                    {
                        Assert.AreEqual(SampleType.STANDARD, chromatogramSet.SampleType);
                    }
                    else
                    {
                        Assert.AreEqual(SampleType.UNKNOWN, chromatogramSet.SampleType);
                    }
                }

                RunUI(() => SkylineWindow.ShowCalibrationForm());
                PauseForScreenShot<CalibrationForm>("Calibration Curve ", 23);

                EnableDocumentGridColumns(documentGrid, Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates, 47,
                    new[]
                    {
                        "Proteins!*.Peptides!*.Results!*.Value.Quantification.Accuracy",
                        "Proteins!*.Peptides!*.Results!*.Value.ExcludeFromCalibration"
                    },
                    "Replicates_custom_quant");
                PauseForScreenShot<DocumentGridForm>("Custom document grid -scroll to end so same rows are in screenshot", 23);

                SetDocumentGridExcludeFromCalibration();
                PauseForScreenShot<CalibrationForm>("Calibration Curve - outliers disabled", 24);

                if (IsCoverShotMode)
                {
                    RunUI(() =>
                    {
                        Settings.Default.ChromatogramFontSize = 14;
                        Settings.Default.AreaFontSize = 14;
                        SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                        SkylineWindow.AutoZoomBestPeak();
                    });

                    RestoreCoverViewOnScreen();

                    RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, resultsDlg =>
                    {
                        resultsDlg.SelectedChromatograms =
                            SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms.Take(15);
                        resultsDlg.RemoveReplicates();
                        resultsDlg.OkDialog();
                    });

                    RunUI(SkylineWindow.FocusDocument);
                    TakeCoverShot();
                    return;
                }

                ImportReplicates(false); // Import the rest of the replicates

                RunUI(() => documentGrid.ChooseView(Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results));
                WaitForConditionUI(() => documentGrid.ColumnCount > 6);
                RunUI(() => {
                    var colReplicate = documentGrid.FindColumn(PropertyPath.Parse("Results!*.Value.ResultFile.Replicate"));
                    documentGrid.DataGridView.Sort(colReplicate, ListSortDirection.Ascending);
                });

                PauseForScreenShot<DocumentGridForm>("Document Grid - Peptide Ratio Results - manually widen to show all columns", 25);
                Settings.Default.CalibrationCurveOptions.LogXAxis = true;
                Settings.Default.CalibrationCurveOptions.LogYAxis = true;

                var calibrationForm = FindOpenForm<CalibrationForm>();
                RunUI(()=>calibrationForm.UpdateUI(false));
                PauseForScreenShot<CalibrationForm>("Calibration Curve: Log", 26);
            }

        }

        private void ImportReplicates(bool isFirstPass)
        {
            using (new WaitDocumentChange(1, true))
            {
                var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                if (isFirstPass)
                {
                    PauseForScreenShot<ImportResultsDlg>("Import Results form", 11);
                }
                var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets =
                    importResultsDlg1.GetDataSourcePathsFile(null));
                var firstPassMatches = new[]
                {
                    "Blank", "Cal", "QC",
                    "80_", "81_", "82_", "83_", "84_", "85_", "86_", "87_", "88_", "89_", "90_",
                    "91_", "92_", "93_", "94_", "95_", "96_"
                };
                RunUI(() =>
                {
                    openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(GetTestPath());
                    openDataSourceDialog1.SelectAllFileType(ExtWatersRaw,
                            path => isFirstPass ? firstPassMatches.Any(path.Contains) : !firstPassMatches.Any(path.Contains));
                });
                if (isFirstPass)
                {
                    PauseForScreenShot<OpenDataSourceDialog>(
                        "Open Data Source Files form - Use horizontal scrollbar to show the already selected files before screenshot",
                        8);
                }
                OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);
                OkDialog(importResultsDlg1, importResultsDlg1.OkDialog);
            }
        }

        // Assumes document grid is open, and is configured with Exclude From Calibration column
        private void SetDocumentGridExcludeFromCalibration()
        {
            IDictionary<string, bool> excludes = new Dictionary<string, bool>
            {
                {"Cal_5_01", true},
                {"Cal_5_02", true}
            };

            var documentGrid = FindOpenForm<DocumentGridForm>();
            WaitForConditionUI(
                () => documentGrid.FindColumn(PropertyPath.Parse("Results!*.Value.ResultFile.Replicate")) != null &&
                      documentGrid.FindColumn(PropertyPath.Parse("Results!*.Value.ExcludeFromCalibration")) != null &&
                      documentGrid.RowCount >= 40);
            RunUI(() =>
            {
                var colReplicate = documentGrid.FindColumn(PropertyPath.Parse("Results!*.Value.ResultFile.Replicate"));
                var colExcludeFromCalibration = documentGrid.FindColumn(PropertyPath.Parse("Results!*.Value.ExcludeFromCalibration"));
                for (var iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    var row = documentGrid.DataGridView.Rows[iRow];
                    var replicateName = row.Cells[colReplicate.Index].Value.ToString();
                    bool exclude;
                    if (!excludes.TryGetValue(replicateName, out exclude))
                    {
                        exclude = false;
                    }
                    row.Cells[colExcludeFromCalibration.Index].Value = exclude;
                }
            });
        }


    }
}
