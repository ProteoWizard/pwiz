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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            IsPauseForScreenShots = false;

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
            // Open the file
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("SRMCourse_DosR-hDP__20130501-tutorial-empty.sky"))); // Not L10N

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

            // Import the raw data
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() => 
            {
                importResultsDlg.RadioAddNewChecked = true;
                var path = new KeyValuePair<string, string[]>[5];
                for (int i = 0; i < 5; ++i)
                {
                    path[i] = new KeyValuePair<string, string[]>(_importFiles[i],
                                            new[] { GetTestPath(_importFiles[i] + (ExtensionTestContext.CanImportAbWiff ? 
                                                                                  ExtensionTestContext.ExtAbWiff : ".mzXML")) });
                }

                importResultsDlg.NamedPathSets = path;
            });
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
            RunUI(importResultsNameDlg.YesDialog);
            WaitForClosedForm(importResultsNameDlg);
            WaitForClosedForm(importResultsDlg);
            WaitForCondition(5 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 5 minutes
            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"SRM1.view"));
            RunUI(() =>
                {
                    var nodeGroup = SkylineWindow.DocumentUI.TransitionGroups.ToArray()[66];
                    Assert.AreEqual(nodeGroup.TransitionGroup.Peptide.Sequence, "LPDGNGIELCR");
                    var chromGroupInfo = nodeGroup.ChromInfos.ToList()[0];
                    Assert.IsNotNull(chromGroupInfo.RetentionTime);
                    Assert.AreEqual(chromGroupInfo.RetentionTime.Value, 16.5, 0.1);
                });
            PauseForScreenShot("p4 -- main window");

            // Train the peak scoring model
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Integration);
            var editDlg = ShowDialog<EditPeakScoringModelDlg>(peptideSettingsDlg.AddPeakScoringModel);
            RunUI(() => editDlg.TrainModel());
            PauseForScreenShot("p5 -- peak scoring dialog trained");

            RunUI(() => editDlg.SelectedGraphTab = 1);
            RunUI(() => editDlg.PeakCalculatorsGrid.SelectRow(2));
            PauseForScreenShot("p6 -- peak scoring dialog feature score");
            
            RunUI(() => editDlg.PeakScoringModelName = "test1");
            OkDialog(editDlg, editDlg.OkDialog);
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);

            // Remove the peptide with no library dot product, and train again
            RemovePeptide("GGYAGMLVGSVGETVAQLAR");
            var peptideSettingsDlgNew = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlgNew.SelectedTab = PeptideSettingsUI.TABS.Integration);
            var editListLibrary = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                peptideSettingsDlgNew.EditPeakScoringModel);

            RunUI(() => editListLibrary.SelectItem("test1"));
            var editDlgLibrary = ShowDialog<EditPeakScoringModelDlg>(editListLibrary.EditItem);
            RunUI(() =>
                {
                    editDlgLibrary.PeakCalculatorsGrid.Items[2].IsEnabled = true;
                    editDlgLibrary.TrainModel(true);
                });
            PauseForScreenShot("p7 - peak scoring dialog with library score");

            OkDialog(editDlgLibrary, editDlgLibrary.OkDialog);

            // Open up the model again for editing, re-train with second best peaks and removing some scores
            RunUI(() => editListLibrary.SelectItem("test1")); // Not L10N
            var editDlgNew = ShowDialog<EditPeakScoringModelDlg>(editListLibrary.EditItem);

            RunUI(() =>
                {
                    editDlgNew.UsesSecondBest = true;
                    editDlgNew.PeakCalculatorsGrid.Items[5].IsEnabled = false;
                    editDlgNew.PeakCalculatorsGrid.Items[6].IsEnabled = false;
                    editDlgNew.TrainModel(true);
                });
            PauseForScreenShot("p8 - peak scoring dialog with second best");
            OkDialog(editDlgNew, editDlgNew.CancelDialog);
            OkDialog(editListLibrary, editListLibrary.CancelDialog);
            OkDialog(peptideSettingsDlgNew, peptideSettingsDlgNew.OkDialog);

            // Apply the model to reintegrate peaks
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() => reintegrateDlg.ReintegrateAll = true);
            PauseForScreenShot("p9 -- reintegrate");

            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"SRM2.view"));
            RunUI(() =>
            {
                var nodeGroup = SkylineWindow.DocumentUI.TransitionGroups.ToArray()[64];
                Assert.AreEqual(nodeGroup.TransitionGroup.Peptide.Sequence, "LPDGNGIELCR");
                var chromGroupInfo = nodeGroup.ChromInfos.ToList()[0];
                Assert.IsNotNull(chromGroupInfo.RetentionTime);
                Assert.AreEqual(chromGroupInfo.RetentionTime.Value, 18.0, 0.1);
            });
            PauseForScreenShot("p10 -- main window");

            // Reintegrate slightly differently, with a q value cutoff
            var reintegrateDlgNew = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
                {
                    reintegrateDlgNew.ReintegrateAll = false;
                    reintegrateDlgNew.Cutoff = 0.001;
                });
            OkDialog(reintegrateDlgNew, reintegrateDlgNew.OkDialog);

            // Export the mProphet features
            var mProphetExportDlg = ShowDialog<MProphetFeaturesDlg>(SkylineWindow.ShowMProphetFeaturesDialog);

            RunUI(() => mProphetExportDlg.BestScoresOnly = true);
            PauseForScreenShot("p11 -- mProphet features dialog");
            
            // TODO: actually write the features here using WriteFeatures
            OkDialog(mProphetExportDlg, mProphetExportDlg.CancelDialog);

            // Open OpenSWATH gold standard dataset
            RunUI(() => SkylineWindow.OpenFile(GetTestPath("AQUA4_Human_picked_napedro2-mod2.sky"))); // Not L10N

            // Train the peak scoring model for the DIA dataset
            var peptideSettingsDlgDIA = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlgDIA.SelectedTab = PeptideSettingsUI.TABS.Integration);
            var editDlgDIA = ShowDialog<EditPeakScoringModelDlg>(peptideSettingsDlgDIA.AddPeakScoringModel);
            RunUI(() =>
                {
                    editDlgDIA.UsesDecoys = false;
                    editDlgDIA.UsesSecondBest = true;
                    editDlgDIA.TrainModel();
                });
            PauseForScreenShot("p13 -- DIA peak scoring dialog with second best");
            
            RunUI(() =>
                {
                    editDlgDIA.PeakScoringModelName = "test3";
                });
            OkDialog(editDlgDIA, editDlgDIA.OkDialog);
            OkDialog(peptideSettingsDlgDIA, peptideSettingsDlgDIA.OkDialog);

            // Reintegrate
            var reintegrateDlgDia = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() => reintegrateDlgDia.ReintegrateAll = true);
            OkDialog(reintegrateDlgDia, reintegrateDlgDia.OkDialog);
        }
    }
}
