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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
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
                generateDecoysDlg.DecoysMethod = DecoyGeneration.ADD_RANDOM;
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
            PauseForScreenShot("p5 -- main window");

            // Train the peak scoring model
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Integration);
            var editDlg = ShowDialog<EditPeakScoringModelDlg>(peptideSettingsDlg.AddPeakScoringModel);
            RunUI(() =>
                {
                    editDlg.TrainModel();
                    Assert.AreEqual(0.622, editDlg.PeakCalculatorsGrid.Items[3].PercentContribution ?? 0, 0.005);
                });
            PauseForScreenShot("p6 -- peak scoring dialog trained");

            RunUI(() => editDlg.SelectedGraphTab = 1);
            RunUI(() => editDlg.PeakCalculatorsGrid.SelectRow(2));
            PauseForScreenShot("p7 -- peak scoring dialog feature score");

            RunUI(() =>
            {
                // The rows which the tutorial says are missing scores are in fact missing scores
                foreach (int i in new[] { 2, 7, 8, 9, 10 })
                {
                    Assert.IsFalse(editDlg.IsActiveCell(i, 0));
                }
                editDlg.IsFindButtonVisible = true;
                editDlg.FindMissingValues(2);
                editDlg.PeakScoringModelName = "test1";
            });
            PauseForScreenShot("p8 -- peak scoring dialog find missing");

            OkDialog(editDlg, editDlg.OkDialog);
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);

            PauseForScreenShot("p8 -- find results form");

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
                }
            });

            RunUI(() => findResultsForm.Close());

            for (int i = 0; i < 6; ++i)
            {
                RemovePeptide(missingPeptides[i], isDecoys[i]);
            }

            var peptideSettingsDlgNew = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlgNew.SelectedTab = PeptideSettingsUI.TABS.Integration);
            var editListLibrary = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                peptideSettingsDlgNew.EditPeakScoringModel);

            RunUI(() => editListLibrary.SelectItem("test1"));
            var editDlgLibrary = ShowDialog<EditPeakScoringModelDlg>(editListLibrary.EditItem);
            RunUI(() =>
                {
                    foreach (int i in new[] { 2, 8, 9, 10 })
                    {
                        Assert.IsTrue(editDlgLibrary.IsActiveCell(i, 0));
                        Assert.IsFalse(editDlgLibrary.PeakCalculatorsGrid.Items[i].IsEnabled);
                        editDlgLibrary.PeakCalculatorsGrid.Items[i].IsEnabled = true;
                    }
                    editDlgLibrary.TrainModel(true);
                });
            PauseForScreenShot("p9 - peak scoring dialog with library score");

            OkDialog(editDlgLibrary, editDlgLibrary.OkDialog);

            // Open up the model again for editing, re-train with second best peaks and removing some scores
            RunUI(() => editListLibrary.SelectItem("test1")); // Not L10N
            var editDlgNew = ShowDialog<EditPeakScoringModelDlg>(editListLibrary.EditItem);

            RunUI(() =>
                {
                    Assert.IsFalse(editDlgNew.UsesSecondBest);
                    Assert.IsTrue(editDlgNew.UsesDecoys);
                    Assert.IsTrue(editDlgNew.PeakCalculatorsGrid.Items[4].IsEnabled);
                    Assert.IsTrue(editDlgNew.PeakCalculatorsGrid.Items[6].IsEnabled);
                    Assert.IsTrue(editDlgNew.PeakCalculatorsGrid.Items[4].PercentContribution < 0);
                    Assert.IsTrue(editDlgNew.PeakCalculatorsGrid.Items[6].PercentContribution < 0);
                    editDlgNew.UsesSecondBest = true;
                    editDlgNew.PeakCalculatorsGrid.Items[4].IsEnabled = false;
                    editDlgNew.PeakCalculatorsGrid.Items[6].IsEnabled = false;
                    editDlgNew.TrainModel(true);
                    // Check that these cells are still active even though they've been unchecked
                    Assert.IsTrue(editDlgNew.IsActiveCell(4, 0));
                    Assert.IsTrue(editDlgNew.IsActiveCell(6, 0));
                });
            PauseForScreenShot("p10 - peak scoring dialog with second best");

            OkDialog(editDlgNew, editDlgNew.CancelDialog);
            OkDialog(editListLibrary, editListLibrary.CancelDialog);
            OkDialog(peptideSettingsDlgNew, peptideSettingsDlgNew.OkDialog);

            // Apply the model to reintegrate peaks
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() => reintegrateDlg.ReintegrateAll = true);
            PauseForScreenShot("p11 -- reintegrate");

            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            RunUI(() =>
            {
                var nodeGroup = SkylineWindow.DocumentUI.TransitionGroups.ToArray()[64];
                Assert.AreEqual(nodeGroup.TransitionGroup.Peptide.Sequence, peptideSeqHighlight);
                var chromGroupInfo = nodeGroup.ChromInfos.ToList()[0];
                Assert.IsNotNull(chromGroupInfo.RetentionTime);
                Assert.AreEqual(chromGroupInfo.RetentionTime.Value, 18.0, 0.1);
            });
            FindNode(peptideSeqHighlight);
            PauseForScreenShot("p12 -- main window");

            // Reintegrate slightly differently, with a q value cutoff
            var reintegrateDlgNew = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
                {
                    reintegrateDlgNew.ReintegrateAll = false;
                    reintegrateDlgNew.Cutoff = 0.001;
                });
            OkDialog(reintegrateDlgNew, reintegrateDlgNew.OkDialog);
            PauseForScreenShot("p13 -- main window with some null peaks");

            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"p14.view"));
            FindNode((622.3086).ToString(CultureInfo.CurrentCulture) + "++");
            PauseForScreenShot("p14 -- main window with interference on transition");

            // Export the mProphet features
            var mProphetExportDlg = ShowDialog<MProphetFeaturesDlg>(SkylineWindow.ShowMProphetFeaturesDialog);

            RunUI(() => mProphetExportDlg.BestScoresOnly = true);
            PauseForScreenShot("p15 -- mProphet features dialog");
            
            // TODO: actually write the features here using WriteFeatures
            OkDialog(mProphetExportDlg, mProphetExportDlg.CancelDialog);

            // Open OpenSWATH gold standard dataset
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("AQUA4_Human_picked_napedro2-mod2.sky"))); // Not L10N
            WaitForDocumentLoaded();

            // Perform re-score of DIA data
            var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            PauseForScreenShot("p17 -- rescore peaks for DIA data");

            var rescoreResultsDlg = ShowDialog<RescoreResultsDlg>(manageResults.Rescore);
            PauseForScreenShot("p17 -- rescore as same file");

            RunUI(() => rescoreResultsDlg.Rescore(false));
            WaitForCondition(5 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 5 minutes
            WaitForClosedForm(rescoreResultsDlg);
            WaitForClosedForm(manageResults);

            // Train the peak scoring model for the DIA dataset
            var peptideSettingsDlgDia = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlgDia.SelectedTab = PeptideSettingsUI.TABS.Integration);

            // Open the previous scoring model for use with the DIA dataset
            var editListDia = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    peptideSettingsDlgDia.EditPeakScoringModel);
            RunUI(() => editListDia.SelectItem("test1"));
            var editDlgFromSrm = ShowDialog<EditPeakScoringModelDlg>(editListDia.EditItem);
            RunUI(() =>
                {
                    int i = 0;
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, 0.4656, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, -1.4527, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, 7.0228, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, 0.0203, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, 0.0507, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, 0.2310, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, 0.3883, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgFromSrm.PeakCalculatorsGrid.Items[i].Weight, null, 1e-3);

                    for (int j = 0; j <= i; ++j)
                    {
                        Assert.AreEqual(editDlgFromSrm.PeakCalculatorsGrid.Items[j].PercentContribution, null);
                    }
                    i = 0;
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
            PauseForScreenShot("p17 -- SRM model applied to DIA data");
            
            OkDialog(editDlgFromSrm, editDlgFromSrm.CancelDialog);
            OkDialog(editListDia, editListDia.CancelDialog);

            // Train a new model for the DIA dataset
            var editDlgDia = ShowDialog<EditPeakScoringModelDlg>(peptideSettingsDlgDia.AddPeakScoringModel);
            RunUI(() =>
                {
                    editDlgDia.UsesDecoys = false;
                    editDlgDia.UsesSecondBest = true;
                    editDlgDia.TrainModel();
                    int i = 0;
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, 0.2612, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, 5.5116, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, -0.0222, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, 0.6672, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, 1.0605, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i++].Weight, null, 1e-3);
                    AssertEx.AreEqualNullable(editDlgDia.PeakCalculatorsGrid.Items[i].Weight, null, 1e-3);
                });
            PauseForScreenShot("p18 -- DIA peak scoring dialog with second best");
            
            RunUI(() =>
                {
                    editDlgDia.SelectedGraphTab = 1;
                    editDlgDia.PeakCalculatorsGrid.SelectRow(2);
                    editDlgDia.IsFindButtonVisible = true;
                    editDlgDia.FindMissingValues(2);
                    editDlgDia.PeakScoringModelName = "testDIA";
                });
            OkDialog(editDlgDia, editDlgDia.OkDialog);
            OkDialog(peptideSettingsDlgDia, peptideSettingsDlgDia.OkDialog);

            findResultsForm = Application.OpenForms.OfType<FindResultsForm>().FirstOrDefault();
            Assert.IsNotNull(findResultsForm);
            Assert.AreEqual(findResultsForm.ItemCount, 34);

            // Reintegrate
            var reintegrateDlgDia = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() => reintegrateDlgDia.ReintegrateAll = true);
            OkDialog(reintegrateDlgDia, reintegrateDlgDia.OkDialog);
        }
    }
}
