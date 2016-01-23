/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AddLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAddLibrary()
        {
            TestFilesZip = @"TestFunctional\AddLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestAddChromatogramLibrary();
        }

        protected void TestAddChromatogramLibrary()
        {
            const string libName = "Human Chromatograms";
            const string predictorName = "Human Chromatograms Predictor";
            const double regressionWindow = 5.0;

            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);
            var addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            RunUI(() =>
            {
                addLibDlg.LibraryName = libName;
                addLibDlg.LibraryPath = TestFilesDir.GetTestPath("Human-iRT_rev1.clib");
            });
            var addPredictorDlg = ShowDialog<AddRetentionTimePredictorDlg>(addLibDlg.OkDialog);
            RunUI(() =>
            {
                addPredictorDlg.PredictorName = predictorName;
                addPredictorDlg.PredictorWindow = regressionWindow;
            });

            OkDialog(addPredictorDlg, addPredictorDlg.OkDialog);
            OkDialog(addLibDlg, addLibDlg.OkDialog);
            OkDialog(libListDlg, libListDlg.OkDialog);

            // Check retention time predictor
            Assert.AreEqual(predictorName, Settings.Default.RetentionTimeList.Last().Name);

            // Check iRT calculator
            Assert.AreEqual(libName, Settings.Default.RTScoreCalculatorList.Last().Name);

            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.SelectedRTPredictor = predictorName;
            });

            var editRtDlg = ShowDialog<EditRTDlg>(peptideSettingsUi.EditRegression);

            Assert.AreEqual(regressionWindow, editRtDlg.Regression.TimeWindow);
            Assert.AreEqual(libName, editRtDlg.Regression.Calculator.Name);

            OkDialog(editRtDlg, editRtDlg.OkDialog);
            var addStandardsBiognosysDlg = ShowDialog<AddIrtStandardsToDocumentDlg>(peptideSettingsUi.OkDialog);
            OkDialog(addStandardsBiognosysDlg, addStandardsBiognosysDlg.BtnYesClick);
            VerifyAdd(1, 0);

            ResetModsAndSetStandards("apoa1", "apoa1.irtdb");
            var addStandardsApoa1Dlg = WaitForOpenForm<AddIrtStandardsToDocumentDlg>();
            OkDialog(addStandardsApoa1Dlg, addStandardsApoa1Dlg.BtnYesClick);
            VerifyAdd(1, 1);

            ResetModsAndSetStandards("pierce", "pierce.irtdb");
            var addStandardsPierceDlg = WaitForOpenForm<AddIrtStandardsToDocumentDlg>();
            OkDialog(addStandardsPierceDlg, addStandardsPierceDlg.BtnYesClick);
            VerifyAdd(1, 2);

            ResetModsAndSetStandards("sigma", "sigma.irtdb");
            var addStandardsSigmaDlg = WaitForOpenForm<AddIrtStandardsToDocumentDlg>();
            OkDialog(addStandardsSigmaDlg, addStandardsSigmaDlg.BtnYesClick);
            VerifyAdd(1, 2);
        }

        private void ResetModsAndSetStandards(string name, string libName)
        {
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            // Reset mods
            var modsListDlg1 = ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsDlg.EditStaticMods);
            RunUI(modsListDlg1.ResetList);
            OkDialog(modsListDlg1, modsListDlg1.OkDialog);
            var modsListDlg2 = ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsDlg.EditHeavyMods);
            RunUI(modsListDlg2.ResetList);
            OkDialog(modsListDlg2, modsListDlg2.OkDialog);

            // Set standards
            var regressionListDlg = ShowDialog<EditListDlg<SettingsListBase<RetentionTimeRegression>, RetentionTimeRegression>>(peptideSettingsDlg.EditRegressionList);
            var editRegressionDlg = ShowDialog<EditRTDlg>(regressionListDlg.AddItem);
            var editIrtCalcDlg = ShowDialog<EditIrtCalcDlg>(editRegressionDlg.AddCalculator);
            RunUI(() =>
            {
                editIrtCalcDlg.OpenDatabase(TestFilesDir.GetTestPath(libName));
                editIrtCalcDlg.CalcName = name;
            });
            OkDialog(editIrtCalcDlg, editIrtCalcDlg.OkDialog);
            RunUI(() =>
            {
                editRegressionDlg.SetRegressionName(name);
                editRegressionDlg.SetAutoCalcRegression(true);
                editRegressionDlg.SetTimeWindow(2);
            });
            OkDialog(editRegressionDlg, editRegressionDlg.OkDialog);
            OkDialog(regressionListDlg, regressionListDlg.OkDialog);
            RunUI(() => peptideSettingsDlg.SelectedRTPredictor = name);
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
        }

        private static void VerifyAdd(int numStaticMods, int numHeavyMods)
        {
            var calc = SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
            Assert.IsNotNull(calc);
            var standards = calc.GetStandardPeptides().Select(SequenceMassCalc.NormalizeModifiedSequence).ToArray();

            var peptides = SkylineWindow.Document.PeptideGroups.First().Peptides.ToArray();
            Assert.AreEqual(standards.Length, peptides.Length);
            foreach (var peptide in peptides)
                Assert.IsTrue(standards.Contains(peptide.ModifiedSequence));

            var mods = SkylineWindow.Document.Settings.PeptideSettings.Modifications;
            Assert.AreEqual(numStaticMods, mods.StaticModifications.Count);
            Assert.AreEqual(numHeavyMods, mods.HeavyModifications.Count);
        }
    }
}
