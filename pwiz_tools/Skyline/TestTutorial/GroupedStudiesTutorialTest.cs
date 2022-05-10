/*
 * Original author: Brendan MacLean <bendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class GroupedStudiesTutorialTest : AbstractFunctionalTestEx
    {
        protected override bool UseRawFiles
        {
            get { return !ForceMzml && ExtensionTestContext.CanImportAbWiff; }
        }

        [TestMethod]
        public void TestGroupedStudiesTutorialDraft()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            TestFilesZipPaths = new[]
                {
                    UseRawFiles
                               ? @"https://skyline.gs.washington.edu/tutorials/GroupedStudies.zip"
                               : @"https://skyline.gs.washington.edu/tutorials/GroupedStudiesMzmlV2.zip", // V2 has updated WIFF->mzML including machine serial #
                    @"TestTutorial\GroupedStudiesViews.zip"
                };
            RunFunctionalTest();
        }

        // Not L10N
        private const string HEAVY_R = "Label:13C(6)15N(4) (C-term R)";
        private const string HEAVY_K = "Label:13C(6)15N(2) (C-term K)";
        private const string HUMAN_MINI = "Human Plasma (mini)"; // Not L10N

        private string GetTestPath(string relativePath)
        {
            var folderExistGroupedStudies = UseRawFiles ? "GroupedStudies" : "GroupedStudiesMzml";
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderExistGroupedStudies, relativePath));
        }

        private string GetOcRawTestPath(string fileName = null)
        {
            string dirPath = GetTestPath(@"Ovarian Cancer\raw");
            return !string.IsNullOrEmpty(fileName)
                       ? Path.Combine(dirPath, fileName)
                       : dirPath;
        }

        protected override void DoTest()
        {
            // Configuring Peptide settings p. 4
            PeptideSettingsUI peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            PauseForScreenShot();

            var modHeavyK = new StaticMod(HEAVY_K, "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUI, "Edit Isotope Modification form");
            var modHeavyR = new StaticMod(HEAVY_R, "R", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyR, peptideSettingsUI, "Edit Isotope Modification form");
            RunUI(() => peptideSettingsUI.PickedHeavyMods = new[] { HEAVY_K, HEAVY_R });
            PauseForScreenShot();

            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            SetTransitionClipboardText(new[] {0, 1, 7, 8});

            PasteTransitionListSkipColumnSelect();
            RunUI(() =>
            {
                SkylineWindow.CollapsePeptides();
            });
            PauseForScreenShot();

            const string peptideStandardsName = "Tr_peps_set2";
            FindNode(peptideStandardsName);

            RunUI(() =>
                {
                    SkylineWindow.Cut();
                    SkylineWindow.SelectedPath = new IdentityPath(SequenceTree.NODE_INSERT_ID);
                    SkylineWindow.Paste();
                });

            // Create auto-calculate regression RT predictor, p. 10
            const string irtPredictorName = "iRT-OC-Study"; // Not L10N
            {
                var peptideSettingsUI2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() => peptideSettingsUI2.SelectedTab = PeptideSettingsUI.TABS.Prediction);
                var regressionDlg = ShowDialog<EditRTDlg>(peptideSettingsUI2.AddRTRegression);
                const string irtCalcName = "iRT-OC-Study";
                var editIrtCalcDlg = ShowDialog<EditIrtCalcDlg>(regressionDlg.AddCalculator);
                RunUI(() =>
                {
                        editIrtCalcDlg.CalcName = irtCalcName;
                        editIrtCalcDlg.CreateDatabase(GetTestPath("iRT-OC-Study.irtdb")); // Not L10N
                        SetTransitionClipboardText(new[] {15, 17}, c =>
                            {
                                if (string.Equals(c[8], "protein_name"))
                                    return null;
//                                c[17] = double.Parse(c[17], CultureInfo.InvariantCulture).ToString(LocalizationHelper.CurrentCulture);
                                return string.Equals(c[8], peptideStandardsName) ? c : null;
                            });
                        editIrtCalcDlg.DoPasteStandard();
                });

                PauseForScreenShot();

                RunUI(() =>
                {
                    string carbC = string.Format("C[+{0:F01}]", 57.0);
                    SetTransitionClipboardText(new[] { 15, 17 }, c =>
                        {
                            if (string.Equals(c[8], peptideStandardsName) || string.Equals(c[8], "protein_name"))
                                return null;
                            c[15] = c[15].Replace("[C160]", carbC).Replace("C[160]", carbC);
//                            c[17] = double.Parse(c[17], CultureInfo.InvariantCulture).ToString(LocalizationHelper.CurrentCulture);
                            return c;
                        });
                });

                var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(editIrtCalcDlg.DoPasteLibrary);

                PauseForScreenShot();   // Add Peptides form

                RunUI(() =>
                {
                    Assert.AreEqual(71, addPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(0, addPeptidesDlg.RunsConvertedCount);
                    Assert.AreEqual(0, addPeptidesDlg.RunsFailedCount);
                    addPeptidesDlg.OkDialog();
                });
                WaitForClosedForm(addPeptidesDlg);

                PauseForScreenShot();   // Edit iRT Calculator form

                RunUI(() => Assert.AreEqual(71, editIrtCalcDlg.LibraryPeptideCount));
                OkDialog(editIrtCalcDlg, editIrtCalcDlg.OkDialog);

                RunUI(() =>
                {
                    regressionDlg.SetRegressionName(irtPredictorName);
                    regressionDlg.ChooseCalculator(irtCalcName);
                    regressionDlg.SetAutoCalcRegression(true);
                    regressionDlg.SetTimeWindow(3);
                });

                PauseForScreenShot();   // Edit retention time predictor form

                OkDialog(regressionDlg, regressionDlg.OkDialog);

                PauseForScreenShot();   // Prediction tab

                RunUI(() => peptideSettingsUI2.SelectedTab = PeptideSettingsUI.TABS.Library);

                var editLibListUI = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI2.EditLibraryList);
                var addLibUI = ShowDialog<EditLibraryDlg>(editLibListUI.AddItem);
                RunUI(() => addLibUI.LibrarySpec =
                    new BiblioSpecLibSpec(HUMAN_MINI, GetOcRawTestPath("h130kdp_consensus.blib"))); // Not L10N
                PauseForScreenShot();

                OkDialog(addLibUI, addLibUI.OkDialog);
                OkDialog(editLibListUI, editLibListUI.OkDialog);

                RunUI(() => peptideSettingsUI2.PickedLibraries = new[] {HUMAN_MINI});
                PauseForScreenShot();

                OkDialog(peptideSettingsUI2, peptideSettingsUI2.OkDialog);
            }

            RunUI(() => SkylineWindow.SaveDocument(GetTestPath("OC-study.sky")));

            // Importing Data
            ImportResultsFiles(GetOcRawTestPath(), ExtAbWiff,
                IsFullData ? "R201217" : "R201217_plasma_revision_A", true);
            WaitForCondition(5*60*1000, () =>
                SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            WaitForDocumentLoaded();

            ImportResultsFiles(GetOcRawTestPath(), ExtAbWiff,
                IsFullData ? "R201203" : "R201203_plasma_revision_F", true);
            WaitForCondition(2*60*1000, () =>
                SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            WaitForDocumentLoaded();

            PauseForScreenShot();
        }

        private void SetTransitionClipboardText(int[] columnIndices, Func<string[], string[]> convertColumns = null)
        {
            var filePath = GetTestPath(!Equals(LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator, ".")
                                           ? @"Ovarian Cancer\raw\1-transitions-intl.txt"
                                           : @"Ovarian Cancer\raw\1-transitions.txt"); // Not L10N
            using (var transitionReader = new StreamReader(filePath))
            {
                var sbClipboard = new StringBuilder();
                string line;
                string lastLine = string.Empty;
                while ((line = transitionReader.ReadLine()) != null)
                {
                    var columns = line.ParseDsvFields(TextUtil.SEPARATOR_TSV);
                    if (convertColumns != null)
                    {
                        columns = convertColumns(columns);
                        if (columns == null)
                            continue;
                    }
                    var sbLine = new StringBuilder();
                    foreach (var columnIndex in columnIndices)
                    {
                        if (sbLine.Length > 0)
                            sbLine.Append(TextUtil.SEPARATOR_TSV);
                        sbLine.Append(columns[columnIndex]);
                    }
                    sbLine.AppendLine();
                    line = sbLine.ToString();
                    if (string.Equals(line, lastLine))
                        continue;
                    lastLine = line;
                    sbClipboard.Append(line);
                }
                SetClipboardTextUI(sbClipboard.ToString());
            }
        }
    }
}
